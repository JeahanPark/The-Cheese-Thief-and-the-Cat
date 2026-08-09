using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 벽감·치즈 드롭 프리팹과 레이아웃 배치 (v3.8, 2026-08-09)
    public static partial class SetupNetworkingScene
    {
        // 방 벽에서 안쪽으로 얼마나 들어와 앉는가. 벽에 딱 붙이면 콜라이더가 벽을 파고든다.
        private const float ALCOVE_WALL_INSET = 0.7f;
        // 치즈 지점과 이만큼은 떨어뜨린다 — 붙어 있으면 한 자리에서 다 해결돼 이동이 사라진다
        private const float ALCOVE_MIN_CHEESE_DIST = 3.5f;

        /// <summary>
        /// 벽감 프리팹. `CreateStructurePrefab`을 쓰지 않는 이유가 둘 있다 —
        /// ① 거기엔 `PlayerDeath`가 붙는데 그건 죽자마자 despawn한다. 벽감은
        ///    **despawn 전에 안의 것을 꺼내야** 하므로 순서를 남에게 맡길 수 없다.
        /// ② 설치음(`PlaceSfxOnSpawn`)이 붙는다. 벽감은 누가 설치하는 게 아니라 처음부터 맵에 있다.
        /// </summary>
        private static GameObject CreateAlcovePrefab(GameObject _cheeseDropPrefab)
        {
            var root = new GameObject("Alcove");
            try
            {
                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", CTC.Runtime.Info.InfoManager.Instance.Balance.AlcoveHp);

                // 덮치기는 RoleTag.Mouse만 때린다 — 고양이도 부술 수 있어야 하므로 붙인다.
                // 매치 생존 수에는 안 낀다(MatchManager.CountAlive가 IsPlayerObject로 거른다).
                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                // 맵에 원래 있던 벽이라 부숴도 장난기가 안 나온다 — v3.5의 '벽 때리기' 실패 방지
                root.AddComponent<BountyExempt>();

                var feedback = root.AddComponent<HitFeedback>();
                SetInt(feedback, "m_OverrideSfx", 1);
                SetInt(feedback, "m_HitSfx", (int)CTC.Runtime.Audio.SfxId.AlcoveHit);
                SetInt(feedback, "m_BreakSfx", (int)CTC.Runtime.Audio.SfxId.AlcoveBreak);
                // 부수는 소리가 곧 경쟁의 초대장이다 — 파밍(20m)과 같은 급으로 멀리 보낸다
                SetFloat(feedback, "m_SfxMaxDistance",
                    CTC.Runtime.Info.InfoManager.Instance.Balance.AlcoveBreakSoundMeters);

                var alcove = root.AddComponent<Alcove>();
                SetRef(alcove, "m_CheeseDropPrefab", _cheeseDropPrefab);

                // 벽감 본체 — 벽에 파인 함이라 납작하다(두께 0.4). 부숴도 지형은 안 뚫린다.
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                body.transform.localScale = new Vector3(1.2f, 1.0f, 0.4f);
                body.GetComponent<Renderer>().sharedMaterial = GridboxMaterial("Brown");

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, AlcovePrefabPath);
                BakeGlobalObjectIdHash(asset.GetComponent<NetworkObject>());
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>바닥에 뒹구는 치즈 덩이. 트리거라 몸을 막지 않는다 — 달려가 줍는 물건이다.</summary>
        private static GameObject CreateCheeseDropPrefab()
        {
            var root = new GameObject("CheeseDrop");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<CheeseDrop>();

                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.6f;          // 쥐 CC 반경 0.22 — 스쳐도 먹히는 넉넉함
                trigger.center = new Vector3(0f, 0.3f, 0f);

                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                body.transform.localScale = Vector3.one * 0.45f;
                // 시각물의 콜라이더는 지운다 — 남기면 쥐가 치즈에 걸려 넘어진다
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = GridboxMaterial("Yellow");

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, CheeseDropPrefabPath);
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
        /// 이미 만들어진 레이아웃 에셋에 **벽감 자리만 덧붙인다.**
        /// 방·복도·치즈·요새는 건드리지 않는다 — 맵을 다시 생성하면 시드가 바뀌어
        /// 검증까지 끝난 지형이 통째로 달라진다.
        ///
        /// 규칙: 방 하나에 하나씩, 벽면에 붙여서, 치즈 지점과는 떨어뜨려서.
        /// 복도에 두지 않는 이유는 부수는 19초 동안 도망칠 방향이 둘뿐이라 사형선고가 되기 때문.
        /// </summary>
        private static void PlanAlcovesInto(MapLayout _layout, int _count)
        {
            if (_layout == null || _count <= 0) return;

            var placed = new List<AlcovePlacement>();
            foreach (RoomRect room in _layout.Rooms)
            {
                if (placed.Count >= _count) break;
                if (!TryWallSpot(room, _layout, out Vector2 pos, out float yaw)) continue;

                placed.Add(new AlcovePlacement
                {
                    Pos = pos,
                    YawDeg = yaw,
                    Payload = AlcovePayload.Cheese,   // 강아지는 v3.8 5번에서
                });
            }

            _layout.SetAlcoves(placed);
            EditorUtility.SetDirty(_layout);
        }

        // 방의 네 벽을 순서대로 시도한다. 무작위를 쓰지 않는 건 같은 레이아웃이면
        // 항상 같은 자리가 나와야 QA가 재현 가능하기 때문이다.
        private static bool TryWallSpot(RoomRect _room, MapLayout _layout, out Vector2 _pos, out float _yaw)
        {
            Vector2 c = _room.Center;
            Vector2 half = _room.Size * 0.5f;

            // (자리, 방 안쪽을 보는 yaw) — 북·남·동·서
            var candidates = new (Vector2 pos, float yaw)[]
            {
                (new Vector2(c.x, c.y + half.y - ALCOVE_WALL_INSET), 180f),
                (new Vector2(c.x, c.y - half.y + ALCOVE_WALL_INSET), 0f),
                (new Vector2(c.x + half.x - ALCOVE_WALL_INSET, c.y), 270f),
                (new Vector2(c.x - half.x + ALCOVE_WALL_INSET, c.y), 90f),
            };

            foreach ((Vector2 pos, float yaw) in candidates)
            {
                if (!IsClearOfCheese(pos, _layout)) continue;
                _pos = pos;
                _yaw = yaw;
                return true;
            }

            _pos = default;
            _yaw = 0f;
            return false;
        }

        private static bool IsClearOfCheese(Vector2 _pos, MapLayout _layout)
        {
            foreach (CheesePlacement cp in _layout.Cheese)
            {
                if (Vector2.Distance(_pos, cp.Pos) < ALCOVE_MIN_CHEESE_DIST) return false;
            }
            return true;
        }
    }
}
