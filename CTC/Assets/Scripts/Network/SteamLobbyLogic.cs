namespace CTC.Runtime.Networking
{
    /// <summary>스팀 로비 문자열/필터 규칙 — 순수 함수(EditMode TDD). AppID 480 공용이라 태그 필터 필수.</summary>
    public static class SteamLobbyLogic
    {
        public const string GAME_TAG_KEY = "game";
        public const string GAME_TAG = "CTC";
        public const string NAME_KEY = "name";

        /// <summary>
        /// 방장 이름(2026-09-20). 목록에서 `Lobby.Owner`를 읽으면 **안 들어간 방은 빈 값**이다 —
        /// 스팀 문서: `GetLobbyOwner`는 「그 방의 멤버여야 읽을 수 있다」. 그래서 만들 때 데이터로 적어 둔다.
        /// </summary>
        public const string HOST_KEY = "host";

        /// <summary>비밀번호 **유무**만 적는다("1"/"0"). 비밀번호 자체는 방 데이터에 절대 안 쓴다 — 누구나 읽는다.</summary>
        public const string PASSWORD_KEY = "pw";

        /// <summary>프로토콜 번호(<see cref="NetVersionLogic.Protocol"/>). 검색에서 다른 버전 방을 뺀다.</summary>
        public const string VERSION_KEY = "ver";

        /// <summary>
        /// 방금 나온 내 방을 목록에서 숨기는 시간(초). 방장이 나가 비게 된 방을 스팀 백엔드가 지우는 데 잠깐 걸려서,
        /// 그 사이 「빠른 참가」가 자기 시체 방에 들어가려다 `DoesntExist`로 실패했다(2026-09-20).
        /// </summary>
        public const float RECENTLY_LEFT_SECONDS = 30f;

        public static string PasswordFlag(bool _hasPassword) => _hasPassword ? "1" : "0";

        public static bool IsPasswordFlag(string _value) => _value == "1";
        // 10 = 기준 구성 2:8 (2026-08-10). 최소는 1:6.
        public const int MAX_PLAYERS = 10;

        /// <summary>480(Spacewar)은 남의 테스트 로비가 섞임 — 우리 태그만 통과.</summary>
        public static bool IsCtcLobby(string _gameTag)
        {
            return _gameTag == GAME_TAG;
        }

        /// <summary>목록의 이 방이 **방금 내가 나온 그 방**인가 — 창 안이면 숨긴다. 0은 방 번호가 아니다.</summary>
        public static bool IsRecentlyLeft(ulong _lobbyId, ulong _lastLeftId, float _leftAt, float _now)
        {
            if (_lobbyId == 0UL || _lobbyId != _lastLeftId) return false;
            return _now - _leftAt < RECENTLY_LEFT_SECONDS;
        }

        /// <summary>목록에 보일 방장 이름 — 방 데이터(`host`)가 먼저, 없으면 스팀 소유자 이름, 그것도 없으면 빈 값.</summary>
        public static string HostLabel(string _hostData, string _ownerName)
        {
            if (!string.IsNullOrWhiteSpace(_hostData)) return _hostData.Trim();
            return string.IsNullOrWhiteSpace(_ownerName) ? "" : _ownerName.Trim();
        }

        public static string RoomLabel(string _name, int _memberCount, int _maxMembers)
        {
            string name = string.IsNullOrWhiteSpace(_name) ? "이름 없는 방" : _name;
            return $"{name} ({_memberCount}/{_maxMembers})";
        }

        public static string DefaultRoomName(string _ownerName)
        {
            return string.IsNullOrWhiteSpace(_ownerName) ? "CTC 방" : $"{_ownerName}의 방";
        }
    }
}
