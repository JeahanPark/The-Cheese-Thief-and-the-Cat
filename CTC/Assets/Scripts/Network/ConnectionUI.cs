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
        /// <summary>
        /// 방 하나 = 스팀 핸들 + **새로고침 순간에 만든 라벨**.
        /// 라벨을 들고 있는 이유는 <see cref="OnGUI"/>가 매 프레임 돌기 때문이다 —
        /// `lobby.GetData`·`MemberCount`는 스팀 내부 인터페이스를 찌르는 호출이라,
        /// 스팀이 흔들린 순간(로그아웃·Shutdown·초기화 실패)에 프레임마다 NRE가 터진다.
        /// 실제로 상대 플레이어 화면에서 `Lobby.GetData`가 OnGUI에서 폭주했다(2026-08-20).
        /// </summary>
        private readonly struct LobbyEntry
        {
            public readonly SteamLobby Lobby;
            public readonly string Label;
            public LobbyEntry(SteamLobby _lobby, string _label) { Lobby = _lobby; Label = _label; }
        }

        private SteamLobbyManager m_Steam;
        private string m_RoomName = "";
        private LobbyEntry[] m_Lobbies = System.Array.Empty<LobbyEntry>();

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
            // SteamBootstrap.Ready만으로는 부족하다 — 초기화 뒤에 스팀이 내려가면 Ready는
            // 참인 채로 내부 인터페이스만 죽는다. IsValid가 그 상태를 잡아 준다.
            if (m_Steam == null || !SteamBootstrap.Ready || !Steamworks.SteamClient.IsValid)
            {
                GUILayout.Space(8f);
                GUILayout.Label("― Steam 매칭 ―");
                GUILayout.Label("Steam 연결 안 됨.\n다른 사람과 하려면:\n1) Steam 실행 후 로그인\n2) 아래 버튼으로 재연결");
                if (GUILayout.Button("Steam 다시 연결"))
                {
                    SteamBootstrap.TryInit();
                }
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
                m_Steam.RefreshLobbies(_lobbies => m_Lobbies = BuildEntries(_lobbies));
            }
            foreach (LobbyEntry entry in m_Lobbies)
            {
                if (GUILayout.Button(entry.Label)) m_Steam.JoinLobby(entry.Lobby);
            }
        }

        /// <summary>목록을 받은 **그 순간에** 라벨을 굳힌다 — 그리는 동안은 스팀을 안 부른다.</summary>
        private static LobbyEntry[] BuildEntries(SteamLobby[] _lobbies)
        {
            if (_lobbies == null || _lobbies.Length == 0) return System.Array.Empty<LobbyEntry>();

            var list = new System.Collections.Generic.List<LobbyEntry>(_lobbies.Length);
            foreach (SteamLobby lobby in _lobbies)
            {
                // 방 하나가 사라지는 중이어도 목록 전체가 날아가면 안 된다 — 그 방만 건너뛴다
                try
                {
                    list.Add(new LobbyEntry(lobby, SteamLobbyLogic.RoomLabel(
                        lobby.GetData(SteamLobbyLogic.NAME_KEY), lobby.MemberCount, lobby.MaxMembers)));
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SteamLobby] 방 정보 읽기 실패(목록에서 제외): {e.Message}");
                }
            }
            return list.ToArray();
        }
    }
}
