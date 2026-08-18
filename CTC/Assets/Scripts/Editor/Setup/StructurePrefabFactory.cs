using System.Linq;
using System.Reflection;
using CTC.Runtime.Cameras;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Managers;
using CTC.Runtime.Map;
using CTC.Runtime.Match;
using CTC.Runtime.Networking;
using CTC.Runtime.Player;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 설치물 프리팹(미끼·요새·창고) 생성 (개편 4단계 분할, 2026-07-31)
    public static partial class SetupNetworkingScene
    {
        // 설치형 NetworkObject 프리팹(요새·미끼). 프리미티브 + Health/RoleTag(Mouse)/PlayerDeath.
        // RoleTag.Mouse라 고양이 덮치기 판정을 그대로 받는다(파괴 가능, 보상은 플레이어 한정).
        private static GameObject CreateStructurePrefab(
            string _name, string _prefabPath, PrimitiveType _primitive, Material _material,
            int _maxHp, Vector3 _scale, System.Action<GameObject> _addExtras, int _layer = -1)
        {
            var root = new GameObject(_name);
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", _maxHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                root.AddComponent<PlayerDeath>();
                root.AddComponent<HitFeedback>();
                root.AddComponent<CTC.Runtime.Audio.PlaceSfxOnSpawn>(); // 기본값 FortressPlace
                _addExtras(root);

                var body = GameObject.CreatePrimitive(_primitive);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                // 프리미티브 기본 높이가 캡슐·구·실린더는 2, 큐브는 1 — 밑면을 바닥(y=0)에 맞춘다
                float unitHalfHeight = _primitive == PrimitiveType.Cube ? 0.5f : 1f;
                body.transform.localPosition = new Vector3(0f, _scale.y * unitHalfHeight, 0f);
                body.transform.localScale = _scale;
                if (_material != null) body.GetComponent<Renderer>().sharedMaterial = _material;

                // 레이어는 콜라이더가 붙은 Body까지 칠해야 물리 필터가 걸린다
                if (_layer >= 0)
                {
                    foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                        tr.gameObject.layer = _layer;
                }

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // v3 진입형 요새(원작 벙커): 외벽 4면 + 쥐 전용 입구(1.0×0.75 — v2 쥐구멍 실효치수).
        // 쥐 CC(0.44Ø/0.5h, skinWidth 포함 실효 0.60/0.63)만 통과, 고양이(0.86/1.03)는 차단.
        // 지붕 없음 — 점프가 없어 벽 1.2로 충분. 회복은 없다(2026-08-15 즉발 방패화 — FortressShield).
        // 입구 방향 = 루트 로컬 +Z (스폰 시 yaw 회전으로 지정). 치수는 FortressPushLogic과 같아야 한다.
        private const float FORTRESS_WALL_HEIGHT = 1.2f;

        private const float FORTRESS_HALF = 1.2f;          // 풋프린트 2.4×2.4
        private const float FORTRESS_DOOR_WIDTH = 1.0f;

        private const float FORTRESS_DOOR_HEIGHT = 0.75f;

        private const float FORTRESS_WALL_THICKNESS = 0.2f;

        // 창고 생산 연출 — 지붕에서 노란 치즈 조각이 튀어오른다. 서버 지급 순간에 Play()로 1회 분출.
        // 멀리서도 "저기서 벌고 있다"가 보여야 해서 위로 1m 가까이 솟는다(소리 가청 15m와 짝).
        private static ParticleSystem CreateProduceVfx(GameObject _root)
        {
            var vfxGo = new GameObject("ProduceVfx");
            vfxGo.transform.SetParent(_root.transform, false);
            vfxGo.transform.localPosition = new Vector3(0f, 0.85f, 0f); // 큐브(높이 0.8) 지붕 바로 위

            var ps = vfxGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 2.2f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.85f, 0.2f);
            main.gravityModifier = 1.6f;
            main.maxParticles = 32;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로

            var renderer = vfxGo.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GridboxMaterial("Yellow");
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = PrimitiveMesh(PrimitiveType.Cube);

            return ps;
        }

        private static GameObject CreateFortressPrefab()
        {
            Material wallMat = GridboxMaterial("Brown");
            var root = new GameObject("CheeseFortress");
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", ShopLogic.FORTRESS_HP);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                root.AddComponent<PlayerDeath>();
                root.AddComponent<HitFeedback>();
                root.AddComponent<CTC.Runtime.Audio.PlaceSfxOnSpawn>(); // 기본값 FortressPlace
                root.AddComponent<FortressShield>(); // 서는 순간 겹친 몸을 밀어낸다

                float h = FORTRESS_WALL_HEIGHT;
                float half = FORTRESS_HALF;
                float t = FORTRESS_WALL_THICKNESS;

                // 좌우벽만 통벽 — 앞(+Z)·뒤(-Z) 양면에 쥐 전용 문 (한쪽 문 캠핑을 반대편 탈출로 카운터, 2026-07-26)
                AddBox(root, wallMat, "Wall_Left", new Vector3(-half + t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));
                AddBox(root, wallMat, "Wall_Right", new Vector3(half - t * 0.5f, h * 0.5f, 0f),
                    new Vector3(t, h, half * 2f - t * 2f));

                // 문 있는 벽: 입구(폭 1.0) 좌우 세그먼트 + 인방(입구 위 y 0.75~1.2) — 앞뒤 공용
                float sideWidth = half - FORTRESS_DOOR_WIDTH * 0.5f;
                float sideCenterX = FORTRESS_DOOR_WIDTH * 0.5f + sideWidth * 0.5f;
                float lintelHeight = h - FORTRESS_DOOR_HEIGHT;
                foreach ((string tag, float z) in new[] { ("Front", half - t * 0.5f), ("Back", -half + t * 0.5f) })
                {
                    AddBox(root, wallMat, $"Wall_{tag}_L", new Vector3(-sideCenterX, h * 0.5f, z),
                        new Vector3(sideWidth, h, t));
                    AddBox(root, wallMat, $"Wall_{tag}_R", new Vector3(sideCenterX, h * 0.5f, z),
                        new Vector3(sideWidth, h, t));
                    AddBox(root, wallMat, $"Wall_{tag}_Lintel",
                        new Vector3(0f, FORTRESS_DOOR_HEIGHT + lintelHeight * 0.5f, z),
                        new Vector3(FORTRESS_DOOR_WIDTH, lintelHeight, t));
                }

                // **벽마다 따로 구멍을 판다** — 통째로 도려내면 쥐 전용 문까지 막혀
                // 요새 안이 갈 수 없는 곳이 된다. 문은 벽 사이의 빈칸이라 저절로 남는다.
                CarveWalls(root);

                // 길찾기 격자 자국(2026-08-18) — 벽은 **고양이가 부술 수 있는 칸**, 입구는 **쥐 전용 문**.
                // 인방(입구 위 가로대)이 문 자리에도 콜라이더를 두므로, 문 띠를 따로 찍지 않으면
                // 쥐까지 막힌다. 인방 높이(0.75)가 곧 "쥐는 지나고 고양이는 못 지난다"의 근거다.
                var blocker = root.AddComponent<CTC.Runtime.Ai.NavGridBlocker>();
                SetInt(blocker, "m_Kind", (int)CTC.Runtime.Ai.NavStructureKind.CatBreakable);
                SetFloat(blocker, "m_DoorHalfWidth", FORTRESS_DOOR_WIDTH * 0.5f);
                // 벽을 관통하는 터널 길이 — 벽(0.2)보다 훨씬 길어야 안팎이 이어진다(셀이 0.5m라
                // 벽 자국이 문 양옆으로 번지기 때문). 폭은 개구부(1.0) 그대로다.
                SetFloat(blocker, "m_DoorDepth", 1.2f);
                SetFloat(blocker, "m_DoorDistance", FORTRESS_HALF - FORTRESS_WALL_THICKNESS * 0.5f);

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, FortressPrefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 콜라이더가 있는 자식마다 <see cref="CTC.Runtime.Map.NavMeshCarver"/>를 붙인다 (2026-08-12).
        ///
        /// 이동 폴리곤은 맵을 지을 때 한 번 굽는다. 판 중에 세워지는 구조물은 거기 없어서,
        /// 길찾기가 "그리로 걸어가면 된다"고 답하고 걷는 쪽이 구조물을 민다.
        /// 카버가 제 자리만 도려내면 다시 굽지 않고도 해결된다.
        /// </summary>
        private static void CarveWalls(GameObject _root)
        {
            foreach (Collider col in _root.GetComponentsInChildren<Collider>(true))
            {
                if (col.gameObject.GetComponent<CTC.Runtime.Map.NavMeshCarver>() == null)
                {
                    col.gameObject.AddComponent<CTC.Runtime.Map.NavMeshCarver>();
                }
            }
        }
    }
}
