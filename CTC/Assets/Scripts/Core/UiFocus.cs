using R3;
using TMPro;
using UnityEngine.EventSystems;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// "지금 UI가 입력을 먹고 있는가" 한 곳 (2026-08-01).
    ///
    /// 전에는 `IsTypingInUi()`가 `ShopHotbar`·`CheerDirector`에 각각 복붙돼 있었고,
    /// `PlayerMovement`·`MouseDash`·ESC는 아예 가드가 없었다 — 채팅을 치면 캐릭터가 걸어가고
    /// ESC로 게임이 꺼졌다. 판정을 여기 하나로 모으고 전부 이걸 부른다.
    ///
    /// 두 가지를 구분한다:
    /// - `IsTyping` — 텍스트 입력창에 포커스가 있다(채팅). 키보드만 막으면 된다.
    /// - `ModalOpen` — 옵션 팝업처럼 화면을 덮는 창이 떠 있다. 마우스까지 막는다.
    /// </summary>
    public static class UiFocus
    {
        private static readonly ReactiveProperty<bool> s_ModalOpen = new ReactiveProperty<bool>(false);

        /// <summary>옵션 팝업 등 모달이 떠 있는가. `LobbyUI`가 커서 잠금을 풀 때 본다.</summary>
        public static ReadOnlyReactiveProperty<bool> ModalOpenChanged => s_ModalOpen;

        public static bool ModalOpen => s_ModalOpen.Value;

        /// <summary>모달이 열고 닫힐 때 팝업 본인이 알린다.</summary>
        public static void SetModalOpen(bool _open) => s_ModalOpen.Value = _open;

        /// <summary>텍스트 입력창(TMP_InputField)에 포커스가 있는가 — 채팅 중이면 true.</summary>
        public static bool IsTyping
        {
            get
            {
                EventSystem es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null) return false;
                return es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
            }
        }

        /// <summary>게임플레이 입력(이동·공격·핫키)을 무시해야 하는가.</summary>
        public static bool BlocksGameplay => ModalOpen || IsTyping;

        /// <summary>도메인 리로드를 끄고 플레이할 때 정적 값이 남는다 — 씬 시작마다 비운다.</summary>
        internal static void ResetForNewSession() => s_ModalOpen.Value = false;
    }
}
