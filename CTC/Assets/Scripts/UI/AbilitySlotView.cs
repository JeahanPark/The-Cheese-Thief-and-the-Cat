using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 능력 슬롯 1칸 — 그리기만. 판단은 AbilityBarLogic, 시간은 AbilityBar가 넘긴다.
    ///
    /// 쿨다운 연출(2026-08-17 시안): 밝은 타일 위에 검은 덮개가 12시부터 **시계방향으로 걷힌다**
    /// (Filled Radial360 · fillClockwise=false 라야 걷히는 가장자리가 시계방향으로 돈다),
    /// 걷히는 가장자리에 밝은 선, 그 위(최상단)에 남은 초 큰 숫자. 0이 되는 순간 0.3초 번쩍.
    /// </summary>
    public class AbilitySlotView : MonoBehaviour
    {
        private const float FLASH_SECONDS = 0.3f;

        [SerializeField] private Image m_Frame;        // 테두리(발광 색)
        [SerializeField] private Image m_Tile;         // 밝은 바탕
        [SerializeField] private Image m_Cover;        // Filled Radial360 — 쿨다운 덮개
        [SerializeField] private RectTransform m_Edge; // 걷히는 가장자리 선(중심 기준 회전)
        [SerializeField] private Image m_ActiveBar;    // Filled Horizontal — 지속 잔여
        [SerializeField] private TMP_Text m_KeyText;
        [SerializeField] private TMP_Text m_NameText;
        [SerializeField] private TMP_Text m_SecondsText;
        [SerializeField] private TMP_Text m_SubText;   // 지속 중 "6초"

        private static readonly Color FRAME_IDLE = new Color(0.54f, 0.45f, 0.32f, 1f);
        private static readonly Color FRAME_ACTIVE = new Color(1f, 0.82f, 0.48f, 1f);
        private static readonly Color TILE_IDLE = new Color(0.36f, 0.30f, 0.21f, 0.95f);
        private static readonly Color TILE_FLASH = new Color(0.95f, 0.80f, 0.50f, 1f);

        private float m_FlashLeft;

        public string Key => m_KeyText != null ? m_KeyText.text : "";
        public string Name => m_NameText != null ? m_NameText.text : "";
        public AbilityState State { get; private set; }
        public float SecondsShown { get; private set; }
        public bool IsAnimating => m_FlashLeft > 0f;

        public void SetSlot(AbilitySlot _slot)
        {
            m_KeyText.text = _slot.Key;
            m_NameText.text = _slot.Name;
            m_SubText.text = "";
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
            m_SubText.text = active ? AbilityBarLogic.SecondsText(_activeRemaining) + "초" : "";
            m_Frame.color = active ? FRAME_ACTIVE : FRAME_IDLE;

            m_FlashLeft = Mathf.Max(0f, m_FlashLeft - _dt);
            m_Tile.color = Color.Lerp(TILE_IDLE, TILE_FLASH, m_FlashLeft / FLASH_SECONDS);
        }
    }
}
