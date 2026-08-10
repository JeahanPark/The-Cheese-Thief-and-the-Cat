using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    // SetupNetworkingScene의 일부 — 상자 프리팹과 배치 (v3.9)
    public static partial class SetupNetworkingScene
    {
        // 방 모서리에서 안쪽으로. 벽에 딱 붙이면 콜라이더가 벽을 파고든다.
        private const float BOX_CORNER_INSET = 1.1f;
        // 치즈 지점·벽감과 이만큼은 떨어뜨린다 — 붙어 있으면 한 자리에서 다 해결된다
        private const float BOX_MIN_OTHER_DIST = 3.0f;

        /// <summary>
        /// 부술 수 있는 상자. 벽감과 부품은 같지만 **역할이 다르다** —
        /// 무르고(HP 40) 소리가 거의 안 새서(8m) 찾은 사람이 조용히 먹는 탐색 보상이다.
        ///
        /// 드롭 프리팹 배열은 `LootId` 순서와 1:1이다. 새 물건이 늘면 여기 한 칸이 늘고
        /// `LootBox`는 안 바뀐다 — 상자는 안에 뭐가 들었는지 모른다.
        /// </summary>
        private static GameObject CreateLootBoxPrefab(
            GameObject _cheeseDropPrefab, GameObject _heroTicket, GameObject _gangTicket)
        {
            var root = new GameObject("LootBox");
            try
            {
                var balance = CTC.Runtime.Info.InfoManager.Instance.Balance;

                root.AddComponent<NetworkObject>();

                var health = root.AddComponent<Health>();
                SetInt(health, "m_MaxHp", balance.BoxHp);

                // 덮치기는 제 진영이 아닌 것을 때린다 — 고양이도 부술 수 있어야 하므로 Mouse.
                var roleTag = root.AddComponent<RoleTag>();
                SetInt(roleTag, "m_Role", (int)RoleId.Mouse);

                // 맵에 원래 있던 물건이라 부숴도 장난기가 안 나온다(벽감과 같은 이유)
                root.AddComponent<BountyExempt>();

                var feedback = root.AddComponent<HitFeedback>();
                SetInt(feedback, "m_OverrideSfx", 1);
                SetInt(feedback, "m_HitSfx", (int)CTC.Runtime.Audio.SfxId.BoxHit);
                SetInt(feedback, "m_BreakSfx", (int)CTC.Runtime.Audio.SfxId.BoxBreak);
                SetFloat(feedback, "m_SfxMaxDistance", balance.BoxBreakSoundMeters);
                SetFloat(feedback, "m_HitSfxMaxDistance", balance.BoxHitSoundMeters);

                var box = root.AddComponent<LootBox>();
                // LootId 순서와 1:1 — Cheese · HeroTicket · GangTicket
                SetRefArray(box, "m_DropPrefabs",
                    new Object[] { _cheeseDropPrefab, _heroTicket, _gangTicket });

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                body.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                body.GetComponent<Renderer>().sharedMaterial = GridboxMaterial("Orange");

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, LootBoxPrefabPath);
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
        /// 바닥에 떨어지는 직업권. 치즈 덩이와 같은 형식(트리거 구체)이라 몸을 막지 않는다 —
        /// 달려가 줍는 물건이다. 색만 다르게 둔다: 용사는 파랑, 깡패는 보라.
        /// </summary>
        private static GameObject CreateJobTicketPrefab(
            string _name, string _path, CTC.Runtime.Match.MouseJobId _job, string _color)
        {
            var root = new GameObject(_name);
            try
            {
                root.AddComponent<NetworkObject>();
                var ticket = root.AddComponent<JobTicketDrop>();
                SetInt(ticket, "m_Job", (int)_job);

                var trigger = root.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.6f;
                trigger.center = new Vector3(0f, 0.3f, 0f);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                body.transform.localScale = new Vector3(0.35f, 0.5f, 0.08f);   // 얇은 패 — 종이 느낌
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = GridboxMaterial(_color);

                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, _path);
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
        /// 상자 자리를 레이아웃에 덧쓴다. **방 모서리**에 놓는다 —
        /// 벽감이 벽 한가운데 노출돼 "다 같이 달려오는" 물건이라면, 상자는 구석에 있어
        /// 돌아다닌 사람만 찾는다. 규칙이 한 줄이라 레벨디자인이 바뀌어도 따라온다.
        ///
        /// 내용물은 지금 치즈 하나뿐이다(고정 방식) — 물건이 늘면 여기서 자리마다 다르게 준다.
        /// </summary>
        private static void PlanBoxesInto(MapLayout _layout, int _perRoom)
        {
            if (_layout == null || _perRoom <= 0) return;

            var placed = new List<BoxPlacement>();
            int amount = CTC.Runtime.Info.InfoManager.Instance.Balance.BoxCheese;

            foreach (RoomRect room in _layout.Rooms)
            {
                Vector2 half = room.Size * 0.5f - Vector2.one * BOX_CORNER_INSET;
                if (half.x <= 0f || half.y <= 0f) continue;

                var corners = new[]
                {
                    room.Center + new Vector2(+half.x, +half.y),
                    room.Center + new Vector2(-half.x, -half.y),
                    room.Center + new Vector2(+half.x, -half.y),
                    room.Center + new Vector2(-half.x, +half.y),
                };

                int inThisRoom = 0;
                foreach (Vector2 c in corners)
                {
                    if (inThisRoom >= _perRoom) break;
                    if (!IsClearOfCheese(c, _layout)) continue;
                    if (!IsClearOfAlcoves(c, _layout)) continue;

                    // 직업권은 **자리마다 정해 둔다**(고정 방식). 깡패권이 하나뿐인 이유:
                    // 둘이면 2단계에 보스가 둘이라 '보스 팀 vs 나머지'라는 구도가 깨진다.
                    LootId id = placed.Count == 0 ? LootId.GangTicket
                        : placed.Count == 1 ? LootId.HeroTicket
                        : LootId.Cheese;

                    placed.Add(new BoxPlacement
                    {
                        Pos = c,
                        YawDeg = 0f,
                        Contents = new List<LootEntry>
                        {
                            new LootEntry { Id = id, Amount = id == LootId.Cheese ? amount : 1 },
                        },
                    });
                    inThisRoom++;
                }
            }

            _layout.SetBoxes(placed);
            EditorUtility.SetDirty(_layout);
        }

        private static bool IsClearOfAlcoves(Vector2 _p, MapLayout _layout)
        {
            foreach (AlcovePlacement a in _layout.Alcoves)
            {
                if (Vector2.Distance(a.Pos, _p) < BOX_MIN_OTHER_DIST) return false;
            }
            return true;
        }
    }
}
