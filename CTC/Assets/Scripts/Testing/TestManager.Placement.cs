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
    }
}
#endif
