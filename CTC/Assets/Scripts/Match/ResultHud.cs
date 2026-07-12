using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 매치 결과 패널: 승자 텍스트 + 호스트 전용 Restart 버튼.
    /// 표시 중 커서를 해제한다 — LobbyUI의 인게임 강제 잠금은 IsShowing으로 양보받는다.
    /// </summary>
    public class ResultHud : MonoBehaviour
    {
        /// <summary>결과 패널이 커서를 점유 중인지 — LobbyUI가 잠금 강제 전 확인.</summary>
        public static bool IsShowing { get; private set; }

        [SerializeField] private MatchManager m_Match;
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private TMP_Text m_Text;
        [SerializeField] private Button m_RestartButton;

        private void Awake()
        {
            m_RestartButton.onClick.AddListener(() => m_Match.RequestRestart());
            m_Panel.SetActive(false);
            IsShowing = false;
        }

        private void OnDestroy()
        {
            IsShowing = false;
        }

        private void Update()
        {
            bool show = m_Match != null && m_Match.IsSpawned && m_Match.Result != MatchResult.None;
            if (m_Panel.activeSelf != show)
            {
                m_Panel.SetActive(show);
                IsShowing = show;
                if (show)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            if (!show) return;

            m_Text.text = m_Match.Result == MatchResult.MouseWin ? "MICE WIN!" : "CAT WINS!";

            NetworkManager nm = NetworkManager.Singleton;
            m_RestartButton.gameObject.SetActive(nm != null && nm.IsHost);
        }
    }
}
