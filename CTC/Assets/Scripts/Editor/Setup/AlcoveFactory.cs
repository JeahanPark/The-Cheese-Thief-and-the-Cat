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
        /// 문에서 이만큼은 떨어뜨린다 (2026-08-11 실측으로 얻었다).
        ///
        /// 격자 맵은 문이 **벽 한가운데**에 뚫린다. 그런데 벽감도 벽 한가운데에 앉혔더니
        /// 막다른 방의 **유일한 문을 벽감이 막았다** — 그 방에서 시작한 쥐가 벽감을 부수기
        /// 전에는 나올 수 없었고, 이동 그래프에서도 그 방이 통째로 잘려 나갔다.
        /// 복도 폭 3m + 벽감 폭을 덮는다.
        /// </summary>
        private const float ALCOVE_MIN_DOOR_DIST = 4f;

        // 문을 피해 벽을 따라 비껴 앉을 자리들(벽 길이 대비 비율). 0이 먼저 = 원래의 한가운데.
        private static readonly float[] ALCOVE_WALL_SLIDES = { 0f, 0.3f, -0.3f };
        // 요새가 이 여유 안에 있는 방에는 감옥을 두지 않는다 — 요새 자리가 곧 쥐 스폰이다
        private const float JAIL_FORTRESS_MARGIN = 1f;

        /// <summary>
        /// 벽감 프리팹. `CreateStructurePrefab`을 쓰지 않는 이유가 둘 있다 —
        /// ① 거기엔 `PlayerDeath`가 붙는데 그건 죽자마자 despawn한다. 벽감은
        ///    **despawn 전에 안의 것을 꺼내야** 하므로 순서를 남에게 맡길 수 없다.
        /// ② 설치음(`PlaceSfxOnSpawn`)이 붙는다. 벽감은 누가 설치하는 게 아니라 처음부터 맵에 있다.
        /// </summary>
        private static GameObject CreateAlcovePrefab(GameObject _lootBoxPrefab, GameObject _dogPrefab)
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
                // 타격음은 더 짧게(20m) — "부수는 중"과 "다 부서졌다"는 요구하는 행동이 다르다
                SetFloat(feedback, "m_HitSfxMaxDistance",
                    CTC.Runtime.Info.InfoManager.Instance.Balance.AlcoveHitSoundMeters);

                var alcove = root.AddComponent<Alcove>();
                SetRef(alcove, "m_LootBoxPrefab", _lootBoxPrefab);
                SetRef(alcove, "m_DogPrefab", _dogPrefab);

                // 벽이 흘리는 소리 — 강아지 벽만 낑낑거린다(치즈 벽은 조용). 안에 뭐가 있는지
                // 알 길이 이것뿐이라 벽감 스펙이 이 자리를 비워 뒀다.
                var idleSource = root.AddComponent<AudioSource>();
                idleSource.playOnAwake = false;
                idleSource.spatialBlend = 1f;
                idleSource.rolloffMode = AudioRolloffMode.Linear;
                idleSource.minDistance = 1.5f;
                idleSource.maxDistance = CTC.Runtime.Info.InfoManager.Instance.Balance.DogAlcoveIdleSoundMeters;
                root.AddComponent<AlcoveIdleSound>();
                // 루프 소스는 팔로워가 없으면 효과음 슬라이더가 이 소리에만 안 먹는다(2026-08-09 실측)
                AddVolumeFollower(root, CTC.Runtime.Audio.AudioVolumeFollower.Category.Sfx);

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
        /// 규칙: 방 하나에 하나씩, 벽면에 붙여서, 치즈 지점과는 떨어뜨려서, **맵 전체에 흩어서**.
        /// 복도에 두지 않는 이유는 부수는 19초 동안 도망칠 방향이 둘뿐이라 사형선고가 되기 때문.
        ///
        /// 흩는 게 왜 규칙인가: 방 목록 앞에서부터 집으면 생성기가 방을 만든 순서를 따라간다.
        /// 격자 맵은 그 순서가 한 줄씩이라 벽감 6개가 맨 아래 한 줄에 전부 몰렸다(2026-08-10 실측).
        /// 중앙에서 시작하는 건 강아지 벽감이 진짜 한가운데에 놓이게 하려는 것이다.
        /// </summary>
        private static void PlanAlcovesInto(MapLayout _layout, int _count)
        {
            if (_layout == null || _count <= 0) return;

            // 벽 자리가 나오는 방만 후보다 — 치즈와 겹치는 방은 애초에 제외된다
            var spots = new List<(Vector2 pos, float yaw)>();
            var anchors = new List<Vector2>();
            foreach (RoomRect room in _layout.Rooms)
            {
                if (!TryWallSpot(room, _layout, out Vector2 pos, out float yaw)) continue;
                spots.Add((pos, yaw));
                anchors.Add(room.Center);
            }

            var placed = new List<AlcovePlacement>();
            int center = PlacementLogic.NearestTo(anchors, Vector2.zero);
            foreach (int i in PlacementLogic.SpreadPick(anchors, _count, center))
            {
                placed.Add(new AlcovePlacement
                {
                    Pos = spots[i].pos,
                    YawDeg = spots[i].yaw,
                    Payload = AlcovePayload.Box,
                    BoxContents = new List<LootEntry>(),
                });
            }

            MarkDogAlcove(placed);
            FillBoxContents(placed);

            _layout.SetAlcoves(placed);
            EditorUtility.SetDirty(_layout);
        }

        /// <summary>
        /// 맵 중심에 **가장 가까운** 벽감 하나를 강아지 벽으로 바꾼다 (v3.8 5번).
        ///
        /// 중심을 고르는 이유: 양쪽이 다 지나는 자리라 랜드마크가 되고, 한쪽 진영 구석에 있으면
        /// 그쪽만 쓸 수 있는 카드가 되어 "지는 쪽의 무리수"라는 의도가 깨진다.
        /// 규칙이 한 줄이라 레벨디자인이 바뀌어도 따라온다.
        /// </summary>
        private static void MarkDogAlcove(List<AlcovePlacement> _placed)
        {
            if (_placed.Count == 0) return;

            int best = 0;
            float bestSqr = _placed[0].Pos.sqrMagnitude;
            for (int i = 1; i < _placed.Count; i++)
            {
                float sqr = _placed[i].Pos.sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }

            AlcovePlacement dog = _placed[best];
            dog.Payload = AlcovePayload.Dog;
            _placed[best] = dog;
        }

        /// <summary>
        /// 벽 뒤 상자에 무엇을 넣을지. **직업권은 벽 뒤에만 있다**(2026-08-11) —
        /// 바닥에 굴러다니면 조용히 주울 수 있어, 판을 뒤집는 물건이 아무 소리 없이 손에 들어간다.
        ///
        /// 깡패권과 영웅권은 **서로 가장 먼 두 벽**에 나눠 둔다. 나란히 두면 한 사람이 둘 다 먹는다.
        /// 강아지 벽은 건너뛴다 — 거긴 상자가 없다.
        /// </summary>
        private static void FillBoxContents(List<AlcovePlacement> _placed)
        {
            var boxes = new List<int>();
            for (int i = 0; i < _placed.Count; i++)
            {
                if (_placed[i].Payload == AlcovePayload.Box) boxes.Add(i);
            }
            if (boxes.Count == 0) return;

            // 서로 가장 먼 한 쌍을 직업권 자리로
            int a = boxes[0], b = boxes[boxes.Count > 1 ? 1 : 0];
            float best = -1f;
            foreach (int i in boxes)
            {
                foreach (int j in boxes)
                {
                    if (i >= j) continue;
                    float d = Vector2.Distance(_placed[i].Pos, _placed[j].Pos);
                    if (d <= best) continue;
                    best = d; a = i; b = j;
                }
            }

            int cheese = CTC.Runtime.Map.AlcoveLogic.CheesePerAlcove;
            foreach (int i in boxes)
            {
                LootId id = i == a ? LootId.GangTicket : i == b ? LootId.HeroTicket : LootId.Cheese;
                AlcovePlacement p = _placed[i];
                p.BoxContents = new List<LootEntry>
                {
                    new LootEntry { Id = id, Amount = id == LootId.Cheese ? cheese : 1 },
                };
                _placed[i] = p;
            }
        }

        // 방의 네 벽을 순서대로 시도한다. 무작위를 쓰지 않는 건 같은 레이아웃이면
        // 항상 같은 자리가 나와야 QA가 재현 가능하기 때문이다.
        private static bool TryWallSpot(RoomRect _room, MapLayout _layout, out Vector2 _pos, out float _yaw)
        {
            Vector2 c = _room.Center;
            Vector2 half = _room.Size * 0.5f;

            // 벽마다 한가운데를 먼저 보고, 문에 걸리면 벽을 따라 좌우로 비껴 앉는다.
            foreach (float slide in ALCOVE_WALL_SLIDES)
            {
                float dx = _room.Size.x * slide;
                float dy = _room.Size.y * slide;

                // (자리, 방 안쪽을 보는 yaw) — 북·남·동·서
                var candidates = new (Vector2 pos, float yaw)[]
                {
                    (new Vector2(c.x + dx, c.y + half.y - ALCOVE_WALL_INSET), 180f),
                    (new Vector2(c.x + dx, c.y - half.y + ALCOVE_WALL_INSET), 0f),
                    (new Vector2(c.x + half.x - ALCOVE_WALL_INSET, c.y + dy), 270f),
                    (new Vector2(c.x - half.x + ALCOVE_WALL_INSET, c.y + dy), 90f),
                };

                foreach ((Vector2 pos, float yaw) in candidates)
                {
                    if (!IsClearOfCheese(pos, _layout)) continue;
                    if (!IsClearOfDoors(pos, _layout)) continue;
                    _pos = pos;
                    _yaw = yaw;
                    return true;
                }
            }

            _pos = default;
            _yaw = 0f;
            return false;
        }

        // 문은 복도 끝점이다 — 거기에 벽감을 앉히면 그 방이 잠긴다.
        private static bool IsClearOfDoors(Vector2 _pos, MapLayout _layout)
        {
            foreach (CorridorSeg seg in _layout.Corridors)
            {
                if (Vector2.Distance(seg.From, _pos) < ALCOVE_MIN_DOOR_DIST) return false;
                if (Vector2.Distance(seg.To, _pos) < ALCOVE_MIN_DOOR_DIST) return false;
            }
            return true;
        }

        /// <summary>
        /// 감옥 자리를 레이아웃에 덧쓴다. 서로 **멀리 떨어진** 방들의 중심을 고른다 —
        /// 붙어 있으면 고양이가 둘 다 지킬 수 있어 "어디를 지킬 것인가"라는 판단이 사라진다.
        ///
        /// **요새가 있는 방은 뺀다** — 단, 뺄 여유가 있을 때만. 요새 자리가 곧 쥐 스폰이라
        /// 겹치면 감옥이 스폰 지점에 서고, 그 앞을 지키는 고양이가 스폰까지 같이 지킨다
        /// (2026-08-10 실측: 120×120 맵에서 감옥 3개가 전부 요새 위였다).
        ///
        /// 여유를 따지는 이유: 기본 맵은 방 6개 중 4개가 요새 방이라 무조건 빼면 후보가 둘만 남고,
        /// 감옥 둘이 10m 거리에 붙어 "어디를 지킬 것인가"가 사라진다 — 겹치는 것보다 그게 더 나쁘다.
        /// </summary>
        private static void PlanJailsInto(MapLayout _layout, int _count)
        {
            if (_layout == null || _count <= 0 || _layout.Rooms.Count == 0) return;

            var fortresses = new List<Vector2>();
            foreach (FortressPlacement f in _layout.Fortresses) fortresses.Add(f.Pos);

            var centers = new List<Vector2>();
            foreach (RoomRect r in _layout.Rooms)
            {
                if (PlacementLogic.RoomHoldsAny(r, fortresses, JAIL_FORTRESS_MARGIN)) continue;
                centers.Add(r.Center);
            }
            // 고를 자리가 감옥 수의 두 배는 돼야 '흩는다'는 말이 성립한다
            if (centers.Count < _count * 2)
            {
                centers.Clear();
                foreach (RoomRect r in _layout.Rooms) centers.Add(r.Center);
            }

            // 원점에서 가장 먼 방부터 — 감옥은 구석에 있어야 "데리러 가는 거리"가 값을 갖는다
            int start = 0;
            float farthest = -1f;
            for (int i = 0; i < centers.Count; i++)
            {
                float d = centers[i].sqrMagnitude;
                if (d <= farthest) continue;
                farthest = d; start = i;
            }

            var placed = new List<JailPlacement>();
            foreach (int i in PlacementLogic.SpreadPick(centers, _count, start))
            {
                placed.Add(new JailPlacement { Pos = centers[i] });
            }

            _layout.SetJails(placed);
            EditorUtility.SetDirty(_layout);
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
