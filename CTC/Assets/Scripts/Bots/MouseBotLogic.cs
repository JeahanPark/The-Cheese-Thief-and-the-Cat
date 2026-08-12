using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 쥐 봇이 **무엇을 할지** 고르는 규칙(순수). MonoBehaviour와 분리해 EditMode로 잰다.
    ///
    /// 봇이 잴 수 있는 건 "정직하게, 평범하게 플레이했을 때"의 밸런스다 —
    /// 그래서 판단은 **단순하고 설명 가능한 규칙**이어야 한다. 영리한 봇은 사람보다 잘해서
    /// 나온 승률이 아무 뜻도 없어진다.
    /// </summary>
    public static class MouseBotLogic
    {
        /// <summary>지점에 이만큼 붙으면 캐기 시작한다.</summary>
        public const float ARRIVE_DISTANCE = 1.2f;

        /// <summary>
        /// 지금 살 것. **싼 것부터, 목숨 지키는 것부터**다 — 사람도 그렇게 산다.
        /// 살 게 없으면 None.
        /// </summary>
        public static Economy.ShopItemId NextPurchase(
            int _cheese, bool _hasDash, bool _hasGun, bool _hasWarehouse, bool _hasFortress)
        {
            if (!_hasDash && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Dash).Cost)
                return Economy.ShopItemId.Dash;
            if (!_hasGun && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Slingshot).Cost)
                return Economy.ShopItemId.Slingshot;
            if (!_hasWarehouse && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Warehouse).Cost)
                return Economy.ShopItemId.Warehouse;
            if (!_hasFortress && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Fortress).Cost)
                return Economy.ShopItemId.Fortress;
            return Economy.ShopItemId.None;
        }

        /// <summary>
        /// 어느 치즈 지점으로 갈까. **가까운 순이 아니라 '등급 ÷ 거리'가 큰 순**이다 —
        /// 가까운 것만 고르면 봇 여덟이 스폰 근처에 뭉쳐 맵이 안 쓰이고,
        /// 등급만 보면 전원이 한가운데로 몰려 같은 자리를 두고 싸운다.
        ///
        /// 남이 쓰는 중이거나 마른 자리는 후보에서 뺀다 — 라벨에 그렇게 적혀 있어
        /// 사람도 가서 보면 안다.
        /// </summary>
        public static int ChooseSpot(
            IReadOnlyList<Vector3> _positions, IReadOnlyList<int> _grades,
            IReadOnlyList<bool> _usable, Vector3 _from)
        {
            if (_positions == null || _positions.Count == 0) return -1;

            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < _positions.Count; i++)
            {
                if (_usable != null && i < _usable.Count && !_usable[i]) continue;

                float distance = Mathf.Max(1f, Flat(_positions[i] - _from).magnitude);
                int grade = _grades != null && i < _grades.Count ? Mathf.Max(1, _grades[i]) : 1;
                float score = grade / distance;
                if (score <= bestScore) continue;

                bestScore = score;
                best = i;
            }
            return best;
        }

        /// <summary>위협에서 멀어지는 방향. 위협과 겹쳐 있으면 아무 쪽이나(영벡터 방지).</summary>
        public static Vector3 FleeDirection(Vector3 _self, Vector3 _threat)
        {
            Vector3 away = Flat(_self - _threat);
            return away.sqrMagnitude < 0.01f ? Vector3.forward : away.normalized;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
