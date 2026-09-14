using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 빌드 뒤에 가구 옷을 입히는 Worker(규칙 6) — 콜라이더·네비그리드는 절대 안 건드린다.
    /// 서버·클라이언트 모두 로컬로 돈다(비주얼 전용). 시드·위치 기반 선택이라 전원이 같은 그림을 본다.
    /// 프리팹 배열이 비어 있으면 아무 일도 안 한다 — 팩 미임포트 시 단색 백킹으로 후퇴.
    /// </summary>
    public class MapDresser : MonoBehaviour
    {
        private const float UNIT_WIDTH = 2f;          // 가구 한 칸 폭(m)
        private const string DRESS_ROOT = "MapDress";

        [SerializeField] private MapBuilder m_Builder;
        [SerializeField] private GameObject[] m_ShelfUnits;     // 방 벽 앞 — 선반
        [SerializeField] private GameObject[] m_CrateUnits;     // 복도 벽 — 궤짝
        [SerializeField] private GameObject[] m_ObstacleUnits;  // 장애물 — 배럴·호박
        // 팩은 사람 스케일(선반 ~2m)이다 — 거대 세계(벽 5m)에선 키워야 가구가 벽 노릇을 한다.
        // 궤짝만 낮게 — 복도 벽(2.4m)에 2배 궤짝을 대면 통로를 통째로 먹는다(2026-08-31 검수).
        [SerializeField] private float m_UnitScale = 2f;
        [SerializeField] private float m_CrateScale = 1.4f;

        public void Dress(int _seed)
        {
            Transform root = m_Builder != null ? m_Builder.BuiltRoot : null;
            if (root == null) return;

            Transform old = root.Find(DRESS_ROOT);
            if (old != null) Destroy(old.gameObject);

            var dressRoot = new GameObject(DRESS_ROOT).transform;
            dressRoot.SetParent(root, false);

            // 스냅샷 — 순회 중 dressRoot 아래가 자라나므로 목록을 먼저 뜬다
            var segments = new List<Transform>();
            foreach (Transform child in root)
                if (child != dressRoot) segments.Add(child);

            foreach (Transform child in segments)
            {
                string n = child.name;
                if (n.StartsWith("Wall_Shelf_"))
                    TileTiers(child, m_ShelfUnits, dressRoot, _seed, m_UnitScale);   // 창고 선반 열 — 5m를 2단으로 채운다
                else if (n.StartsWith("Wall_"))
                    TileAlong(child, m_ShelfUnits, dressRoot, _seed, m_UnitScale);
                else if (n.StartsWith("CorridorWall") || n.StartsWith("CorridorEndCap"))
                    TileAlong(child, m_CrateUnits, dressRoot, _seed + 1, m_CrateScale);
                else if (n.StartsWith("Obstacle_Block_"))
                    CratePile(child, m_CrateUnits, dressRoot, _seed + 4);
                else if (n == "Obstacles")
                    foreach (Transform o in child)
                        ReplaceVisual(o, m_ObstacleUnits, dressRoot, _seed + 2);
            }

            PlaceLightPools(dressRoot, _seed);
        }

        /// <summary>앰버 빛웅덩이 — 3방 중 1방꼴. 빛 = 위험이라 드문드문해야 스텔스가 산다(도판 1).</summary>
        private void PlaceLightPools(Transform _parent, int _seed)
        {
            MapLayout layout = m_Builder != null ? m_Builder.CurrentLayout : null;
            if (layout == null) return;

            // 밤 하늘·후보정(2026-09-14) — 밤(안개 켠 본 게임)일 때만, 이미 있으면 그대로.
            MapAtmosphere.ApplyScene();

            // 창고 평면도(2026-09-07)는 빛 자리를 직접 든다 — "3방 중 1" 규칙을 안 쓴다
            // 천장(2026-09-14)이 전등·채광창 자리를 알아야 한다 — 빛을 놓으면서 모은다.
            var lamps = new List<Vector2>();
            var moons = new List<Vector2>();

            if (layout.HandPlaced)
            {
                for (int i = 0; i < layout.Lights.Count; i++)
                {
                    LightPlacement lp = layout.Lights[i];
                    (lp.Moon ? moons : lamps).Add(lp.Pos);
                    var lgo = new GameObject("LightPool_" + i);
                    lgo.transform.SetParent(_parent, false);
                    lgo.transform.position = new Vector3(lp.Pos.x, lp.Moon ? 6f : 3.5f, lp.Pos.y);
                    var lt = lgo.AddComponent<Light>();
                    lt.type = LightType.Point;
                    lt.color = lp.Moon ? new Color(0.55f, 0.70f, 1.0f) : new Color(0.91f, 0.71f, 0.21f);
                    lt.intensity = lp.Moon ? 3.5f : 5.5f;
                    lt.range = lp.Radius;
                    lt.shadows = LightShadows.None;
                    // 광원이 눈에 보이게 — 전등(갓·전구·빛줄기·먼지) 또는 달빛 기둥(2026-09-14)
                    MapAtmosphere.DecorateLight(lt, lp.Moon, lp.Radius, PositionSeed(lp.Pos.x, lp.Pos.y, _seed));
                }
                MapCeiling.Build(_parent, layout.HalfExtent, _seed, lamps, moons);
                return;
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (MapDressLogic.Pick(i, 3, _seed + 3) != 0) continue;

                RoomRect room = layout.Rooms[i];
                lamps.Add(room.Center);
                var go = new GameObject($"LightPool_{i}");
                go.transform.SetParent(_parent, false);
                go.transform.position = new Vector3(room.Center.x, 3.5f, room.Center.y);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.91f, 0.71f, 0.21f);   // 앰버 등불
                light.intensity = 5.5f;   // 2.2는 바닥이 미지근했다 — 웅덩이는 한눈에 읽혀야 위험이다
                light.range = 12f;
                light.shadows = LightShadows.None;   // 웅덩이 수십 개 — 그림자는 달빛 하나면 된다
                MapAtmosphere.DecorateLight(light, false, light.range, PositionSeed(room.Center.x, room.Center.y, _seed));
            }
            MapCeiling.Build(_parent, layout.HalfExtent, _seed, lamps, moons);
        }

        /// <summary>위치가 곧 신원 — 인스턴스 ID는 클라이언트마다 달라 전등마다 깜빡임이 어긋난다.</summary>
        private static int PositionSeed(float _x, float _z, int _seed)
        {
            return Mathf.RoundToInt(_x * 7f + _z * 13f) * 31 + _seed;
        }

        /// <summary>
        /// 창고 선반 열(2026-09-07): 5 m 박스를 선반으로 **2단** 채운다 — 1단만 두면 위쪽 3 m가 맨 판벽으로 남아
        /// 벽돌 벽처럼 읽혔다(BotLab 실측). 위 단은 아래 단의 렌더 높이만큼 올린다.
        /// </summary>
        private void TileTiers(Transform _wall, GameObject[] _units, Transform _parent, int _seed, float _scale)
        {
            if (_units == null || _units.Length == 0) return;

            int before = _parent.childCount;
            TileAlong(_wall, _units, _parent, _seed, _scale);
            int firstTier = _parent.childCount - before;
            if (firstTier == 0) return;

            // 1단 높이 = 방금 놓은 것들의 렌더 높이 최대값
            float tierHeight = 0f;
            for (int i = before; i < _parent.childCount; i++)
            {
                if (TryGetWorldBounds(_parent.GetChild(i).gameObject, out Bounds b)) tierHeight = Mathf.Max(tierHeight, b.size.y);
            }
            if (tierHeight < 0.5f || tierHeight * 2f > _wall.localScale.y + 0.5f) return;

            int mid = _parent.childCount;
            TileAlong(_wall, _units, _parent, _seed + 7, _scale);
            for (int i = mid; i < _parent.childCount; i++)
            {
                Transform t = _parent.GetChild(i);
                t.position += Vector3.up * tierHeight;
                t.name += "_T2";
            }
        }

        /// <summary>
        /// 작업대·자루 더미 박스(2026-09-07): 박스 렌더러를 끄고 발자국을 궤짝으로 채운다(2 m 격자, 높이 3 m면 2층).
        /// 계단(5 m)은 그대로 판벽 박스 — 팩에 계단이 없다.
        /// </summary>
        private void CratePile(Transform _block, GameObject[] _units, Transform _parent, int _seed)
        {
            if (_units == null || _units.Length == 0) return;
            Vector3 s = _block.localScale;
            if (s.y > 4f) return;   // 계단

            var renderer = _block.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;

            const float UNIT = 2f;
            int nx = Mathf.Max(1, Mathf.FloorToInt(s.x / UNIT));
            int nz = Mathf.Max(1, Mathf.FloorToInt(s.z / UNIT));
            int tiers = s.y >= 2.6f ? 2 : 1;
            float x0 = _block.position.x - (nx - 1) * UNIT * 0.5f;
            float z0 = _block.position.z - (nz - 1) * UNIT * 0.5f;
            int key = Mathf.RoundToInt(_block.position.x * 7f + _block.position.z * 13f);

            float tierHeight = 0f;
            for (int tier = 0; tier < tiers; tier++)
            {
                for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    GameObject prefab = _units[MapDressLogic.Pick(key + ix * 3 + iz * 5 + tier * 11, _units.Length, _seed)];
                    if (prefab == null) continue;
                    float yaw = PantryTextureLogic.Hash(key + ix + iz * 7 + tier, 3, _seed) * 40f - 20f;
                    var go = Instantiate(prefab, new Vector3(x0 + ix * UNIT, 0f, z0 + iz * UNIT), Quaternion.Euler(0f, yaw, 0f), _parent);
                    go.transform.localScale *= m_CrateScale;
                    AlignToFloor(go);
                    if (tier > 0) go.transform.position += Vector3.up * tierHeight;
                    else if (TryGetWorldBounds(go, out Bounds b)) tierHeight = Mathf.Max(tierHeight, b.size.y);
                    StripPhysics(go);
                }
            }
        }

        /// <summary>벽 박스의 긴 축을 따라 가구를 타일링 — 양면에 붙인다.</summary>
        private void TileAlong(Transform _wall, GameObject[] _units, Transform _parent, int _seed, float _scale)
        {
            if (_units == null || _units.Length == 0) return;

            Vector3 s = _wall.localScale;
            bool xLong = s.x >= s.z;
            float length = xLong ? s.x : s.z;
            float thick = xLong ? s.z : s.x;

            int count = MapDressLogic.TileCount(length, UNIT_WIDTH);
            if (count == 0) return;
            float start = MapDressLogic.TileOffset(length, UNIT_WIDTH, count) - length * 0.5f + UNIT_WIDTH * 0.5f;

            Vector3 axis = _wall.rotation * (xLong ? Vector3.right : Vector3.forward);
            Vector3 face = _wall.rotation * (xLong ? Vector3.forward : Vector3.right);
            // 시드에 인스턴스 ID를 쓰지 않는다 — 클라이언트마다 다르다. 위치가 곧 신원이다.
            int wallKey = Mathf.RoundToInt(_wall.position.x * 7f + _wall.position.z * 13f);

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = _units[MapDressLogic.Pick(i * 31 + wallKey, _units.Length, _seed)];
                if (prefab == null) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 outward = face * side;
                    // 경계와 맞닿은 면(바깥쪽)은 안 입힌다 — 맵 밖에 가구가 떠 있었다(2026-08-31 검수)
                    float facePlane = Vector3.Dot(_wall.position, outward) + thick * 0.5f;
                    float half = m_Builder.CurrentLayout != null ? m_Builder.CurrentLayout.HalfExtent : float.MaxValue;
                    if (facePlane > half - 0.2f) continue;

                    Vector3 pos = _wall.position + axis * (start + i * UNIT_WIDTH);
                    pos.y = 0f;
                    GameObject go = Instantiate(prefab, pos, Quaternion.LookRotation(outward), _parent);
                    go.name = $"{prefab.name}_{i}";
                    go.transform.localScale *= _scale;
                    // 피벗을 믿지 않는다(2026-08-31 검수: 벽에 파묻혔다) — 렌더 AABB로
                    // 뒷면을 벽면에, 바닥을 지면에 맞춘다. 팩마다 피벗이 제각각이라 이 길뿐이다.
                    AlignToWallFace(go, outward, Vector3.Dot(_wall.position, outward) + thick * 0.5f);
                    StripPhysics(go);
                }
            }
        }

        /// <summary>장애물: 박스 렌더러를 끄고 배럴 비주얼로 교체. 콜라이더는 박스가 계속 든다.</summary>
        private void ReplaceVisual(Transform _obstacle, GameObject[] _units, Transform _parent, int _seed)
        {
            if (_units == null || _units.Length == 0) return;

            var renderer = _obstacle.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;

            int key = Mathf.RoundToInt(_obstacle.position.x * 7f + _obstacle.position.z * 13f);
            GameObject prefab = _units[MapDressLogic.Pick(key, _units.Length, _seed)];
            if (prefab == null) return;

            Vector3 pos = _obstacle.position;
            pos.y = 0f;
            float yaw = PantryTextureLogic.Hash(key, 5, _seed) * 360f;
            GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), _parent);
            go.transform.localScale *= m_UnitScale;
            AlignToFloor(go);   // 피벗이 가운데인 팩 프리팹은 바닥에 반쯤 파묻힌다
            StripPhysics(go);
        }

        /// <summary>렌더 AABB의 뒷면을 벽면(+2cm)에, 바닥을 지면에 맞춘다.</summary>
        private static void AlignToWallFace(GameObject _go, Vector3 _outward, float _wallPlane)
        {
            if (!TryGetWorldBounds(_go, out Bounds b)) return;

            // AABB를 outward 방향으로 사영 — 안쪽(벽 쪽) 모서리 좌표
            float halfExt = Mathf.Abs(b.extents.x * _outward.x)
                          + Mathf.Abs(b.extents.y * _outward.y)
                          + Mathf.Abs(b.extents.z * _outward.z);
            float innerEdge = Vector3.Dot(b.center, _outward) - halfExt;
            float shift = (_wallPlane + 0.02f) - innerEdge;
            _go.transform.position += _outward * shift + Vector3.up * (0.01f - b.min.y);
        }

        /// <summary>렌더 AABB의 바닥을 지면(+1cm)에 맞춘다.</summary>
        private static void AlignToFloor(GameObject _go)
        {
            if (!TryGetWorldBounds(_go, out Bounds b)) return;
            _go.transform.position += Vector3.up * (0.01f - b.min.y);
        }

        private static bool TryGetWorldBounds(GameObject _go, out Bounds _bounds)
        {
            Renderer[] renderers = _go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { _bounds = default; return false; }
            _bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) _bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        /// <summary>
        /// 팩 프리팹의 물리를 눕힌다 — 리지드바디는 제거, 콜라이더는 **트리거로 전환**.
        /// 트리거는 이동(CC)·게임플레이 레이(전부 Ignore)를 안 건드리면서
        /// OccluderFader(Collide로 캐스트)에게만 잡혀 카메라 가림 페이드가 산다(2026-08-31).
        /// </summary>
        private static void StripPhysics(GameObject _go)
        {
            // 원본 콜라이더는 전부 버린다 — concave MeshCollider는 트리거가 못 된다(런타임 에러).
            foreach (Collider c in _go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody r in _go.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);

            // 페이더용 트리거 박스 하나 — 렌더 경계 AABB. 벽 가구는 축 정렬이라 딱 맞고,
            // 회전한 배럴은 좀 크게 잡히지만 페이드 용도로는 충분하다.
            Renderer[] renderers = _go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            var box = _go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = _go.transform.InverseTransformPoint(b.center);
            Vector3 ls = _go.transform.lossyScale;
            box.size = new Vector3(
                b.size.x / Mathf.Max(ls.x, 1e-4f),
                b.size.y / Mathf.Max(ls.y, 1e-4f),
                b.size.z / Mathf.Max(ls.z, 1e-4f));
        }
    }
}
