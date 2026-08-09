using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점 수입 규칙(원작 약올리기 돈줄 대응). MonoBehaviour와 분리된
    /// 순수 함수라 EditMode 테스트가 가능하다.
    /// - 등급(1~7) = 초당 수입 (임시 밸런스 — 튜닝은 스펙 §12)
    /// - 지점당 동시 수입 1마리: 선점자가 남아 있는 한 유지
    /// </summary>
    public static class CheeseSpotLogic
    {
        public const ulong NO_EARNER = ulong.MaxValue;
        public const int MIN_GRADE = 1;
        public const int MAX_GRADE = 7;

        // ── 고갈(v3.4) ──
        // 지점마다 매장량이 있고 다 캐면 사라진다. 넓은 맵에서 양쪽이 각자 구석에 눌러앉아
        // 판이 끝날 때까지 안 만나는 것을 구조적으로 막는 장치 — 후반엔 남은 자리로 몰릴 수밖에 없다.
        //
        // 매장량을 등급에 비례시키지 않고 '균일'로 두는 게 핵심이다. 그래야 고등급일수록
        // 빨리 말라(7등급 100초 / 1등급 700초) 좋은 자리부터 사라지고, 플레이어가 점점
        // 나쁜 자리로 밀려나며 자연스럽게 압축된다. 비례로 주면 전부 동시에 말라 압축이 안 생긴다.
        //
        // 총량을 고정하고 지점 수로 나눈다 — 맵이 넓어져 지점이 늘어도 고갈 시점이 유지된다.
        // 수치 원본은 CSV(BalanceInfo.csv) — 2026-07-31 데이터 분리
        public static int TOTAL_MAP_CHEESE => Info.InfoManager.Instance.Balance.TotalMapCheese;

        /// <summary>
        /// 지점 하나가 가질 매장량. 벽감이 없으면 7지점에 700(기준 맵), 10지점에 490.
        ///
        /// <paramref name="_alcoveCheeseTotal"/>은 벽감이 **총량에서 먼저 떼가는** 몫이다
        /// (2026-08-09). 더하지 않고 빼는 이유: 총량을 지점 수로 나누는 이 구조가
        /// 고갈 시점 = 성장 기한을 고정하는데, 벽 치즈를 얹으면 성장이 늘어져 후반 압축이 무너진다.
        /// 인자를 기본값 없이 받는 건 호출부가 "벽 몫을 뺐는가"를 반드시 답하게 하려는 것이다.
        /// </summary>
        public static int ReservePerSpot(int _spotCount, int _alcoveCheeseTotal)
        {
            if (_spotCount <= 0) return 0;

            // 배치 실수로 벽이 총량보다 많이 가져가도 지점 매장량이 음수가 되면 안 된다
            int forSpots = Mathf.Max(0, TOTAL_MAP_CHEESE - Mathf.Max(0, _alcoveCheeseTotal));
            return forSpots / _spotCount;
        }

        /// <summary>마지막 부활 지점의 매장량 — 뒤처진 쥐의 만회 기회이자 후반 결전지.</summary>
        public static int LAST_STAND_RESERVE => Info.InfoManager.Instance.Balance.LastStandReserve;

        /// <summary>
        /// 매장량에서 실제로 캘 수 있는 양. 바닥나면 요청량보다 적게 나온다.
        /// </summary>
        public static int Extract(int _remaining, int _requested)
        {
            if (_remaining <= 0 || _requested <= 0) return 0;
            return Mathf.Min(_remaining, _requested);
        }

        /// <summary>
        /// 맵 전체 매장량 — 지점 수와 무관하게 고정이다(나눗셈 나머지만큼만 줄어든다).
        /// 쥐 전원이 최종 테크까지 사고도 남을 만큼이어야 고갈이 '기한'으로 작동한다.
        /// </summary>
        public static int TotalReserve(int _spotCount, int _alcoveCheeseTotal)
        {
            return Mathf.Max(0, _spotCount) * ReservePerSpot(_spotCount, _alcoveCheeseTotal);
        }

        public static int IncomePerSecond(int _grade)
        {
            return Mathf.Clamp(_grade, MIN_GRADE, MAX_GRADE);
        }

        /// <summary>점유 상태 — 지점 라벨 색·문구를 정한다(로컬 플레이어 기준).</summary>
        public enum Occupancy
        {
            Free = 0,
            Mine = 1,
            Others = 2,
        }

        public static Occupancy OccupancyOf(ulong _earner, ulong _localClientId)
        {
            if (_earner == NO_EARNER) return Occupancy.Free;
            return _earner == _localClientId ? Occupancy.Mine : Occupancy.Others;
        }

        /// <summary>
        /// 진입 순서 목록을 갱신한다 — 나간 사람은 빼고, 새로 온 사람은 뒤에 붙인다.
        /// SelectEarner가 목록의 첫 후보를 고르므로 이 순서가 곧 '먼저 온 사람이 먼저'가 된다.
        /// (예전엔 물리 검사 순서라 동시 진입 시 누가 되는지 임의였다)
        /// </summary>
        public static void UpdateEntryOrder(List<ulong> _order, IReadOnlyList<ulong> _present)
        {
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                if (!Contains(_present, _order[i])) _order.RemoveAt(i);
            }
            for (int i = 0; i < _present.Count; i++)
            {
                if (!_order.Contains(_present[i])) _order.Add(_present[i]);
            }
        }

        private static bool Contains(IReadOnlyList<ulong> _list, ulong _value)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i] == _value) return true;
            }
            return false;
        }

        /// <summary>현 선점자가 반경 내에 남아 있으면 유지, 아니면 목록의 첫 후보.</summary>
        public static ulong SelectEarner(ulong _currentEarner, IReadOnlyList<ulong> _present)
        {
            if (_present == null || _present.Count == 0) return NO_EARNER;

            if (_currentEarner != NO_EARNER)
            {
                for (int i = 0; i < _present.Count; i++)
                {
                    if (_present[i] == _currentEarner) return _currentEarner;
                }
            }
            return _present[0];
        }

        /// <summary>소수 누적 수입 → 정수 치즈 지급. 반환: 새 누적 잔여, out: 지급할 정수량.</summary>
        public static float Accumulate(float _accumulated, int _grade, float _deltaSeconds, out int _wholeCheese)
        {
            float total = _accumulated + IncomePerSecond(_grade) * _deltaSeconds;
            _wholeCheese = (int)total;
            return total - _wholeCheese;
        }
    }
}
