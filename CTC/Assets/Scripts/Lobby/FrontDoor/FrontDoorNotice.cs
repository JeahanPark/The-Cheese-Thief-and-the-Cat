namespace CTC.Runtime.Lobby.FrontDoor
{
    /// <summary>
    /// 씬을 넘어 전할 알림 한 장 (2026-09-14, R.E.P.O. `PagePopUpScheduled`와 같은 방식).
    ///
    /// 대기실에서 끊기면 메인으로 돌아오는데, 돌아오는 순간 대기실의 캔버스가 사라진다 —
    /// 그래서 사유를 여기 적어 두고 **메인 화면이 선 뒤에** 띄운다. 한 번 꺼내면 비운다.
    /// </summary>
    public static class FrontDoorNotice
    {
        private static string s_Title;
        private static string s_Body;

        public static bool HasPending => !string.IsNullOrEmpty(s_Body);

        public static void Schedule(string _title, string _body)
        {
            s_Title = _title;
            s_Body = _body;
        }

        public static bool TryConsume(out string _title, out string _body)
        {
            _title = s_Title;
            _body = s_Body;
            s_Title = null;
            s_Body = null;
            return !string.IsNullOrEmpty(_body);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_Title = null;
            s_Body = null;
        }
    }
}
