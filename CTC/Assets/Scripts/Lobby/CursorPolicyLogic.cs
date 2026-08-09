namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 커서를 놓아야 하는가 — 잠금의 단독 소유자인 `LobbyUI.Update()`가 매 프레임 묻는다.
    ///
    /// 놓는 상태: 로비 · 결과 패널 · 모달(옵션 창). 그 외 인게임은 잠근다.
    /// 채팅(IsTyping)은 항이 아니다 — 커서를 풀면 `OrbitCameraController`가 멈추므로 잠근 채 타이핑한다.
    ///
    /// 2026-08-08 버그의 재발 방지: 옵션 창이 `UiFocus.ModalOpen`을 세워도 이 판정에 항이 없어
    /// 다음 프레임에 도로 잠겼다 — 커서가 안 보이고 슬라이더 클릭이 전부 죽었다.
    /// (에디터는 게임 뷰 포커스를 잃으면 잠금을 강제 해제해 런타임 QA로는 못 잡는다 — 그래서 EditMode.)
    /// </summary>
    public static class CursorPolicyLogic
    {
        public static bool ShouldRelease(bool _inGame, bool _resultShowing, bool _modalOpen)
        {
            return !_inGame || _resultShowing || _modalOpen;
        }
    }
}
