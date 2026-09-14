using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 천장을 짓는다(2026-09-14) — 나무 판 · 큰 들보 · 장선 · 채광창 테 · 들보에 매단 식료품.
    /// 자리는 <see cref="MapCeilingLogic"/>, 재료는 <see cref="MapAtmosphereProfile"/>, 호출은 <see cref="MapDresser"/>(빛 자리를 안 뒤).
    ///
    /// **전부 비주얼 전용이다.** 콜라이더가 없어 이동·네비·레이(시야·가림막 페이드)에 안 걸리고,
    /// **그림자를 드리우지 않는다** — 달빛(방향광)이 판에 막히면 맵이 통째로 꺼멓게 된다.
    /// 판·들보는 재료별로 **메시 하나로 합친다**(120m 맵에서도 드로우콜 두 개).
    /// </summary>
    public static class MapCeiling
    {
        private const string ROOT_NAME = "MapCeiling";

        public static void Build(Transform _parent, float _half, int _seed, IReadOnlyList<Vector2> _lamps, IReadOnlyList<Vector2> _moons)
        {
            if (_parent == null || _half <= 0f || !MapAtmosphere.IsNight) return;
            MapAtmosphereProfile profile = MapAtmosphere.LoadedProfile;
            if (profile == null || profile.CeilingDeckMaterial == null || profile.CeilingBeamMaterial == null) return;

            Transform old = _parent.Find(ROOT_NAME);
            if (old != null) Object.Destroy(old.gameObject);
            var root = new GameObject(ROOT_NAME).transform;
            root.SetParent(_parent, false);
            // 좌표는 월드 XZ다 — 부모가 어디 있든 월드 원점에 맞춘다.
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var deck = new BoxBatch();
            var beams = new BoxBatch();
            var windows = new BoxBatch();
            float y = MapCeilingLogic.DECK_Y;

            // 판 — **빈틈 없이 전부 덮는다**(사용자: 뚫린 것보다 덮는 게 중요). 달빛 칸은 닫힌 채광창: 판 밑에 푸른 유리 + 십자 창살 + 테.
            foreach (Vector2 c in MapCeilingLogic.CellCenters(_half, out float cell))
            {
                deck.Add(new Vector3(c.x, y + MapCeilingLogic.DECK_THICK * 0.5f, c.y), new Vector3(cell, MapCeilingLogic.DECK_THICK, cell));
                if (!MapCeilingLogic.IsSkylight(c, _moons)) continue;

                const float FRAME = 0.22f;
                const float PANE_INSET = 0.35f;
                float fy = y - 0.12f;
                float pane = cell - PANE_INSET * 2f;
                windows.Add(new Vector3(c.x, y - 0.03f, c.y), new Vector3(pane, 0.04f, pane));
                beams.Add(new Vector3(c.x, fy, c.y - pane * 0.5f), new Vector3(pane + FRAME, 0.24f, FRAME));
                beams.Add(new Vector3(c.x, fy, c.y + pane * 0.5f), new Vector3(pane + FRAME, 0.24f, FRAME));
                beams.Add(new Vector3(c.x - pane * 0.5f, fy, c.y), new Vector3(FRAME, 0.24f, pane + FRAME));
                beams.Add(new Vector3(c.x + pane * 0.5f, fy, c.y), new Vector3(FRAME, 0.24f, pane + FRAME));
                beams.Add(new Vector3(c.x, fy + 0.04f, c.y), new Vector3(pane, 0.12f, 0.1f));   // 창살
                beams.Add(new Vector3(c.x, fy + 0.04f, c.y), new Vector3(0.1f, 0.12f, pane));
            }

            // 장선(Z로 뻗음) — 판 바로 아래.
            float joistY = y - MapCeilingLogic.JOIST_DEPTH * 0.5f;
            foreach (float x in MapCeilingLogic.JoistXs(_half))
            {
                beams.Add(new Vector3(x, joistY, 0f), new Vector3(MapCeilingLogic.JOIST_WIDTH, MapCeilingLogic.JOIST_DEPTH, _half * 2f));
            }

            // 큰 들보(X로 뻗음) — 장선보다 깊이 내려와 천장에 리듬을 준다.
            float girderY = y - MapCeilingLogic.GIRDER_DEPTH * 0.5f;
            foreach (float z in MapCeilingLogic.GirderZs(_half))
            {
                beams.Add(new Vector3(0f, girderY, z), new Vector3(_half * 2f, MapCeilingLogic.GIRDER_DEPTH, MapCeilingLogic.GIRDER_WIDTH));
            }

            // 둘레 판벽 — 경계벽 위 틈으로 하늘이 새지 않게(MapCeilingLogic.CURTAIN_BOTTOM).
            float ch = MapCeilingLogic.DECK_Y + MapCeilingLogic.DECK_THICK - MapCeilingLogic.CURTAIN_BOTTOM;
            float cy = MapCeilingLogic.CURTAIN_BOTTOM + ch * 0.5f;
            float edge = _half + MapCeilingLogic.CURTAIN_THICK * 0.5f;
            float span = _half * 2f + MapCeilingLogic.CURTAIN_THICK * 2f;
            beams.Add(new Vector3(0f, cy, edge), new Vector3(span, ch, MapCeilingLogic.CURTAIN_THICK));
            beams.Add(new Vector3(0f, cy, -edge), new Vector3(span, ch, MapCeilingLogic.CURTAIN_THICK));
            beams.Add(new Vector3(edge, cy, 0f), new Vector3(MapCeilingLogic.CURTAIN_THICK, ch, span));
            beams.Add(new Vector3(-edge, cy, 0f), new Vector3(MapCeilingLogic.CURTAIN_THICK, ch, span));

            deck.Emit(root, "CeilingDeck", profile.CeilingDeckMaterial);
            if (profile.CeilingWindowMaterial != null) windows.Emit(root, "CeilingWindows", profile.CeilingWindowMaterial);
            beams.Emit(root, "CeilingBeams", profile.CeilingBeamMaterial);

            PlaceHangings(root, profile, _half, _seed, _lamps, _moons);
        }

        private static void PlaceHangings(Transform _root, MapAtmosphereProfile _profile, float _half, int _seed,
            IReadOnlyList<Vector2> _lamps, IReadOnlyList<Vector2> _moons)
        {
            GameObject[] prefabs = _profile.HangingPrefabs;
            if (prefabs == null || prefabs.Length == 0) return;

            float hookY = MapCeilingLogic.DECK_Y - MapCeilingLogic.GIRDER_DEPTH;
            foreach (MapCeilingLogic.HangSlot slot in MapCeilingLogic.HangSlots(_half, _seed, prefabs.Length, _lamps, _moons))
            {
                GameObject prefab = prefabs[slot.Kind];
                if (prefab == null) continue;

                var hook = new GameObject("Hanging").transform;
                hook.SetParent(_root, false);
                hook.position = new Vector3(slot.Pos.x, hookY, slot.Pos.y);
                hook.rotation = Quaternion.Euler(0f, MapCeilingLogic.Hash(slot.Kind, (int)(slot.Pos.x * 10f), _seed) % 360, 0f);

                // 줄 — 메시는 피벗이 위, 아래로 3m. 길이만큼 줄인다.
                if (_profile.HangCordMesh != null && _profile.HangCordMaterial != null)
                {
                    var cord = new GameObject("Cord");
                    cord.transform.SetParent(hook, false);
                    cord.transform.localScale = new Vector3(1.4f, slot.Length / 3f, 1.4f);
                    cord.AddComponent<MeshFilter>().sharedMesh = _profile.HangCordMesh;
                    Quiet(cord.AddComponent<MeshRenderer>(), _profile.HangCordMaterial);
                }

                GameObject item = Object.Instantiate(prefab, hook);
                item.transform.localPosition = new Vector3(0f, -slot.Length, 0f);
                foreach (Renderer r in item.GetComponentsInChildren<Renderer>()) Quiet(r, null);
            }
        }

        // 그림자 끔 · 광원 탐침 끔 — 천장 소품은 조명을 받기만 한다.
        private static void Quiet(Renderer _renderer, Material _material)
        {
            if (_material != null) _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>축 정렬 상자 여러 개를 메시 하나로 모은다(면마다 법선이 따로라 모서리가 또렷하다). UV는 안 쓴다 — 트리플래너 셰이더.</summary>
        private sealed class BoxBatch
        {
            private readonly List<Vector3> m_Verts = new List<Vector3>();
            private readonly List<Vector3> m_Normals = new List<Vector3>();
            private readonly List<int> m_Tris = new List<int>();

            private static readonly Vector3[] FACE_NORMALS =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };

            public void Add(Vector3 _center, Vector3 _size)
            {
                Vector3 h = _size * 0.5f;
                foreach (Vector3 n in FACE_NORMALS)
                {
                    // 면의 두 접선 — 법선에 수직인 두 축
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(n, u);
                    Vector3 fc = _center + Vector3.Scale(n, h);
                    Vector3 du = Vector3.Scale(u, h);
                    Vector3 dv = Vector3.Scale(v, h);

                    int start = m_Verts.Count;
                    m_Verts.Add(fc - du - dv);
                    m_Verts.Add(fc + du - dv);
                    m_Verts.Add(fc + du + dv);
                    m_Verts.Add(fc - du + dv);
                    for (int i = 0; i < 4; i++) m_Normals.Add(n);

                    // 감김은 법선과 맞춘다 — 뒤집히면 컬링에 통째로 사라진다
                    Vector3 cross = Vector3.Cross(m_Verts[start + 1] - m_Verts[start], m_Verts[start + 2] - m_Verts[start]);
                    if (Vector3.Dot(cross, n) >= 0f)
                    {
                        m_Tris.Add(start); m_Tris.Add(start + 1); m_Tris.Add(start + 2);
                        m_Tris.Add(start); m_Tris.Add(start + 2); m_Tris.Add(start + 3);
                    }
                    else
                    {
                        m_Tris.Add(start); m_Tris.Add(start + 2); m_Tris.Add(start + 1);
                        m_Tris.Add(start); m_Tris.Add(start + 3); m_Tris.Add(start + 2);
                    }
                }
            }

            public void Emit(Transform _parent, string _name, Material _material)
            {
                if (m_Verts.Count == 0) return;
                var mesh = new Mesh { name = _name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(m_Verts);
                mesh.SetNormals(m_Normals);
                mesh.SetTriangles(m_Tris, 0);
                mesh.RecalculateBounds();

                var go = new GameObject(_name);
                go.transform.SetParent(_parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                Quiet(go.AddComponent<MeshRenderer>(), _material);
                go.AddComponent<MeshLifetime>().Own(mesh);
            }
        }
    }
}
