namespace CTC.Runtime.Core
{
    /// <summary>Enter 한 번이 갈 곳. 순서가 곧 우선순위다.</summary>
    public enum EnterRoute
    {
        /// <summary>채팅이 열려 있으면 보낸다 — 열려 있는 입력창이 언제나 우선.</summary>
        SubmitChat,

        /// <summary>상점 안에서 살 후보가 있으면 확정 구매.</summary>
        ConfirmBuy,

        /// <summary>그 외에는 채팅을 연다.</summary>
        OpenChat,
    }

    /// <summary>
    /// Enter 배분 순수 로직 (2026-09-07). ESC와 같은 이유로 **한 곳에서만** 읽는다.
    ///
    /// 전에는 `ChatHud`와 `ShopHotbar`가 같은 프레임에 각자 Enter를 읽었다. 게다가
    /// `UiFocus.IsTyping`은 `EventSystem.currentSelectedGameObject`를 보는데 TMP의
    /// `ActivateInputField()`는 LateUpdate에나 선택을 넣는다 — 그 프레임 Update에서는
    /// `BlocksGameplay`가 아직 false다. 그래서 Enter 한 번이 **채팅을 열면서 동시에 구매**했다.
    /// </summary>
    public static class EnterRouteLogic
    {
        public static EnterRoute Route(bool _chatOpen, bool _hotbarCanConfirm)
        {
            if (_chatOpen) return EnterRoute.SubmitChat;
            if (_hotbarCanConfirm) return EnterRoute.ConfirmBuy;
            return EnterRoute.OpenChat;
        }
    }
}
