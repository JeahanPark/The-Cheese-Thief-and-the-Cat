namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 방 목록 한 줄이 화면에 보여 줄 것. 스팀 타입에서 뜯어낸 **값만** 담는다.
    ///
    /// 왜 값으로 옮겨 담는가 — 화면이 <c>Steamworks.Data.Lobby</c>를 직접 들면
    /// 스팀 없이는 UI를 한 줄도 못 그린다. 에디터에는 스팀이 없으니 그러면 개발이 막힌다.
    /// 여기서 끊어 두면 가짜 목록으로도 화면이 그대로 돈다.
    /// </summary>
    public readonly struct LobbyRoomInfo
    {
        public readonly string Name;
        public readonly string Host;
        public readonly int MemberCount;
        public readonly int MaxMembers;

        /// <summary>스팀 로비 목록에서의 자리. 참여할 때 이 번호로 원본을 되찾는다.</summary>
        public readonly int Index;

        public LobbyRoomInfo(string _name, string _host, int _memberCount, int _maxMembers, int _index)
        {
            Name = _name;
            Host = _host;
            MemberCount = _memberCount;
            MaxMembers = _maxMembers;
            Index = _index;
        }

        public bool IsFull => MemberCount >= MaxMembers;

        public string CountLabel => $"{MemberCount} / {MaxMembers}";

        public string StateLabel => IsFull ? "가득 참" : "대기 중";
    }
}
