namespace CTC.Runtime.Networking
{
    /// <summary>스팀 로비 문자열/필터 규칙 — 순수 함수(EditMode TDD). AppID 480 공용이라 태그 필터 필수.</summary>
    public static class SteamLobbyLogic
    {
        public const string GAME_TAG_KEY = "game";
        public const string GAME_TAG = "CTC";
        public const string NAME_KEY = "name";
        public const int MAX_PLAYERS = 8;

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
