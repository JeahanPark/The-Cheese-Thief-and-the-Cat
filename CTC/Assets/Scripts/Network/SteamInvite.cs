using CTC.Runtime.Core;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 켜질 때 받은 초대를 들고 있다가 **붙을 수 있는 곳에서 꺼내 쓴다** (2026-08-30).
    ///
    /// 왜 바로 안 붙나: 스팀이 `+connect_lobby`를 넘기는 순간 게임은 인트로 씬에 있고,
    /// 거기엔 `NetworkManager`도 <see cref="SteamLobbyManager"/>도 없다. 그래서 번호만 적어 두고,
    /// 로비 씬에서 스팀 매니저가 서면 그때 꺼내 붙는다.
    ///
    /// **한 번만 쓴다.** 꺼내 가면 비운다 — 안 그러면 방을 나올 때마다 같은 방으로 다시 끌려간다.
    /// </summary>
    public static class SteamInvite
    {
        private static ulong s_PendingLobbyId;

        /// <summary>초대를 받고 켜졌는가. 인트로가 이걸 보고 메뉴를 건너뛴다.</summary>
        public static bool HasPending => s_PendingLobbyId != 0UL;

        /// <summary>초대를 꺼낸다. 꺼내면 비워지므로 두 번 붙지 않는다.</summary>
        public static bool TryConsume(out ulong _lobbyId)
        {
            _lobbyId = s_PendingLobbyId;
            s_PendingLobbyId = 0UL;
            return _lobbyId != 0UL;
        }

        /// <summary>
        /// 앱이 뜨자마자 커맨드라인을 한 번 읽는다. 씬보다 먼저 돌아야 인트로가 판단할 수 있다.
        /// 도메인 리로드를 끈 채 플레이하면 값이 남으므로 여기서 같이 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReadCommandLine()
        {
            s_PendingLobbyId = 0UL;

            if (SteamInviteLogic.TryParseConnectLobby(System.Environment.GetCommandLineArgs(), out ulong id))
            {
                s_PendingLobbyId = id;
                GameDebug.Log($"[SteamInvite] 초대를 받고 켜졌다 — 방 {id}");
            }
        }
    }
}
