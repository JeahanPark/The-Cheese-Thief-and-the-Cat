using System.Text;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 방 비밀번호 규칙 — 순수 함수(EditMode TDD, 2026-09-14). R.E.P.O.와 같다:
    /// **최대 10자 · 공백 없음 · 대문자.** 대소문자를 가르면 말로 불러 준 비밀번호가 안 맞는다.
    /// </summary>
    public static class LobbyPasswordLogic
    {
        public const int MAX_LENGTH = 10;

        /// <summary>공백을 지우고 대문자로, 넘치면 자른다. null은 빈 문자열.</summary>
        public static string Normalize(string _raw)
        {
            if (string.IsNullOrEmpty(_raw)) return "";

            var sb = new StringBuilder(MAX_LENGTH);
            foreach (char c in _raw)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                sb.Append(char.ToUpperInvariant(c));
                if (sb.Length >= MAX_LENGTH) break;
            }
            return sb.ToString();
        }

        public static bool HasPassword(string _raw) => Normalize(_raw).Length > 0;

        /// <summary>접속 요청에 실어 보낼 바이트. 비밀번호가 없으면 빈 배열.</summary>
        public static byte[] ToPayload(string _raw) => Encoding.UTF8.GetBytes(Normalize(_raw));

        public static string FromPayload(byte[] _payload)
        {
            if (_payload == null || _payload.Length == 0) return "";
            // 남이 보낸 바이트다 — 길이를 먼저 막는다(10자 한글이어도 30바이트면 충분하다).
            int length = _payload.Length > MAX_LENGTH * 4 ? MAX_LENGTH * 4 : _payload.Length;
            try
            {
                return Normalize(Encoding.UTF8.GetString(_payload, 0, length));
            }
            catch (System.ArgumentException)
            {
                return "";
            }
        }
    }
}
