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
            string name, string prefabPath, PrimitiveType primitive, Material material,
            int maxHp, Vector3 scale, System.Action<GameObject> addExtras, int layer = -1)
        {
            var root = new GameObject(name);
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", maxHp);

                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                root.AddComponent<PlayerDeath>();
                root.AddComponent<HitFeedback>();
                var placeSfx = root.AddComponent<CTC.Runtime.Audio.PlaceSfxOnSpawn>();
                SetInt(placeSfx, "m_Sfx", (int)CTC.Runtime.Audio.SfxId.DecoyPlace);
                addExtras(root);

                var body = GameObject.CreatePrimitive(primitive);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                // 프리미티브 기본 높이가 캡슐·구·실린더는 2, 큐브는 1 — 밑면을 바닥(y=0)에 맞춘다
                float unitHalfHeight = primitive == PrimitiveType.Cube ? 0.5f : 1f;
                body.transform.localPosition = new Vector3(0f, scale.y * unitHalfHeight, 0f);
                body.transform.localScale = scale;
                if (material != null) body.GetComponent<Renderer>().sharedMaterial = material;

                // 레이어는 콜라이더가 붙은 Body까지 칠해야 물리 필터가 걸린다
                if (layer >= 0)
                {
                    foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                        tr.gameObject.layer = layer;
                }

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
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
        // 지붕 없음 — 점프가 없어 벽 1.2로 충분. 내부 회복은 FortressHealZone(반경 1.0).
        // 입구 방향 = 루트 로컬 +Z (스폰 시 yaw 회전으로 지정).
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
                root.AddComponent<FortressHealZone>();

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
    }
}
