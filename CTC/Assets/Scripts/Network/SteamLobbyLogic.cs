namespace CTC.Runtime.Networking
{
    /// <summary>스팀 로비 문자열/필터 규칙 — 순수 함수(EditMode TDD). AppID 480 공용이라 태그 필터 필수.</summary>
    public static class SteamLobbyLogic
    {
        public const string GAME_TAG_KEY = "game";
        public const string GAME_TAG = "CTC";
        public const string NAME_KEY = "name";

        /// <summary>비밀번호 **유무**만 적는다("1"/"0"). 비밀번호 자체는 방 데이터에 절대 안 쓴다 — 누구나 읽는다.</summary>
        public const string PASSWORD_KEY = "pw";

        /// <summary>프로토콜 번호(<see cref="NetVersionLogic.Protocol"/>). 검색에서 다른 버전 방을 뺀다.</summary>
        public const string VERSION_KEY = "ver";

        public static string PasswordFlag(bool _hasPassword) => _hasPassword ? "1" : "0";

        public static bool IsPasswordFlag(string _value) => _value == "1";
        // 10 = 기준 구성 2:8 (2026-08-10). 최소는 1:6.
        public const int MAX_PLAYERS = 10;

        /// <summary>480(Spacewar)은 남의 테스트 로비가 섞임 — 우리 태그만 통과.</summary>
        public static bool IsCtcLobby(string _gameTag)
        {
            return _gameTag == GAME_TAG;
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
