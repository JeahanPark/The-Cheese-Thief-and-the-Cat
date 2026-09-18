using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 화면 색의 **단독 출처** (2026-08-30).
    ///
    /// 인트로·로비·대기실은 어두운 남색 바탕에 치즈 노랑 하나로 강조하는 톤으로 굳었는데,
    /// 인게임만 따로 자랐다 — 갈색 타일·흰 버튼·제각각인 회색이 섞여 있었다.
    /// 같은 게임의 다른 화면처럼 보이는 게 아니라 **다른 게임처럼 보였다.**
    ///
    /// 그래서 색을 여기 한 곳에 모은다. 에디터 씬 생성기(빌드 타임)와 런타임 HUD가
    /// 같은 값을 읽는다 — 런타임에 두는 이유가 그것이다(에디터 어셈블리는 런타임을 참조한다).
    ///
    /// **의미가 있는 색은 의미를 지킨다.** 초록=충분·보유·생존, 빨강=부족·위험은
    /// 톤만 팔레트에 맞추고 역할은 그대로 둔다. 색을 통일한다고 뜻까지 지우면 안 된다.
    /// </summary>
    public static class UiPalette
    {
        // ── 바탕 ──────────────────────────────────────────────
        /// <summary>패널 기본. 인트로·로비가 쓰는 값 그대로다.</summary>
        public static readonly Color Panel = new Color(0.043f, 0.059f, 0.086f, 0.94f);

        /// <summary>결과창처럼 화면을 덮는 것 — 뒤가 비치면 글자가 시끄럽다.</summary>
        public static readonly Color PanelSolid = new Color(0.043f, 0.059f, 0.086f, 0.98f);

        /// <summary>버튼·슬롯·타일 바탕. 패널 위에 한 겹 뜬다.</summary>
        public static readonly Color Slot = new Color(0.055f, 0.075f, 0.110f, 0.82f);

        /// <summary>지금은 못 고르는 칸 — 살 수 있는 칸(<see cref="Slot"/>)보다 어둡고 탁하다(상점 팝업).</summary>
        public static readonly Color SlotDim = new Color(0.035f, 0.045f, 0.063f, 0.82f);

        /// <summary>슬라이더 홈처럼 눌린 자리.</summary>
        public static readonly Color Groove = new Color(0.098f, 0.125f, 0.176f, 1f);

        /// <summary>테두리 선.</summary>
        public static readonly Color Line = new Color(0.126f, 0.169f, 0.239f, 1f);

        /// <summary>화면 전체를 덮는 암막(추첨 연출·모달 뒤).</summary>
        public static readonly Color Veil = new Color(0.004f, 0.006f, 0.012f, 0.84f);

        // ── 글자 ──────────────────────────────────────────────
        public static readonly Color TextOn = new Color(0.82f, 0.85f, 0.88f);
        public static readonly Color TextDim = new Color(0.435f, 0.475f, 0.529f);

        /// <summary>노란 바탕 위에 올리는 글자. 흰색을 올리면 안 읽힌다.</summary>
        public static readonly Color TextOnAccent = new Color(0.133f, 0.102f, 0.016f);

        // ── 강조 (치즈 노랑) ──────────────────────────────────
        /// <summary>#ffd84d — 이 게임의 유일한 강조색이다.</summary>
        public static readonly Color Accent = new Color(1f, 0.847f, 0.302f);

        /// <summary>강조를 글자로 쓸 때. 원색은 큰 면적에만 쓴다.</summary>
        public static readonly Color AccentSoft = new Color(1f, 0.914f, 0.639f);

        // ── 의미색 ────────────────────────────────────────────
        /// <summary>충분·보유·생존·대기 중. 로비 방 목록의 "대기 중"과 같은 값.</summary>
        public static readonly Color Good = new Color(0.50f, 0.82f, 0.42f);

        /// <summary>부족·위험·가득 참. 로비 방 목록의 "가득 참"과 같은 값.</summary>
        public static readonly Color Bad = new Color(0.95f, 0.44f, 0.36f);

        /// <summary>정보·영웅. 차갑고 밝은 쪽.</summary>
        public static readonly Color Info = new Color(0.55f, 0.78f, 1f);

        /// <summary>경고·깡패. 노랑과 빨강 사이라 강조색과 안 헷갈릴 만큼만 띄운다.</summary>
        public static readonly Color Warn = new Color(0.95f, 0.65f, 0.35f);

        /// <summary>같은 색을 투명도만 바꿔 쓸 때. 값을 또 적지 않게.</summary>
        public static Color WithAlpha(Color _color, float _alpha)
        {
            _color.a = _alpha;
            return _color;
        }
    }
}
