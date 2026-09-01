using System;

namespace CTC.Runtime.UI.Popup
{
    /// <summary>
    /// 팝업 스택의 순수 판정 (2026-09-01, WES `PopupManager`에서 로직만 떼어냄).
    /// MonoBehaviour·Addressables 없이 EditMode가 덮을 수 있는 부분만 여기 있다.
    /// </summary>
    public static class PopupStackLogic
    {
        /// <summary>팝업이 하나라도 떠 있으면 모달 — <see cref="Core.UiFocus"/>가 이걸로 입력을 막는다.</summary>
        public static bool ModalOpen(int _openedCount) => _openedCount > 0;

        /// <summary>맨 위 인덱스. 비었으면 -1.</summary>
        public static int TopIndex(int _openedCount) => _openedCount - 1;

        /// <summary>
        /// Addressable 주소 = **클래스명**(WES 규약). 어긋나면 컴파일은 되고 런타임에 못 연다 —
        /// 그래서 부르는 쪽이 실패를 반드시 로그로 남긴다.
        /// </summary>
        public static string AddressOf(Type _type) => _type != null ? _type.Name : "";

        /// <summary>같은 팝업을 두 번 열지 않는다 — 겹쳐도 보이는 건 하나뿐이라 스택만 더러워진다.</summary>
        public static bool CanOpen(bool _alreadyOpen) => !_alreadyOpen;
    }
}
