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
        /// 감옥에 이만큼 가까우면 잠행으로 접근한다 — 뛰는 발소리(14m)가 간수를 부른다.
        /// 멀리서는 뛴다: 체류 30게임초 안에 이동+접근+8초 버티기를 다 해야 해서,
        /// 20m부터 잠행(2.7m/s)했더니 접근에만 7초가 녹아 구출이 시간을 못 맞췄다(실측).
        /// </summary>
        public const float RESCUE_SNEAK_RADIUS = 10f;

        /// <summary>
        /// 보이는 고양이가 이 안이면 구출을 접는다. **거리 없이 접으면** 간수가 감옥에
        /// 서 있기만 해도 온 맵의 구출이 죽는다(실측: 접근 중 원거리 간수 목격 → 전원 Farm 복귀).
        /// 이 밖의 고양이는 무시하고 접근한다 — 다가오면 그때 도주 가지가 받는다.
        /// </summary>
        public const float RESCUE_ABORT_RADIUS = 12f;

        /// <summary>구출 접근을 잠행으로 할까 — 감옥까지의 거리로 정한다.</summary>
        public static bool ShouldSneakForRescue(float _distanceToJail)
            => _distanceToJail <= RESCUE_SNEAK_RADIUS;

        /// <summary>
        /// 소리 위협에 도망으로 반응하는 반경 — 쥐가 고양이 발소리를 듣는 거리(12m)와 같다.
        /// 덮치기·스킬 소리는 45m를 가는데, 그때마다 도망치면 온 맵의 쥐가 상시 잠행 도주에
        /// 빠져 파밍이 통째로 선다(2026-08-12 실측: 최대 치즈 634→400 · 평균 이속 1.0m/s).
        /// 먼 소리는 정보일 뿐 도망 사유가 아니다 — 사람도 멀리서 난 소리에 뛰지 않는다.
        /// </summary>
        public const float SOUND_THREAT_RADIUS = 12f;

        /// <summary>이 소리에 도망칠까 — 보이는 위협은 거리와 무관하게 항상 도망이다.</summary>
        public static bool IsSoundThreat(float _distanceToSound)
            => _distanceToSound <= SOUND_THREAT_RADIUS;

        /// <summary>
        /// 지금 살 것. 살 게 없으면 None.
        ///
        /// **수급·성장 빌드다**(2026-08-12). 도망칠 수단(대시)을 먼저 쥐고, 그다음은
        /// **수입(창고) → 생존(요새)**이고, 싸울 것(새총)은 맨 뒤다.
        ///
        /// 그전에는 새총이 두 번째였다. 그랬더니 쥐 여덟이 도망치며 쉬지 않고 쏴서
        /// **97초 만에 고양이 둘이 죽었다**(실측). 사람이 "벌어서 큰다"를 목표로 할 때
        /// 하는 행동이 아니다 — 그런 봇으로 잰 승률은 밸런스가 아니라 봇의 화력을 잰 것이다.
        /// </summary>
        public static Economy.ShopItemId NextPurchase(
            int _cheese, bool _hasDash, bool _hasGun, bool _hasWarehouse, bool _hasFortress)
        {
            if (!_hasDash && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Dash).Cost)
                return Economy.ShopItemId.Dash;
            if (!_hasWarehouse && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Warehouse).Cost)
                return Economy.ShopItemId.Warehouse;
            if (!_hasFortress && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Fortress).Cost)
                return Economy.ShopItemId.Fortress;
            if (!_hasGun && _cheese >= Economy.ShopLogic.Find(Economy.ShopItemId.Slingshot).Cost)
                return Economy.ShopItemId.Slingshot;
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

        // 덜 꺾을수록 위협에서 멀어진다 — 정반대를 먼저, 그다음 좌우로.
        private static readonly float[] FLEE_ANGLES = { 0f, 45f, -45f, 90f, -90f };

        /// <summary>
        /// 도망칠 목표 지점. 위협 반대 방향을 기본으로, 좌우로 틀어서라도
        /// **위협에게 안 보이는 자리**를 고른다 — 직선 도주는 더 빠른 고양이(7.8 vs 6.0)에게
        /// 항상 진다(2026-08-12 실측: 첫 발견 후 2게임분 안에 8마리 전멸).
        /// 문 차폐벽·기둥 뒤가 이 후보에 걸린다. 전부 보이면 정반대(거리라도 번다).
        /// </summary>
        public static Vector3 FleeGoal(
            Vector3 _self, Vector3 _threat, float _distance,
            System.Func<Vector3, Vector3, bool> _isHidden)
        {
            Vector3 away = FleeDirection(_self, _threat);
            if (_isHidden != null)
            {
                foreach (float deg in FLEE_ANGLES)
                {
                    Vector3 goal = _self + Quaternion.Euler(0f, deg, 0f) * away * _distance;
                    if (_isHidden(_threat, goal)) return goal;
                }
            }
            return _self + away * _distance;
        }

        private static Vector3 Flat(Vector3 _v)
        {
            _v.y = 0f;
            return _v;
        }
    }
}
