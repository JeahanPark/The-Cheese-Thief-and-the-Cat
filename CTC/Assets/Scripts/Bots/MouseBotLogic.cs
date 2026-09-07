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
        /// 소리 위협에 도망으로 반응하는 반경 — 쥐가 고양이 발소리를 듣는 거리(12m)와 같다.
        /// 덮치기·스킬 소리는 45m를 가는데, 그때마다 도망치면 온 맵의 쥐가 상시 잠행 도주에
        /// 빠져 파밍이 통째로 선다(2026-08-12 실측: 최대 치즈 634→400 · 평균 이속 1.0m/s).
        /// 먼 소리는 정보일 뿐 도망 사유가 아니다 — 사람도 멀리서 난 소리에 뛰지 않는다.
        /// </summary>
        public const float SOUND_THREAT_RADIUS = 12f;

        /// <summary>이 소리에 도망칠까 — 보이는 위협은 거리와 무관하게 항상 도망이다.</summary>
        public static bool IsSoundThreat(float _distanceToSound)
            => _distanceToSound <= SOUND_THREAT_RADIUS;

        /// <summary>도망 목적지로 요새를 고려하는 반경 — 이보다 멀면 뛰어가다 잡힌다.</summary>
        public const float FORT_REFUGE_RADIUS = 20f;

        /// <summary>
        /// 고양이는 1.3배 빠르다 — 요새 진입 경주에서 이기려면 내가 이 배율만큼
        /// 유리해야 한다(추가 여유는 덮치기 사거리 몫).
        /// </summary>
        public const float REFUGE_RACE_RATIO = 1.3f;
        public const float REFUGE_RACE_MARGIN = 3f;

        /// <summary>
        /// 도망칠 때 들어갈 요새 인덱스. 없으면 -1.
        ///
        /// 조건 둘 — ① 요새가 가깝고(FORT_REFUGE_RADIUS) ② **진입 경주에서 이긴다**
        /// (위협의 요새 거리 > 내 거리 × 1.3 + 여유). 위협 뒤편의 요새로 뛰는 건
        /// 자살이라, 이 경주 계산이 없으면 피난 행동이 봇을 더 빨리 죽인다.
        /// 사람도 같은 판단을 한다("집이 가깝고 고양이가 저쪽에 있으니 들어간다").
        /// </summary>
        /// <summary>
        /// 이 비율 아래로 깎인 요새는 은신처로 안 친다 — **곧 뚫린다**(2026-08-18).
        /// 고양이가 벽을 부수고 들어올 수 있게 된 뒤로 요새는 "안전지대"가 아니라 "버는 시간"이다.
        /// 이미 맞고 있는 벽 뒤로 뛰어드는 건 도망이 아니라 자살이다.
        /// </summary>
        public const float REFUGE_MIN_HP_RATIO = 0.5f;

        /// <summary>맞고 있는 요새인가 — 이 안에 있었다면 반대쪽 문으로 나가야 한다.</summary>
        public static bool IsRefugeSpent(int _hp, int _maxHp) =>
            _maxHp > 0 && _hp < _maxHp * REFUGE_MIN_HP_RATIO;

        public static int ChooseRefuge(
            IReadOnlyList<Vector3> _forts, Vector3 _self, Vector3 _threat)
        {
            int best = -1;
            float bestDist = FORT_REFUGE_RADIUS;
            for (int i = 0; i < _forts.Count; i++)
            {
                float mine = Flat(_forts[i] - _self).magnitude;
                if (mine > bestDist) continue;

                float theirs = Flat(_forts[i] - _threat).magnitude;
                if (theirs <= mine * REFUGE_RACE_RATIO + REFUGE_RACE_MARGIN) continue;

                best = i;
                bestDist = mine;
            }
            return best;
        }

        /// <summary>
        /// 지금 살 것. 살 게 없으면 None.
        ///
        /// **수급·성장 빌드다**(2026-08-12). 도망칠 수단(대시)을 먼저 쥐고, 그다음은
        /// **생존(요새) → 수입(창고)**(2026-08-18에 뒤집었다). 도주 중 요새는 여기와 별개로
        /// 즉석에서 산다(BotShopper.BuyFortressShield).
        ///
        /// **새총은 안 산다**(2026-08-14). 봇은 맞서 쏘지 않으므로(그랬더니 쥐 여덟이
        /// 도망치며 쏴 97초 만에 고양이 둘을 죽였다 — 실측) 안 쓸 총에 치즈를 태우면
        /// 그만큼 미끼를 못 사서 경제 측정이 왜곡된다.
        /// </summary>
        public static Economy.TechId NextPurchase(
            int _cheese, bool _hasDash, bool _hasGun, bool _hasWarehouse, bool _hasFortress)
        {
            Info.BalanceInfo balance = Info.InfoManager.Instance.Balance;
            int CostOf(Economy.TechId _id) => Economy.TechLogic.RowOf(_id, 1).Cost;

            // ① 대시 — 눈앞에서 쫓길 때 거리를 벌리는 유일한 수단
            if (!_hasDash && _cheese >= CostOf(Economy.TechId.Dash)) return Economy.TechId.Dash;

            // ② 요새 — **창고보다 먼저다**(2026-08-18). 예전에는 창고(150) → 요새(80) 순이라
            //    첫 요새를 세우려면 250을 모아야 했고, 그때까지 쥐는 도망칠 벽이 없었다.
            //
            //    **미리 세우는 게 맞다.** "쫓길 때만 사면 되지 않나" 싶어 빼 봤더니 승률이 17%→0%로
            //    떨어졌다(측정). 서 있는 요새는 `TryGetRefugeFortress`가 찾아 주는 **피난처**라,
            //    쫓기기 전에 세워 둬야 도망칠 곳이 생긴다. 쫓기는 순간의 발밑 설치는 그것대로 남아 있다.
            //    테크 v3에선 Lv1(설치 "권한")만 사면 되고, 실제 설치는 매번 FortressPlaceCost를 낸다.
            if (!_hasFortress && _cheese >= CostOf(Economy.TechId.Fortress)) return Economy.TechId.Fortress;

            // ③ 창고 — **비상 벽 값(요새 설치비)뿐 아니라 창고 자체 설치비도 남기고 산다**(Task 8 갱신,
            //    2026-09-07). 테크 v3부터 창고도 Lv1(권한) + 설치마다 WarehousePlaceCost를 따로 낸다 —
            //    Lv1만 사고 설치비가 없으면 전부 털어 창고를 세운 것과 같아 그 직후 쫓길 때
            //    `PlaceFortressForFlee`가 빈 지갑을 보고 아무것도 못 한다.
            if (!_hasWarehouse
                && _cheese >= CostOf(Economy.TechId.Warehouse) + balance.WarehousePlaceCost + balance.FortressPlaceCost)
                return Economy.TechId.Warehouse;

            return Economy.TechId.None;
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
