using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 고양이 봇이 **무엇을 할지** 고르는 규칙(순수).
    ///
    /// 이 봇의 승률이 곧 "소리 밸런스가 쓸모 있는가"의 답이다. 그래서 판단은
    /// **소리와 눈으로 얻은 것만** 쓰고, 규칙은 단순해야 한다 — 영리해지면 그 순간
    /// 재는 대상이 밸런스가 아니라 봇의 실력이 된다.
    /// </summary>
    public static class CatBotLogic
    {
        public enum State
        {
            /// <summary>보인다 — 쫓는다.</summary>
            Chase = 0,
            /// <summary>사거리 안이다 — 덮친다.</summary>
            Pounce = 1,
            /// <summary>소리가 났던 쪽으로 간다.</summary>
            Investigate = 2,
            /// <summary>단서가 없다 — 돌아다닌다.</summary>
            Patrol = 3,
        }

        /// <summary>
        /// **본 것이 들은 것을 이긴다.** 눈앞에 있는데 소리를 쫓아 돌아서면 사람이 안 하는 짓이 된다.
        /// 덮치기는 사거리 안에서만 — 밖에서 눌러 봐야 쿨다운만 태운다.
        /// </summary>
        public static State Decide(bool _seesPrey, float _distanceToPrey, float _pounceRange, bool _hasSoundLead)
        {
            if (_seesPrey) return _distanceToPrey <= _pounceRange ? State.Pounce : State.Chase;
            return _hasSoundLead ? State.Investigate : State.Patrol;
        }

        /// <summary>
        /// 소리 단서가 여럿이면 무엇부터 갈까. **가까울수록·최근일수록** 값이 크다.
        /// 오래된 소리를 쫓으면 이미 떠난 자리로 가고, 먼 소리를 쫓으면 도착 전에 식는다.
        /// </summary>
        public static float LeadScore(float _distance, float _age)
        {
            return 1f / (Mathf.Max(1f, _distance) * (1f + Mathf.Max(0f, _age)));
        }

        /// <summary>
        /// 조사하러 갔는데 아무도 없었다 — 이만큼 지나면 그 단서를 버린다.
        /// 없으면 봇이 빈 자리를 영원히 서성인다.
        /// </summary>
        public const float GIVE_UP_SECONDS = 5f;

        /// <summary>덮치기 직전에 잠행을 푼다 — 느린 채로 덮치면 사람이 안 하는 짓이다.</summary>
        public static bool ShouldSneak(State _state) => _state == State.Investigate;
    }
}
