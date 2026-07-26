using SteamLobby = Steamworks.Data.Lobby;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 접속 UI(IMGUI). 로컬 Host/Client(에디터·MPPM QA용) + 스팀 매칭 섹션
    /// (방 만들기 공개/친구만 · 공개 방 목록 참여 · 오버레이 초대 안내).
    /// 스팀 미실행이면 스팀 섹션은 숨김 — 기존 로컬 흐름만 노출.
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        private SteamLobbyManager m_Steam;
        private string m_RoomName = "";
        private SteamLobby[] m_Lobbies = System.Array.Empty<SteamLobby>();

        private void Awake()
        {
            m_Steam = GetComponent<SteamLobbyManager>();
        }

        private void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 240, 520));

            if (!nm.IsClient && !nm.IsServer)
            {
                GUILayout.Label("로컬 (같은 PC)");
                if (GUILayout.Button("Host")) HostPortFallback.StartHost(nm);
                if (GUILayout.Button("Client"))
                {
                    HostPortFallback.ApplyHostPortToTransport(nm);
                    nm.StartClient();
                }

                DrawSteamSection();
            }
            else
            {
                bool steamMode = m_Steam != null && m_Steam.InLobby;
                GUILayout.Label($"Mode: {(nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client")}{(steamMode ? " (Steam)" : "")}");
                // ConnectedClientsList는 서버 전용 — 클라이언트에서 접근하면 NotServerException
                if (nm.IsServer)
                {
                    GUILayout.Label($"Players: {nm.ConnectedClientsList.Count}");
                }
                if (steamMode)
                {
                    GUILayout.Label("친구 초대: Shift+Tab 오버레이");
                }
                if (GUILayout.Button("Disconnect"))
                {
                    nm.Shutdown();
                    if (m_Steam != null) m_Steam.LeaveLobby();
                }
            }

            GUILayout.EndArea();
        }

        private void DrawSteamSection()
        {
            if (m_Steam == null || !SteamBootstrap.Ready)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Steam: 미실행 (로컬 전용)");
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("― Steam 매칭 ―");
            GUILayout.Label("방 이름");
            m_RoomName = GUILayout.TextField(m_RoomName, 24);
            if (GUILayout.Button("방 만들기 (공개)")) m_Steam.CreateLobby(m_RoomName, true);
            if (GUILayout.Button("방 만들기 (친구만)")) m_Steam.CreateLobby(m_RoomName, false);

            GUILayout.Space(4f);
            if (GUILayout.Button("방 목록 새로고침"))
            {
                m_Steam.RefreshLobbies(_lobbies => m_Lobbies = _lobbies);
            }
            foreach (SteamLobby lobby in m_Lobbies)
            {
                string label = SteamLobbyLogic.RoomLabel(
                    lobby.GetData(SteamLobbyLogic.NAME_KEY), lobby.MemberCount, lobby.MaxMembers);
                if (GUILayout.Button(label))
                {
                    m_Steam.JoinLobby(lobby);
                }
            }
        }
    }
}
