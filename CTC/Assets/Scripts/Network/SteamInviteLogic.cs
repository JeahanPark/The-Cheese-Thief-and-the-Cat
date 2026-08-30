namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 스팀 오버레이 초대의 **문자열 규약**만 안다 — 순수 함수(EditMode TDD).
    ///
    /// 초대에는 길이 두 개다:
    /// - **게임이 켜져 있을 때**: `SteamFriends.OnGameLobbyJoinRequested` 콜백이 온다. 이미 처리하고 있었다.
    /// - **게임이 꺼져 있을 때**: 스팀이 게임을 켜면서 커맨드라인에 `+connect_lobby &lt;64bit id&gt;`를 넘긴다.
    ///   **이쪽이 통째로 빠져 있었다**(2026-08-30 확인) — 오버레이 초대의 절반이 조용히 실패했다.
    ///
    /// 친구목록의 "게임 참가"도 같은 문자열이다. 리치 프레즌스 `connect` 키에 이 값을 넣어 두면
    /// 스팀이 그걸 그대로 커맨드라인으로 넘겨 준다 — 그래서 두 경로가 한 규약을 공유한다.
    /// </summary>
    public static class SteamInviteLogic
    {
        /// <summary>스팀이 정한 이름이다. 바꿀 수 없다.</summary>
        public const string CONNECT_ARG = "+connect_lobby";

        /// <summary>친구목록 "게임 참가"가 읽는 리치 프레즌스 키. 이것도 스팀이 정한 이름이다.</summary>
        public const string RICH_PRESENCE_CONNECT_KEY = "connect";

        /// <summary>리치 프레즌스에 넣을 값 — 스팀이 이걸 그대로 커맨드라인으로 넘긴다.</summary>
        public static string ConnectValue(ulong _lobbyId) => $"{CONNECT_ARG} {_lobbyId}";

        /// <summary>
        /// 커맨드라인에서 초대받은 방 번호를 꺼낸다. 없거나 이상하면 false.
        ///
        /// 인자가 몇 번째에 오는지는 보장되지 않는다 — 스팀이 실행 옵션 뒤에 붙이기도 한다.
        /// 그래서 위치를 가정하지 않고 전체를 훑는다.
        /// </summary>
        public static bool TryParseConnectLobby(string[] _args, out ulong _lobbyId)
        {
            _lobbyId = 0UL;
            if (_args == null) return false;

            for (int i = 0; i < _args.Length - 1; i++)
            {
                if (_args[i] != CONNECT_ARG) continue;
                if (!ulong.TryParse(_args[i + 1], out ulong id) || id == 0UL) continue;

                _lobbyId = id;
                return true;
            }
            return false;
        }
    }
}
