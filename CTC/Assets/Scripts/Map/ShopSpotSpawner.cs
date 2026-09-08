using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 판 시작에 상점 자리를 세우는 Worker (테크 v3). `LootBoxSpawner`와 같은 취급 —
    /// NetworkObject라 서버가 스폰한다. 자리는 <see cref="ShopSpotLogic.Pick"/>.
    /// </summary>
    public class ShopSpotSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject m_SpotPrefab;

        private readonly List<NetworkObject> m_Spawned = new List<NetworkObject>();

        /// <summary>서버 전용. 이전 판 것을 걷어내고 새로 세운다.</summary>
        public void ServerSpawnAll(MapLayout _layout)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            ServerDespawnAll();
            if (_layout == null || m_SpotPrefab == null) return;

            var placed = new List<Vector2>(_layout.Shops.Count);
            foreach (ShopPlacement shop in _layout.Shops) placed.Add(shop.Pos);

            // 손 배치 맵은 레벨디자이너 후보 중에서, 절차 맵은 방 중심 규칙 — ShopSpotLogic.Resolve.
            // 방 **중심만**이 아니라 방 사각형째로 넘긴다: 원반이 들어가는 방을 골라야
            // 상점 안에서 설치가 막히지 않는다(2026-09-08).
            Info.BalanceInfo b = Info.InfoManager.Instance.Balance;
            int count = ShopSpotLogic.CountFor(_layout.HalfExtent, b.ShopSpotCount,
                b.ShopSpotCountSmall, b.ShopSpotLargeHalfExtent);

            // 판마다 다른 자리를 만드는 것은 **매치 시드**다(계획 3). 시드가 없으면(로비·봇 실험실)
            // 맵 시드로 후퇴한다 — 판마다 같지만 서기는 한다.
            Match.MatchRandom random = Match.MatchRandom.Current;
            int seed = random != null && random.Seed != 0
                ? Match.MatchRandom.SubSeed(random.Seed, Match.MatchRandom.SALT_SHOP)
                : _layout.Seed;

            // 원반 안이 실제로 비었는지까지 본다(2026-09-08) — 크기만 보던 시절엔 엄폐물을 문 자리에
            // 상점이 서서 요새·창고가 통째로 거절됐다. 고양이 스폰을 삼키는 자리도 여기서 빠진다.
            ShopSpotLogic.SpotCheck check = BuildCheck();

            List<Vector2> picked = ShopSpotLogic.Resolve(_layout.HandPlaced, placed, _layout.Rooms,
                count, b.ShopSpotMinGap, b.ShopSpotRadius, seed, check);

            foreach (Vector2 p in picked)
            {
                GameObject go = Instantiate(m_SpotPrefab, new Vector3(p.x, 0f, p.y), Quaternion.identity);
                var no = go.GetComponent<NetworkObject>();
                no.Spawn(true);
                m_Spawned.Add(no);
            }
            Core.GameDebug.Log($"[ShopSpotSpawner] 상점 {picked.Count}곳 (목표 {count} · 간격 {b.ShopSpotMinGap:0.#} · 시드 {seed})");
            foreach (Vector2 p in picked)
            {
                Core.GameDebug.Log($"[ShopSpotSpawner]   ({p.x:0.##},{p.y:0.##}) 열린 자리 " +
                    $"{ShopSpotLogic.FreeProbeCount(p, b.ShopSpotRadius, check)}/{ShopSpotLogic.DISC_PROBE_RING + 1}");
            }
        }

        /// <summary>
        /// 원반 검사를 짓는다 — **차단물은 격자(<c>NavGridService.Grid</c>)에게 묻는다.**
        ///
        /// 격자를 고른 이유: 절차 맵과 손 배치 맵을 **한 벌로** 덮는 유일한 원본이다.
        /// `MapPointAudit`은 `MapLayout.Props`만 알아 절차 맵이 세우는 방 벽·복도·엄폐물을 못 보고,
        /// `ShopController.PlacementFits`는 물리 질의라 순수 로직으로 못 뽑아 EditMode가 못 덮는다.
        /// 격자는 `MapBuilder`가 **실제로 세운 콜라이더**에서 구워지므로 둘의 장점만 남는다.
        ///
        /// 격자가 아직 없으면(맵 미빌드) 검사를 끄고 예전처럼 크기만 본다 — 상점이 0곳이 되는 것보다 낫다.
        /// </summary>
        private static ShopSpotLogic.SpotCheck BuildCheck()
        {
            // 설치물 크기의 주인은 ShopController다 — 여기서 숫자를 베끼면 요새를 키웠을 때 조용히 갈라진다.
            float placeHalf = Economy.ShopController.HalfExtentsOf(Economy.TechId.Fortress).x;

            var check = new ShopSpotLogic.SpotCheck
            {
                PlaceHalfExtent = placeHalf,
                KeepAway = Lobby.PlayerSpawner.CatSpawnPoints(),
                KeepAwayMargin = Info.InfoManager.Instance.PlayerOf(Lobby.RoleId.Cat).CcRadius
                                 + CAT_SPAWN_MARGIN,
            };

            Ai.NavGrid grid = Ai.NavGridService.Grid;
            if (grid == null) return check;

            check.IsFree = _p =>
            {
                grid.ToCell(new Vector3(_p.x, 0f, _p.y), out int cx, out int cz);
                if (!grid.InBounds(cx, cz)) return false;
                // 막힌 셀은 ClearanceMeters가 0이라 따로 안 본다 — 요새 반치수가 들어가야 열린 자리다.
                return grid.ClearanceMeters(cx, cz) >= placeHalf;
            };
            return check;
        }

        /// <summary>고양이 몸 반지름 위에 더 띄우는 여유(m) — 원반 가장자리에 걸치는 것도 막는다.</summary>
        private const float CAT_SPAWN_MARGIN = 1f;

        /// <summary>서버 전용. 로비 복귀 시 걷는다.</summary>
        public void ServerDespawnAll()
        {
            foreach (NetworkObject no in m_Spawned)
            {
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
            m_Spawned.Clear();
        }
    }
}
