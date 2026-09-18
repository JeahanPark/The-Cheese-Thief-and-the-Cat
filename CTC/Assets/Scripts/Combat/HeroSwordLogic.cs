using CTC.Runtime.Economy;
using CTC.Runtime.Match;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 용사 칼 (2026-09-19, 기획 `2026-09-19-hero-sword-design.md`).
    ///
    /// 깡패 몽둥이와 **같은 구조**지만 켜지는 조건이 다르다 — 깡패는 2단계에서만 몽둥이를 들고
    /// (정체를 숨기는 게 그 직업의 전부다), 용사는 **표를 밟는 순간부터** 칼을 든다(드러나는 게 의도다).
    /// 부채꼴 판정은 <see cref="GangLookLogic.InArc"/>를 그대로 쓴다 — 같은 규칙을 두 벌 두지 않는다.
    /// </summary>
    public static class HeroSwordLogic
    {
        /// <summary>칼을 들었는가 — 용사면 단계와 무관하게 참.</summary>
        public static bool Active(MouseJobId _job, MatchPhase _phase) => _job == MouseJobId.Hero;

        /// <summary>벨 수 있는 상대인가. 같은 직업(용사끼리)만 못 벤다 — 2단계의 깡패도 벤다.</summary>
        public static bool CanCut(MouseJobId _me, MouseJobId _target) => _me != _target;

        /// <summary>
        /// 용사가 못 사는 테크 — 총알(총·후추)은 칼이라 못 쓰고, 대시는 Space가 무적으로 찼다.
        /// 사 봐야 치즈만 사라지므로 구매 단계에서 막는다(깡패 `GangNoGun`과 같은 규약).
        /// </summary>
        public static bool Denied(TechId _id) => TechLogic.IsGunTech(_id) || _id == TechId.Dash;
    }
}
