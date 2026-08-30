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

                // 나무 궤짝 메시로 갈아 끼운다(2026-08-30). **단위 크기(±0.5)로 구워** 두었기 때문에
                // 위 균등 스케일 0.7이 그대로 먹고 BoxCollider는 손도 안 닿는다.
                Mesh crateMesh = PropAssets.LoadMesh(PropAssets.CRATE_MESH);
                Material crateMat = PropAssets.LoadMaterial(PropAssets.CRATE_MATERIAL);
                if (crateMesh != null && crateMat != null)
                {
                    body.GetComponent<MeshFilter>().sharedMesh = crateMesh;
                    body.GetComponent<Renderer>().sharedMaterial = crateMat;
                }

                // 판이 시작할 때 서므로 구운 폴리곤에 없다 — 제 자리를 도려낸다
                root.AddComponent<CTC.Runtime.Map.NavMeshCarver>();
                // 격자 자국 — 벽감·상자는 **누구나** 부순다(CombatLogic의 MapLoot 예외)
                var gridBlocker = root.AddComponent<CTC.Runtime.Ai.NavGridBlocker>();
                SetInt(gridBlocker, "m_Kind", (int)CTC.Runtime.Ai.NavStructureKind.AnyBreakable);

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
            string _name, string _path, CTC.Runtime.Match.MouseJobId _job, string _color, string _materialPath)
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

                // 테두리와 끈 구멍이 있는 패로 갈아 끼운다(2026-08-30). 메시가 이미 실제 치수라
                // 스케일을 1로 되돌린다 — 여기엔 콜라이더가 없어(위에서 지웠다) 밸런스가 안 걸린다.
                Mesh ticketMesh = PropAssets.LoadMesh(PropAssets.TICKET_MESH);
                Material ticketMat = PropAssets.LoadMaterial(_materialPath);
                if (ticketMesh != null && ticketMat != null)
                {
                    body.GetComponent<MeshFilter>().sharedMesh = ticketMesh;
                    body.GetComponent<Renderer>().sharedMaterial = ticketMat;
                    body.transform.localScale = Vector3.one;
                }

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
        /// **맵 전체에 몇 개**다(2026-08-10, 그전엔 방마다 하나라 120×120에서 49개였다).
        /// 상자는 직업권이 나오는 곳이라 개수가 곧 희소성이다 — 방마다 있으면 뒤지는 게 일이 되고,
        /// 흔해지면 "찾았다"가 사건이 아니게 된다.
        ///
        /// 자리는 서로 멀리 흩는다. 앞에서부터 집으면 앞의 두 상자(깡패권·영웅권)가
        /// 붙어 있어 한 사람이 둘 다 먹는다.
        /// </summary>
        private static void PlanBoxesInto(MapLayout _layout, int _count)
        {
            if (_layout == null) return;

            // 0이어도 **비워야** 한다. 그냥 돌아가면 이전 배치가 레이아웃에 그대로 남는다
            // (2026-08-11 실측: 상자를 벽 뒤로 옮겼는데 바닥 상자 5개가 살아 있었다).
            if (_count <= 0)
            {
                _layout.SetBoxes(new List<BoxPlacement>());
                EditorUtility.SetDirty(_layout);
                return;
            }

            // 방마다 쓸 만한 모서리 하나씩 — 여기서 흩어 고른다
            var spots = new List<Vector2>();
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
                foreach (Vector2 c in corners)
                {
                    if (!IsClearOfCheese(c, _layout)) continue;
                    if (!IsClearOfAlcoves(c, _layout)) continue;
                    spots.Add(c);
                    break;
                }
            }

            var placed = new List<BoxPlacement>();
            int amount = CTC.Runtime.Info.InfoManager.Instance.Balance.BoxCheese;

            // **원점에서 가장 먼 자리부터** 흩는다. 첫 상자가 깡패권인데 중앙에서 시작하면
            // 그게 고양이 스폰(0,0) 코앞에 놓인다 — 쥐의 가장 큰 상품을 고양이 앞마당에 두는 꼴이다.
            int start = 0;
            float farthest = -1f;
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].sqrMagnitude <= farthest) continue;
                farthest = spots[i].sqrMagnitude;
                start = i;
            }

            foreach (int i in PlacementLogic.SpreadPick(spots, _count, start))
            {
                // 직업권은 **자리마다 정해 둔다**(고정 방식). 깡패권이 하나뿐인 이유:
                // 둘이면 2단계에 보스가 둘이라 '보스 팀 vs 나머지'라는 구도가 깨진다.
                LootId id = placed.Count == 0 ? LootId.GangTicket
                    : placed.Count == 1 ? LootId.HeroTicket
                    : LootId.Cheese;

                placed.Add(new BoxPlacement
                {
                    Pos = spots[i],
                    YawDeg = 0f,
                    Contents = new List<LootEntry>
                    {
                        new LootEntry { Id = id, Amount = id == LootId.Cheese ? amount : 1 },
                    },
                });
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

        private static bool IsClearOfCheese(Vector2 _p, MapLayout _layout)
        {
            foreach (CheesePlacement cp in _layout.Cheese)
            {
                if (Vector2.Distance(cp.Pos, _p) < BOX_MIN_OTHER_DIST) return false;
            }
            return true;
        }
    }
}
