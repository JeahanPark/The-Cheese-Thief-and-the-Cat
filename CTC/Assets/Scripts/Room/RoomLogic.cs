using CTC.Runtime.Networking;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실 판정 규칙 — NetworkBehaviour와 분리된 순수 함수(EditMode TDD).
    ///
    /// 준비(Ready) 체크는 **없다**(기획 2026-08-26). 방장이 시작을 누르는 것으로 끝이라
    /// 시작 조건은 "사람이 둘 이상"과 "누른 사람이 방장" 둘뿐이다.
    /// </summary>
    public static class RoomLogic
    {
        /// <summary>고양이 하나와 쥐 하나. 이보다 적으면 판이 성립하지 않는다.</summary>
        public const int MIN_PLAYERS = 2;

        /// <summary>스팀 방 정원과 같은 값을 쓴다 — 두 곳에 숫자를 박으면 갈라진다.</summary>
        public const int MAX_PLAYERS = SteamLobbyLogic.MAX_PLAYERS;

        /// <summary>고양이는 1명(2026-08-26 확정). 나머지는 전원 쥐.</summary>
        public const int CAT_COUNT = 1;

        public static bool CanStart(int _memberCount, bool _isHost)
        {
            return _isHost && _memberCount >= MIN_PLAYERS;
        }

        /// <summary>시작 버튼 옆에 뜨는 안내 — 못 누르는 이유를 사람 말로 준다.</summary>
        public static string StartHint(int _memberCount, bool _isHost)
        {
            if (!_isHost) return "방장이 시작하기를 기다리는 중";
            if (_memberCount < MIN_PLAYERS) return $"{MIN_PLAYERS}명부터 시작할 수 있습니다";
            return "고양이 1명은 시작할 때 무작위로 정해집니다";
        }

        public static string CountLabel(int _memberCount) => $"{_memberCount} / {MAX_PLAYERS}";

        /// <summary>이름이 비면 자리번호로 대신한다 — 빈 이름표가 뜨는 것보다 낫다.</summary>
        public static string DisplayName(string _name, int _index)
        {
            return string.IsNullOrWhiteSpace(_name) ? $"플레이어 {_index + 1}" : _name.Trim();
        }

        /// <summary>
        /// 참가자를 원형으로 세울 자리. 대기실은 맵이 없어 자리 공식이 여기 하나뿐이다 —
        /// 인원이 늘어도 반지름만 커지고 서로 겹치지 않는다.
        /// </summary>
        public static UnityEngine.Vector3 StandPosition(int _index, int _count, float _radius)
        {
            if (_count <= 0) return UnityEngine.Vector3.zero;

            float step = 360f / _count;
            float yaw = step * _index;
            UnityEngine.Vector3 dir = UnityEngine.Quaternion.Euler(0f, yaw, 0f) * UnityEngine.Vector3.forward;
            return dir * _radius;
        }

        /// <summary>선 자리에서 안쪽(원 중심)을 본다.</summary>
        public static float StandYaw(int _index, int _count)
        {
            if (_count <= 0) return 0f;
            return 360f / _count * _index + 180f;
        }
    }
}
