using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 판이 끝나 대기실로 돌아올 때 **방 설정(봇 수·맵)을 되살리는** 서버 전용 쪽지(2026-09-19).
    ///
    /// 돌아온 대기실의 <see cref="RoomSession"/>은 씬과 함께 새로 스폰돼 봇 0·기본 맵으로 선다.
    /// 그래서 혼자+봇으로 한 판 하고 돌아오면 **시작을 눌러도 아무 일도 안 일어났다**(사용자 보고).
    /// 씬 사이를 건너는 건 정적 상태뿐이라 여기 적는다(<see cref="Core.MatchHandoff"/>와 같은 이유).
    ///
    /// **판에서 돌아올 때만 한 번 되살린다** — 메인에서 새로 연 방은 기본값으로 선다.
    /// 적기는 시작하는 순간(<see cref="RoomDirector"/>), 표시는 대기실을 부르는 순간(`MatchRoomReturn`),
    /// 꺼내기는 돌아온 대기실이 스폰될 때(<see cref="RoomSession"/>)다.
    /// </summary>
    public static class RoomReturnMemo
    {
        private static bool s_HasMemo;
        private static bool s_Returning;
        private static int s_BotCount;
        private static int s_MapIndex;

        /// <summary>시작하는 순간의 방 설정을 적는다. 새 판으로 가는 길이라 복귀 표시는 지운다.</summary>
        public static void Remember(int _botCount, int _mapIndex)
        {
            s_HasMemo = true;
            s_Returning = false;
            s_BotCount = Mathf.Max(0, _botCount);
            s_MapIndex = Mathf.Max(0, _mapIndex);
        }

        /// <summary>판이 끝나 대기실을 부르는 순간 — 이 표시가 있어야 돌아온 대기실이 설정을 꺼낸다.</summary>
        public static void MarkReturning()
        {
            s_Returning = s_HasMemo;
        }

        /// <summary>돌아온 대기실이 **한 번** 꺼내 간다. 판에서 돌아온 게 아니면 false(기본값으로 선다).</summary>
        public static bool TryConsume(out int _botCount, out int _mapIndex)
        {
            _botCount = s_BotCount;
            _mapIndex = s_MapIndex;
            if (!s_Returning) return false;

            s_Returning = false;
            s_HasMemo = false;
            return true;
        }

        /// <summary>도메인 리로드를 끈 채로 플레이하면 옛 판의 값이 남는다 — 진입마다 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_HasMemo = false;
            s_Returning = false;
            s_BotCount = 0;
            s_MapIndex = 0;
        }
    }
}
