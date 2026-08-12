using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>지휘관이 내리는 명령의 종류.</summary>
    public enum BotOrderId
    {
        /// <summary>명령 없음 — 봇이 알아서 한다.</summary>
        None = 0,

        /// <summary>맡은 구역을 훑는다.</summary>
        Search = 1,

        /// <summary>이 소리를 확인한다.</summary>
        Investigate = 2,

        /// <summary>감옥 앞을 지킨다 — 구하러 오는 쥐를 잡는다.</summary>
        GuardJail = 3,
    }

    /// <summary>
    /// 지휘관이 명령을 정하는 규칙(순수).
    ///
    /// 기술소개서(리그오브레전드 10p)의 명령계통구조를 옮긴 것이다. 원본은 지휘관 트리가
    /// `TEAM_ORDER`를 챔피언 트리에 써 넣고, 챔피언은 **개인행동 → 지휘관명령 → Idle** 순으로
    /// 본다. 눈앞의 일은 스스로 처리하고, 그게 없을 때만 명령을 따른다.
    ///
    /// **지휘관은 쥐 좌표를 모른다.** 고양이들이 실제로 보고 들은 것만 받는다 —
    /// 서버가 전지적이라 그냥 읽을 수 있지만, 그러면 봇 승률이 "밸런스가 좋다"가 아니라
    /// "봇이 반칙했다"를 뜻하게 된다. 사람 둘이 채팅으로 공유하는 딱 그만큼만 안다.
    /// </summary>
    public static class BotOrderLogic
    {
        /// <summary>
        /// `_index`번 고양이가 맡을 구역의 중심.
        ///
        /// 맵을 세로로 `_count`등분한다. 둘이면 왼쪽·오른쪽이 되고, 그것만으로도
        /// **둘이 한 몸처럼 붙어 다니는 것**이 사라진다(2026-08-12 실측: 목표가 늘 같았다).
        /// </summary>
        public static Vector3 ZoneCenter(int _index, int _count, float _halfExtent)
        {
            if (_count <= 1) return Vector3.zero;

            int index = Mathf.Clamp(_index, 0, _count - 1);
            float width = _halfExtent * 2f / _count;
            float x = -_halfExtent + width * (index + 0.5f);
            return new Vector3(x, 0f, 0f);
        }

        /// <summary>
        /// 구역 안에서 훑을 자리. 같은 자리만 오가면 반쪽만 보게 되므로 씨앗으로 흩는다.
        /// </summary>
        public static Vector3 SearchPoint(Vector3 _zoneCenter, float _halfExtent, int _count, System.Random _rng)
        {
            if (_rng == null) return _zoneCenter;

            float halfWidth = _count > 0 ? _halfExtent / _count : _halfExtent;
            float x = _zoneCenter.x + (float)(_rng.NextDouble() * 2.0 - 1.0) * halfWidth;
            float z = (float)(_rng.NextDouble() * 2.0 - 1.0) * _halfExtent;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 감옥을 지킬 때인가. **갇힌 쥐가 있고 아직 자유로운 쥐가 남았을 때**만이다 —
        /// 전부 갇혔으면 판이 곧 끝나므로 지킬 이유가 없고, 아무도 안 갇혔으면 지킬 게 없다.
        /// </summary>
        public static bool ShouldGuardJail(int _jailedMice, int _freeMice)
        {
            return _jailedMice > 0 && _freeMice > 0;
        }

        /// <summary>
        /// 고양이가 둘 이상일 때 **한 마리만** 감옥을 맡는다.
        /// 둘 다 지키면 맵이 통째로 빈다.
        /// </summary>
        public static bool IsJailGuard(int _index, int _catCount)
        {
            return _catCount > 1 && _index == 0;
        }
    }
}
