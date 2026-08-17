using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 능력 슬롯 1칸 — 그리기만. 판단은 AbilityBarLogic, 시간은 AbilityBar가 넘긴다.
    ///
    /// **핫바 칸과 같은 생김새**(2026-08-17 사용자 요청): 같은 크기·같은 어두운 바탕·세 줄
    /// ("Q  불" / 반줄 힌트 / 상태). 핫바의 "1  공격 강화 / 덮치기 더 아프게 / 보유"와 나란히 놓여도
    /// 한 벌로 보이게. 상태 줄은 준비 "준비"(초록) · 지속 중 "지속 n초"(금색) · 쿨다운은 비운다.
    ///
    /// 쿨다운 연출: 검은 덮개가 12시부터 **시계방향으로 걷힌다**(Filled Radial360 · fillClockwise=false 라야
    /// 걷히는 가장자리가 시계방향으로 돈다), 걷히는 가장자리에 밝은 선, 그 위(최상단)에 남은 초 큰 숫자.
    /// 0이 되는 순간 0.3초 번쩍.
    /// </summary>
    public class AbilitySlotView : MonoBehaviour
    {
        private const float FLASH_SECONDS = 0.3f;

        [SerializeField] private Image m_Tile;         // 바탕(핫바 칸과 같은 색)
        [SerializeField] private Image m_Cover;        // Filled Radial360 — 쿨다운 덮개
        [SerializeField] private RectTransform m_Edge; // 걷히는 가장자리 선(중심 기준 회전)
        [SerializeField] private Image m_ActiveBar;    // Filled Horizontal — 지속 잔여
        [SerializeField] private TMP_Text m_NameText;   // "Q  불"
        [SerializeField] private TMP_Text m_HintText;   // 반줄 힌트
        [SerializeField] private TMP_Text m_StatusText; // "준비" · "지속 6초"
        [SerializeField] private TMP_Text m_SecondsText;// 쿨다운 남은 초(최상단)

        // 핫바 칸 색과 같은 값(LobbyCanvasBuilder·ShopHotbar)
        private static readonly Color TILE_IDLE = new Color(0.07f, 0.055f, 0.04f, 0.82f);
        private static readonly Color TILE_FLASH = new Color(0.55f, 0.45f, 0.25f, 0.95f);
        private static readonly Color COLOR_READY = new Color(0.5f, 0.82f, 0.42f);   // 핫바 "보유"와 같은 초록
        private static readonly Color COLOR_ACTIVE = new Color(1f, 0.82f, 0.48f);
        // 덮개 — 바탕이 이미 검어서 검은 덮개는 안 보인다. 덮인 자리를 **뿌옇게** 밝히고 글자를 가려
        // "아직 안 걷힌 부분"이 눈에 들어오게 한다(2026-08-17 실측: 검정 0.84는 구분이 안 됐다).
        private static readonly Color COVER = new Color(0.62f, 0.56f, 0.46f, 0.72f);

        private AbilitySlot m_Slot;
        private float m_FlashLeft;

        public string Key => m_Slot.Key ?? "";
        public string Name => m_Slot.Name ?? "";
        public AbilityState State { get; private set; }
        public float SecondsShown { get; private set; }
        public bool IsAnimating => m_FlashLeft > 0f;

        public void SetSlot(AbilitySlot _slot)
        {
            m_Slot = _slot;
            m_Cover.color = COVER;
            m_NameText.text = $"{_slot.Key}  {_slot.Name}";
            m_HintText.text = _slot.Hint;
            m_StatusText.text = "";
        }

        /// <summary>매 프레임(살아 있을 때) 또는 상태가 바뀔 때.</summary>
        public void Apply(AbilitySlot _slot, float _cooldownRemaining, float _cooldownTotal,
            float _activeRemaining, float _activeTotal, float _dt)
        {
            AbilityState next = AbilityBarLogic.StateOf(_activeRemaining, _cooldownRemaining);
            if (State == AbilityState.Cooldown && next == AbilityState.Ready) m_FlashLeft = FLASH_SECONDS;
            State = next;
            SecondsShown = _cooldownRemaining;

            bool cooling = next == AbilityState.Cooldown;
            float remain = cooling ? AbilityBarLogic.FillRatio(_cooldownRemaining, _cooldownTotal) : 0f;
            m_Cover.fillAmount = remain;
            m_Edge.gameObject.SetActive(cooling && remain > 0f && remain < 1f);
            m_Edge.localRotation = Quaternion.Euler(0f, 0f, -(1f - remain) * 360f);
            m_SecondsText.text = cooling ? AbilityBarLogic.SecondsText(_cooldownRemaining) : "";

            bool active = next == AbilityState.Active;
            m_ActiveBar.gameObject.SetActive(active);
            if (active) m_ActiveBar.fillAmount = AbilityBarLogic.FillRatio(_activeRemaining, _activeTotal);

            if (active)
            {
                m_StatusText.text = "지속 " + AbilityBarLogic.SecondsText(_activeRemaining) + "초";
                m_StatusText.color = COLOR_ACTIVE;
            }
            else
            {
                m_StatusText.text = cooling ? "" : "준비";
                m_StatusText.color = COLOR_READY;
            }

            m_FlashLeft = Mathf.Max(0f, m_FlashLeft - _dt);
            m_Tile.color = Color.Lerp(TILE_IDLE, TILE_FLASH, m_FlashLeft / FLASH_SECONDS);
        }
    }
}
