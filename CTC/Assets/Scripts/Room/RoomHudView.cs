using CTC.Runtime.Core;
using R3;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실 머리·발치 표시 (MVC의 View) — 방 이름 · 인원 · 시작 버튼과 그 안내.
    ///
    /// 버튼을 **잠그기만** 한다. 눌렀을 때 무슨 일이 나는지는 <see cref="RoomController"/>가 안다.
    /// 추첨 연출은 <see cref="RoomDrawStage"/>가 따로 맡는다 — 여기는 대기 중 화면만 그린다.
    /// </summary>
    public class RoomHudView : MonoBehaviour
    {
        [SerializeField] private RoomSession m_Session;

        [Header("머리")]
        [SerializeField] private TMP_Text m_RoomNameLabel;
        [SerializeField] private TMP_Text m_CountLabel;

        [Header("하단")]
        [SerializeField] private GameObject m_StartButton;
        [SerializeField] private TMP_Text m_StartHint;

        private void Start()
        {
            if (m_Session == null)
            {
                GameDebug.LogWarning("[Room] HUD에 RoomSession이 안 붙어 있다");
                return;
            }

            m_Session.ViewChanged.Subscribe(_ => Render()).AddTo(this);
        }

        private void Render()
        {
            if (m_Session == null) return;

            int count = m_Session.MemberCount;
            bool isHost = m_Session.IsHost;

            if (m_RoomNameLabel != null)
            {
                string name = m_Session.RoomName;
                m_RoomNameLabel.text = string.IsNullOrWhiteSpace(name) ? "대기실" : name;
            }

            if (m_CountLabel != null) m_CountLabel.text = RoomLogic.CountLabel(count);
            if (m_StartHint != null) m_StartHint.text = RoomLogic.StartHint(count, isHost);

            // 방장이 아니면 버튼 자체를 안 보여 준다 — 못 누르는 버튼이 떠 있으면 기다리게 만든다.
            if (m_StartButton != null) m_StartButton.SetActive(isHost && !m_Session.Starting);
        }

    }
}
