using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 깡패 변신(2초) 동안 **이동·공격·액티브·임명을 막는다**(2026-09-14 사용자 요청). 판정은 <see cref="GangLookVisual.Transforming"/> 하나다.
    ///
    /// 막는 자리: <c>PlayerMovement</c>(입력 0) · <c>MousePopgun</c>(입력·서버 FireRpc·봇) · <c>MouseDash</c> · <c>ShopController.CanUse</c> · <c>GangRecruit</c>.
    /// 서버도 같은 컴포넌트를 돌리므로(호스트) 서버 Rpc 입구에서도 같은 값으로 거절한다.
    ///
    /// **이미 하고 있던 행동은 결과를 그대로 성공시키고 변신한다**(사용자 결정) — 변신이 시작되는 순간
    /// <see cref="ICompletableAction.CompleteNow"/>를 부른다. 사격·설치·임명은 원래 즉시 끝나는 서버 판정이라 할 일이 없고,
    /// 시간을 들여 끝나는 것(대시)만 남은 결과를 한 번에 적용한다.
    /// </summary>
    public static class GangTransformLock
    {
        /// <summary>이 몸이 지금 변신 중이라 행동을 못 하나. 컴포넌트가 없는 몸(고양이)은 항상 거짓.</summary>
        public static bool Locked(GangLookVisual _visual) => _visual != null && _visual.Transforming;

        /// <summary>캐시 없이 한 번 묻는 자리(서버 Rpc 입구 등) — 매 프레임 부르는 곳은 컴포넌트를 캐시해 <see cref="Locked(GangLookVisual)"/>를 쓴다.</summary>
        public static bool Locked(Component _any) => _any != null && Locked(_any.GetComponent<GangLookVisual>());
    }

    /// <summary>시간을 들여 끝나는 행동 — 변신이 끼어들면 **남은 결과를 즉시 적용하고** 끝낸다.</summary>
    public interface ICompletableAction
    {
        void CompleteNow();
    }
}
