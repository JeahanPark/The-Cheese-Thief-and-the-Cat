using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 쥐 감옥 규칙(v3.8 4번). MonoBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// 수감·구출·체류의 산수만 여기 있고, "누가 갇혔나"는 `JailedTag`가,
    /// "언제 도는가"는 `JailDirector`가 맡는다.
    /// </summary>
    public static class JailLogic
    {
        // 수치 원본은 CSV(BalanceInfo.csv)
        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        /// <summary>구조되지 않으면 영구 탈락하기까지의 시간. 사실상 이 확장의 난이도 노브다.</summary>
        public static float HoldSeconds => Balance.JailHoldSeconds;

        /// <summary>구조자를 한 자리에 묶어 두는 시간 — 이 몇 초가 고양이에게 주는 기회다.</summary>
        public static float RescueSeconds => Balance.JailRescueSeconds;

        public static float RescueRadius => Balance.JailRescueRadius;
        public static float ReleaseHpRatio => Balance.JailReleaseHpRatio;

        /// <summary>
        /// 잡힌 자리에서 가장 가까운 감옥. 없으면 -1.
        /// 높이는 무시한다(평면 맵) — y 차이로 감옥이 바뀌면 안 된다.
        /// 동점이면 앞선 인덱스: 결정적이어야 QA가 재현할 수 있다.
        /// </summary>
        public static int NearestJailIndex(Vector3 _from, IReadOnlyList<Vector3> _jails)
        {
            if (_jails == null || _jails.Count == 0) return -1;

            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _jails.Count; i++)
            {
                Vector3 d = _jails[i] - _from;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 구출 게이지. **반경을 벗어나면 0으로 돌아간다** — 스치듯 지나가며 풀 수 있으면
        /// 구출에 위험이 없고, 그러면 잡는 행위 자체가 무의미해진다.
        /// 임계 위로는 쌓이지 않는다(다음 구출이 즉시 끝나 버리는 것을 막는다).
        /// </summary>
        public static float AdvanceRescue(float _progress, float _deltaSeconds, bool _rescuerInside)
        {
            if (!_rescuerInside) return 0f;
            return Mathf.Min(RescueSeconds, _progress + Mathf.Max(0f, _deltaSeconds));
        }

        public static bool IsRescueComplete(float _progress, float _threshold)
        {
            return _progress >= _threshold;
        }

        /// <summary>남은 체류 시간 — 0 아래로 내려가지 않는다.</summary>
        public static float AdvanceHold(float _remaining, float _deltaSeconds)
        {
            return Mathf.Max(0f, _remaining - Mathf.Max(0f, _deltaSeconds));
        }

        public static bool IsHoldExpired(float _remaining)
        {
            return _remaining <= 0f;
        }

        /// <summary>
        /// 석방 직후 HP. 온전히 돌려주면 고양이가 방금 들인 노력이 0이 된다 —
        /// 절반이면 "풀려났지만 위험하다"가 남아 구출 직후의 도주가 긴장된다.
        /// 비율이 0이어도 최소 1 — 석방하자마자 죽는 건 버그로 보인다.
        /// </summary>
        public static int ReleaseHp(int _maxHp, float _ratio)
        {
            return Mathf.Max(1, Mathf.RoundToInt(_maxHp * Mathf.Clamp01(_ratio)));
        }
    }
}
