using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 「지금 이 몸이 행동할 수 있나」 한 곳 (2026-09-19).
    ///
    /// 막는 이유가 셋으로 늘었다 — 깡패 변신(<see cref="GangTransformLock"/>) · 스턴(<see cref="Combat.Stun"/>) ·
    /// 용사 변신(<see cref="HeroLook"/>). 가드 자리마다 셋을 따로 부르면 하나를 빠뜨린 자리에서
    /// 굳은 고양이가 덮친다. 그래서 입력·Rpc 입구는 전부 여기만 본다.
    /// </summary>
    public static class ActionLock
    {
        /// <summary>이 몸이 지금 이동·공격·액티브·임명을 못 하나.</summary>
        public static bool Locked(Component _body)
        {
            if (_body == null) return false;
            return GangTransformLock.Locked(_body) || Combat.Stun.Locked(_body) || HeroLook.TransformingOf(_body);
        }
    }
}
