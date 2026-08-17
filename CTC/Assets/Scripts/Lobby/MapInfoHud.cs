using CTC.Runtime.Map;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 로비 좌하단 맵 설명. 고른 맵의 크기·구성과 권장 인원을 보여주고,
    /// 지금 인원이 권장에 못 미치면 경고한다.
    ///
    /// 넓은 맵에 인원이 적으면 고양이가 쥐를 못 찾아 타이머 만료(쥐 승)로 끝난다 —
    /// 방을 만들 때 알아야 할 정보라 게임 중이 아니라 로비에서 띄운다.
    /// (게임이 시작되면 꺼진다 — 조작 안내는 하단 능력 바·핫바가 맡는다)
    ///
    /// 표시는 R3 구독 — 맵·참가자·시작 여부가 바뀔 때만 다시 그린다(2026-07-31).
    /// </summary>
    public class MapInfoHud : MonoBehaviour
    {
        private static readonly Color COLOR_OK = new Color(0.78f, 0.82f, 0.9f);
        private static readonly Color COLOR_WARN = new Color(1f, 0.72f, 0.35f);

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private TMP_Text m_Text;

        private void Start()
        {
            // Awake가 아니라 Start — LobbyController.Awake가 세션·맵 감독을 잡은 뒤여야 한다.
            if (m_Lobby != null) m_Lobby.ViewChanged.Subscribe(_ => Render()).AddTo(this);
        }

        private void Render()
        {
            NetworkManager nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsClient || nm.IsServer);
            bool inLobby = connected && m_Lobby != null && m_Lobby.IsSpawned && !m_Lobby.GameStarted;

            if (m_Text.gameObject.activeSelf != inLobby) m_Text.gameObject.SetActive(inLobby);
            if (!inLobby) return;

            MapLayout layout = m_Lobby.ActiveLayout;
            if (layout == null) { m_Text.text = "맵 없음"; return; }

            int mice = CountMice();
            bool tooFew = mice > 0 && mice < layout.RecommendedMiceMin;

            float span = layout.HalfExtent * 2f;
            m_Text.text =
                $"<b>{layout.DisplayName}</b>   {span:F0}×{span:F0}m\n" +
                $"방 {layout.Rooms.Count} · 치즈 지점 {layout.Cheese.Count} · 요새 자리 {layout.Fortresses.Count}\n" +
                $"권장 쥐 {layout.RecommendedMiceMin}~{layout.RecommendedMiceMax}마리 (현재 {mice})" +
                (tooFew ? "\n인원이 적어 고양이가 쥐를 찾기 어렵다 — 더 좁은 맵을 권장" : "");
            m_Text.color = tooFew ? COLOR_WARN : COLOR_OK;
        }

        private int CountMice()
        {
            int mice = 0;
            for (int i = 0; i < m_Lobby.PlayerCount; i++)
            {
                if (m_Lobby.GetPlayer(i).Role == RoleId.Mouse) mice++;
            }
            return mice;
        }
    }
}
