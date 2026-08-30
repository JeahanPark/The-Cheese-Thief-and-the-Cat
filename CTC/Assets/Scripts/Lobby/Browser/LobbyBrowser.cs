using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Networking;
using R3;
using UnityEngine;
using SteamLobby = Steamworks.Data.Lobby;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 방 목록을 물어다 주는 Worker(CTC 규칙 6). 화면은 스팀을 모르고 이 스트림만 구독한다.
    ///
    /// **에디터에는 스팀이 없다.** 그래서 에디터에서는 가짜 목록을 내준다 — 안 그러면
    /// 화면을 한 줄도 못 그려 보고 개발이 막힌다. 가짜는 에디터에서만 나온다.
    ///
    /// 새로고침은 **명시 호출만** 한다. 프레임마다 스팀을 찌르면 로그아웃 순간에 터진다는 게
    /// 이 프로젝트가 이미 겪은 사고다(ConnectionUI, 2026-08-26).
    /// </summary>
    public class LobbyBrowser : MonoBehaviour
    {
        private readonly List<LobbyRoomInfo> m_Rooms = new List<LobbyRoomInfo>();
        private readonly Subject<Unit> m_Changed = new Subject<Unit>();

        private SteamLobby[] m_Raw = System.Array.Empty<SteamLobby>();
        private SteamLobbyManager m_Steam;
        private bool m_Busy;
        private int m_Selected = -1;

        /// <summary>목록이나 상태가 바뀔 때마다 울린다.</summary>
        public Observable<Unit> Changed => m_Changed;

        public IReadOnlyList<LobbyRoomInfo> Rooms => m_Rooms;

        /// <summary>불러오는 중인가 — 화면이 "새로고침" 버튼을 잠그는 데 쓴다.</summary>
        public bool Busy => m_Busy;

        /// <summary>스팀이 붙어 있는가. 아니면 화면이 안내를 띄운다.</summary>
        public bool SteamReady => SteamBootstrap.Ready;

        /// <summary>
        /// 지금 고른 줄. 고른 방이 없으면 -1.
        /// 선택은 화면이 아니라 여기 있다 — 참여 버튼(Controller)과 상세 패널(View)이
        /// 같은 값을 봐야 하는데, 둘 중 하나가 들면 나머지가 그걸 캐물어야 한다.
        /// </summary>
        public int SelectedIndex => m_Selected;

        public bool HasSelection => m_Selected >= 0 && m_Selected < m_Rooms.Count;

        public LobbyRoomInfo Selected => HasSelection ? m_Rooms[m_Selected] : default;

        public void Select(int _index)
        {
            int next = _index >= 0 && _index < m_Rooms.Count ? _index : -1;
            if (next == m_Selected) return;

            m_Selected = next;
            m_Changed.OnNext(Unit.Default);
        }

        private void Awake()
        {
            m_Steam = FindAnyObjectByType<SteamLobbyManager>();
            if (m_Steam == null) GameDebug.LogWarning("[LobbyBrowser] SteamLobbyManager가 씬에 없다");
        }

        private void Start() => Refresh();

        private void OnDestroy() => m_Changed.Dispose();

        public void Refresh()
        {
            if (m_Busy) return;

#if UNITY_EDITOR
            FillFakeRooms();
            return;
#else
            if (m_Steam == null || !SteamBootstrap.Ready)
            {
                m_Rooms.Clear();
                m_Raw = System.Array.Empty<SteamLobby>();
                m_Changed.OnNext(Unit.Default);
                return;
            }

            m_Busy = true;
            m_Changed.OnNext(Unit.Default);
            m_Steam.RefreshLobbies(OnLobbies);
#endif
        }

        /// <summary>
        /// 목록의 한 줄로 들어간다. 성공했으면 true — 화면은 그때만 대기실로 넘어간다.
        /// **에디터에는 스팀이 없다.** 그래서 에디터에서는 로컬(UTP)로 붙는다 — 안 그러면
        /// 대기실을 한 번도 못 열어 보고 개발이 막힌다(가짜 방 목록과 같은 이유).
        /// </summary>
        public bool Join(LobbyRoomInfo _room)
        {
#if UNITY_EDITOR
            if (!SteamBootstrap.Ready) return StartLocal(_host: false);
#endif
            if (m_Steam == null || !SteamBootstrap.Ready)
            {
                GameDebug.LogWarning("[LobbyBrowser] 스팀이 없어 참여할 수 없다");
                return false;
            }

            if (_room.Index < 0 || _room.Index >= m_Raw.Length)
            {
                GameDebug.LogWarning($"[LobbyBrowser] 목록이 낡았다 — 다시 불러온다 (index {_room.Index})");
                Refresh();
                return false;
            }

            m_Steam.JoinLobby(m_Raw[_room.Index]);
            return true;
        }

        /// <summary>방을 연다. 성공했으면 true.</summary>
        public bool Create(string _name, bool _isPublic)
        {
#if UNITY_EDITOR
            if (!SteamBootstrap.Ready) return StartLocal(_host: true);
#endif
            if (m_Steam == null || !SteamBootstrap.Ready)
            {
                GameDebug.LogWarning("[LobbyBrowser] 스팀이 없어 방을 만들 수 없다");
                return false;
            }

            m_Steam.CreateLobby(_name, _isPublic);
            return true;
        }

        /// <summary>
        /// 스팀 없이 NGO만 띄운다(에디터 전용). 대기실은 네트워크가 붙어야 화면이 서므로
        /// 이게 없으면 에디터에서 대기실이 통째로 빈 화면이 된다.
        /// </summary>
        private static bool StartLocal(bool _host)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null)
            {
                GameDebug.LogError("[LobbyBrowser] NetworkManager가 없다 — 씬에 NetworkRoot를 넣어라");
                return false;
            }

            if (nm.IsListening || nm.IsClient || nm.IsServer) return true;

            // **포트 폴백을 탄다.** 에디터는 플레이 종료 시 UDP 소켓을 자주 안 놓아 7788이 막힌다.
            // 그냥 StartHost를 부르면 그대로 실패해 방이 안 열린다(2026-08-30 실측) —
            // `HostPortFallback`은 빈 포트를 찾아 올라가고, 그 번호를 공유 파일에 적어
            // MPPM 클론이 같은 포트로 붙게 한다.
            bool ok;
            if (_host)
            {
                ok = HostPortFallback.StartHost(nm);
            }
            else
            {
                // 참여 쪽은 호스트가 적어 둔 포트를 읽어 맞춘다(폴백으로 7788이 아닐 수 있다).
                HostPortFallback.ApplyHostPortToTransport(nm);
                ok = nm.StartClient();
            }

            GameDebug.Log($"[LobbyBrowser] 스팀 없음 — 로컬 {(_host ? "호스트" : "참여")} {(ok ? "성공" : "실패")}");
            return ok;
        }

        private void OnLobbies(SteamLobby[] _found)
        {
            m_Busy = false;
            m_Raw = _found ?? System.Array.Empty<SteamLobby>();

            m_Selected = -1;
            m_Rooms.Clear();
            for (int i = 0; i < m_Raw.Length; i++)
            {
                SteamLobby lobby = m_Raw[i];
                m_Rooms.Add(new LobbyRoomInfo(
                    lobby.GetData(SteamLobbyLogic.NAME_KEY),
                    lobby.Owner.Name,
                    lobby.MemberCount,
                    lobby.MaxMembers,
                    i));
            }

            m_Changed.OnNext(Unit.Default);
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용 가짜 목록 — 화면 배치를 눈으로 확인하기 위한 것이다.</summary>
        private void FillFakeRooms()
        {
            m_Selected = -1;
            m_Rooms.Clear();
            m_Rooms.Add(new LobbyRoomInfo("치즈 창고 털러 갈 사람", "야옹이대장", 6, SteamLobbyLogic.MAX_PLAYERS, -1));
            m_Rooms.Add(new LobbyRoomInfo("초보만 · 욕 ㄴㄴ", "생쥐삼형제", 4, SteamLobbyLogic.MAX_PLAYERS, -1));
            m_Rooms.Add(new LobbyRoomInfo("빡겜 풀방만", "치즈러버", 10, SteamLobbyLogic.MAX_PLAYERS, -1));
            m_Rooms.Add(new LobbyRoomInfo("한 판만 더", "숨은쥐", 7, SteamLobbyLogic.MAX_PLAYERS, -1));
            m_Rooms.Add(new LobbyRoomInfo("아무나 오세요", "냥냥펀치", 2, SteamLobbyLogic.MAX_PLAYERS, -1));
            m_Changed.OnNext(Unit.Default);
        }
#endif
    }
}
