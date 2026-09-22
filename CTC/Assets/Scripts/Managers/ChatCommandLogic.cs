// 배포 빌드에는 통째로 없다 — 참조하는 곳(ChatHud 명령부·ChatManager 전송부·EditMode 테스트)이
// 전부 같은 가드 안에 있다. 예약어 문자열 하나도 배포 바이너리에 남기지 않는다(2026-08-20).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace CTC.Runtime.Managers
{
    /// <summary>
    /// 채팅 명령어 판정 (순수 로직 — EditMode가 덮는다).
    ///
    /// 채팅창에 `/`를 치면 예약어 목록이 힌트로 뜨고(2026-08-20 건의), 완성해 보내면
    /// 채팅 대신 명령이 실행된다. 첫 명령은 `/send log` — 테스터가 로그 파일을 복사해
    /// 메신저로 나르던 것을 채팅 한 줄로 줄인다.
    ///
    /// 모르는 `/문자열`은 명령이 아니라 **그냥 채팅으로 보낸다** — 오타를 삼키면
    /// "보냈는데 아무 일도 없는" 상태가 된다.
    /// </summary>
    public static class ChatCommandLogic
    {
        public readonly struct Command
        {
            public string Name { get; }
            public string Description { get; }

            public Command(string _name, string _description)
            {
                Name = _name;
                Description = _description;
            }
        }

        public static readonly Command[] ALL =
        {
            new Command("/send log", "내 로그 파일을 호스트에게 보낸다"),
        };

        /// <summary>입력이 예약어 힌트를 띄울 상태인가 — `/`로 시작하는 동안 참.</summary>
        public static List<Command> Match(string _input)
        {
            var result = new List<Command>();
            if (string.IsNullOrEmpty(_input)) return result;

            string typed = Normalize(_input);
            if (typed.Length == 0 || typed[0] != '/') return result;

            foreach (Command cmd in ALL)
            {
                // 치는 중(접두)이거나 다 쳤거나 — 둘 다 힌트를 유지한다
                if (cmd.Name.StartsWith(typed, System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(cmd);
                }
            }
            return result;
        }

        /// <summary>
        /// 힌트에서 고른 줄을 위아래로 옮긴다(2026-09-23 건의) — 끝에서 누르면 반대쪽 끝으로 돈다.
        /// 후보가 없으면 0.
        /// </summary>
        public static int MoveSelection(int _current, int _delta, int _count)
        {
            if (_count <= 0) return 0;
            int next = (_current + _delta) % _count;
            return next < 0 ? next + _count : next;
        }

        /// <summary>
        /// 이미 어떤 예약어를 끝까지 쳤는가 — 그러면 Enter는 채우기가 아니라 **실행**이다.
        /// 치는 중(`/se`)에 Enter를 누르면 고른 줄로 채우기만 한다 — 반쪽 명령이 채팅으로 새지 않게.
        /// </summary>
        public static bool IsComplete(string _input)
        {
            string typed = Normalize(_input);
            foreach (Command cmd in ALL)
            {
                if (cmd.Name.Equals(typed, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsSendLog(string _input) =>
            Normalize(_input).Equals("/send log", System.StringComparison.OrdinalIgnoreCase);

        // 앞뒤 공백을 버리고 연속 공백을 하나로 — "/send  log"도 같은 명령이다
        private static string Normalize(string _input)
        {
            if (string.IsNullOrWhiteSpace(_input)) return "";
            return string.Join(" ", _input.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
        }

        // ---- 로그 전송 조각 계산 (ChatManager가 쓴다) ----

        /// <summary>파일 끝에서 상한만큼만 보낸다 — 에러는 끝에 있다.</summary>
        public static long TailStart(long _fileLength, int _capBytes) =>
            _fileLength <= _capBytes ? 0L : _fileLength - _capBytes;

        public static int ChunkCount(int _totalBytes, int _chunkBytes) =>
            _totalBytes <= 0 ? 0 : (_totalBytes + _chunkBytes - 1) / _chunkBytes;
    }
}
#endif
