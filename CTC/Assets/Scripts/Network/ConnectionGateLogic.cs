namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 접속 관문의 판정 — 순수 함수(EditMode TDD, 2026-09-14).
    ///
    /// **순서가 규칙이다.** 잠긴 방에 틀린 비밀번호로 온 사람에게 "비밀번호가 틀렸다"고 하면
    /// 다시 쳐 봐도 못 들어간다. 그래서 들어갈 수 없는 이유(잠김·가득 참)를 먼저 말한다.
    ///
    /// 거절 사유는 **코드**로 보낸다. 문구로 보내면 클라이언트가 "비밀번호가 필요하다"를
    /// 알아듣고 팝업을 띄울 수가 없다(문구가 바뀌면 조용히 깨진다).
    /// </summary>
    public static class ConnectionGateLogic
    {
        public const string CLOSED = "CLOSED";
        public const string FULL = "FULL";
        public const string PW_REQUIRED = "PW_REQUIRED";
        public const string PW_WRONG = "PW_WRONG";

        /// <summary>통과면 null, 아니면 거절 코드.</summary>
        public static string Decide(bool _isHostSelf, bool _accepting, int _connectedCount, int _maxPlayers,
            string _hostPassword, string _sentPassword)
        {
            if (_isHostSelf) return null;
            if (!_accepting) return CLOSED;
            if (_connectedCount >= _maxPlayers) return FULL;

            if (!LobbyPasswordLogic.HasPassword(_hostPassword)) return null;

            string sent = LobbyPasswordLogic.Normalize(_sentPassword);
            if (sent.Length == 0) return PW_REQUIRED;
            return sent == LobbyPasswordLogic.Normalize(_hostPassword) ? null : PW_WRONG;
        }

        /// <summary>
        /// 끊긴 사유 → 화면 문구. 우리 코드가 아니면(NGO·트랜스포트가 준 문장, 빈 값) 일반 문구로 덮는다 —
        /// 영어 내부 메시지를 그대로 보여 주지 않는다.
        /// </summary>
        public static string MessageOf(string _reason)
        {
            switch (_reason)
            {
                case CLOSED: return "이미 게임이 시작된 방입니다.";
                case FULL: return "방이 가득 찼습니다.";
                case PW_REQUIRED: return "비밀번호가 필요한 방입니다.";
                case PW_WRONG: return "비밀번호가 틀렸습니다.";
                default: return "방에 연결하지 못했습니다.";
            }
        }

        /// <summary>
        /// 관문이 낸 거절인가. NGO는 트랜스포트가 끊겨도 DisconnectReason에 문장을 채운다("[Disconnect Event From Transport]…") —
        /// 그걸 거절로 적으면 호스트가 뜨기 전에 두드리던 클론이 **거절당했다고 믿고 멈춘다**(2026-09-14 qa_v56 ②에서 잡힘).
        /// </summary>
        public static bool IsGateCode(string _reason)
        {
            return _reason == CLOSED || _reason == FULL || _reason == PW_REQUIRED || _reason == PW_WRONG;
        }

        /// <summary>비밀번호를 물어 다시 시도할 사유인가 — 틀렸을 때도 한 번 더 기회를 준다.</summary>
        public static bool AsksPassword(string _reason)
        {
            return _reason == PW_REQUIRED || _reason == PW_WRONG;
        }
    }
}
