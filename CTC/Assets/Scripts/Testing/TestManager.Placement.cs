// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using CTC.Runtime.Economy;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// TestManager의 일부 — **설치가 왜 거절되는가**를 캐는 훅(2026-09-08).
    ///
    /// <see cref="ShopController.PlacementFits"/>는 bool 하나만 돌려준다. 안 보이는 콜라이더가
    /// 조용히 막으면 화면만 보고는 절대 못 밝힌다 — 실제로 「상점 자리 안에서 창고가 안 선다」를
    /// 이걸 만들기 전에는 하루 동안 못 짚었다(<see cref="TestDebugPounceTargets"/>와 같은 이유).
    /// 그래서 이건 임시가 아니라 **남기는 훅**이다.
    /// </summary>
    public partial class TestManager
    {
        private static readonly Collider[] s_DebugOverlap = new Collider[ShopController.PLACE_OVERLAP_BUFFER];

        /// <summary>
        /// QA: 그 지점에서 설치를 막는 것의 정체 — 이름·레이어·타입·루트를 그대로 뱉는다.
        ///
        /// 질의는 <see cref="ShopController.PlacementOverlap"/> 하나를 같이 쓴다. 여기서 식을
        /// 베끼면 훅이 "비었다"고 하는데 서버는 거절하는, 가장 나쁜 종류의 거짓말이 된다.
        /// 형식: <c>fits=False count=2 | Stand(L0,MeshCollider,root=ShopSpot) | ...</c>
        /// </summary>
        public string TestDebugPlacementBlockers(float _x, float _z, string _techId)
        {
            if (!System.Enum.TryParse(_techId, out TechId id)) return $"모르는 테크: {_techId}";

            var at = new Vector3(_x, 0f, _z);
            int count = ShopController.PlacementOverlap(id, at, s_DebugOverlap);

            var sb = new StringBuilder();
            sb.Append($"fits={ShopController.PlacementFits(id, at)} count={count}");
            for (int i = 0; i < count; i++)
            {
                Collider col = s_DebugOverlap[i];
                if (col == null) continue;
                Transform root = col.transform.root;
                sb.Append($" | {col.gameObject.name}(L{col.gameObject.layer},{col.GetType().Name}")
                  .Append($",trigger={col.isTrigger},root={root.name}")
                  .Append($",blocks={ShopController.IsPlacementBlocker(col)})");
            }
            return sb.ToString();
        }

        /// <summary>QA: 상점 자리 좌표 "x,z"(없는 번호면 빈 문자열) — 진단 훅을 찍을 자리를 준다.</summary>
        public string TestGetShopSpotPos(int _index)
        {
            System.Collections.Generic.IReadOnlyList<ShopSpot> all = ShopSpot.All;
            if (_index < 0 || _index >= all.Count) return "";
            Vector3 p = all[_index].transform.position;
            return $"{p.x:F2},{p.z:F2}";
        }

        /// <summary>QA: 상점 자리 반경(m) — 스크립트가 CSV 값을 손으로 적지 않게 한다.</summary>
        public float TestGetShopSpotRadius()
        {
            return Info.InfoManager.Instance.Balance.ShopSpotRadius;
        }

        /// <summary>
        /// QA: 지금 선 상점마다 **원반 안에서 요새가 서는 자리 수**를 "3|9|8|..."로(중심+둘레 여덟 중).
        ///
        /// 자리 수를 세는 건 <see cref="ShopSpotLogic.FreeProbeCount"/> 하나다 — 자리를 고른
        /// 규칙과 QA가 재는 규칙이 같아야 "훅은 통과라는데 게임에선 안 선다"가 안 생긴다.
        /// **물리(<see cref="ShopController.PlacementFits"/>)가 아니라 격자**를 본다: 고르는 쪽이
        /// 격자를 봤으므로 같은 눈으로 재야 한다. 물리 쪽 확인은 <see cref="TestDebugPlacementBlockers"/>가 한다.
        /// </summary>
        public string TestGetShopSpotFreeSpots()
        {
            System.Collections.Generic.IReadOnlyList<ShopSpot> all = ShopSpot.All;
            float radius = Info.InfoManager.Instance.Balance.ShopSpotRadius;
            float placeHalf = ShopController.HalfExtentsOf(TechId.Fortress).x;
            Ai.NavGrid grid = Ai.NavGridService.Grid;

            var check = new ShopSpotLogic.SpotCheck { PlaceHalfExtent = placeHalf };
            if (grid != null)
            {
                check.IsFree = _p =>
                {
                    grid.ToCell(new Vector3(_p.x, 0f, _p.y), out int cx, out int cz);
                    return grid.InBounds(cx, cz) && grid.ClearanceMeters(cx, cz) >= placeHalf;
                };
            }

            var sb = new StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;   // 규칙 9
                Vector3 p = all[i].transform.position;
                if (sb.Length > 0) sb.Append('|');
                sb.Append(ShopSpotLogic.FreeProbeCount(new Vector2(p.x, p.z), radius, check));
            }
            return sb.ToString();
        }

        /// <summary>
        /// QA: 상점 원반이 고양이 스폰을 삼켰는가 — 삼킨 상점 좌표를 "x,z|x,z"로, 없으면 빈 문자열.
        /// 판정은 <see cref="ShopSpotLogic.SwallowsKeepAway"/> 하나를 자리 고르는 쪽과 같이 쓴다.
        /// </summary>
        public string TestGetShopSpotsOnCatSpawn()
        {
            System.Collections.Generic.IReadOnlyList<ShopSpot> all = ShopSpot.All;
            float radius = Info.InfoManager.Instance.Balance.ShopSpotRadius;
            var check = new ShopSpotLogic.SpotCheck
            {
                KeepAway = Lobby.PlayerSpawner.CatSpawnPoints(),
                KeepAwayMargin = Info.InfoManager.Instance.PlayerOf(Lobby.RoleId.Cat).CcRadius,
            };

            var sb = new StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                Vector3 p = all[i].transform.position;
                if (!ShopSpotLogic.SwallowsKeepAway(new Vector2(p.x, p.z), radius, check)) continue;
                if (sb.Length > 0) sb.Append('|');
                sb.Append($"{p.x:F2},{p.z:F2}");
            }
            return sb.ToString();
        }
    }
}
#endif
