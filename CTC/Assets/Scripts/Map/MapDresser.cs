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
        // 팩은 사람 스케일(선반 ~2m)이다 — 거대 세계(벽 5m)에선 키워야 가구가 벽 노릇을 한다
        [SerializeField] private float m_UnitScale = 2f;

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
                if (n.StartsWith("Wall_"))
                    TileAlong(child, m_ShelfUnits, dressRoot, _seed);
                else if (n.StartsWith("CorridorWall") || n.StartsWith("CorridorEndCap"))
                    TileAlong(child, m_CrateUnits, dressRoot, _seed + 1);
                else if (n == "Obstacles")
                    foreach (Transform o in child)
                        ReplaceVisual(o, m_ObstacleUnits, dressRoot, _seed + 2);
            }
        }

        /// <summary>벽 박스의 긴 축을 따라 가구를 타일링 — 양면에 붙인다.</summary>
        private void TileAlong(Transform _wall, GameObject[] _units, Transform _parent, int _seed)
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
                    Vector3 pos = _wall.position + axis * (start + i * UNIT_WIDTH)
                                + face * (side * (thick * 0.5f + 0.05f));
                    pos.y = 0f;
                    GameObject go = Instantiate(prefab, pos, Quaternion.LookRotation(face * side), _parent);
                    go.name = $"{prefab.name}_{i}";
                    go.transform.localScale *= m_UnitScale;
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
            StripPhysics(go);
        }

        /// <summary>팩 프리팹의 콜라이더·리지드바디 제거 — 물리는 원본 박스의 몫.</summary>
        private static void StripPhysics(GameObject _go)
        {
            foreach (Collider c in _go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody r in _go.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
        }
    }
}
