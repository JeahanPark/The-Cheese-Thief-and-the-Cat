using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실 봇 자리의 순수 판정(2026-09-01) — 혼자서도 판을 열어 보려고 만든다.
    /// 봇은 **쥐만** 채운다: 고양이는 추첨으로 사람에게 가야 하고, 봇 고양이는 실험실(BotLab)의 몫이다.
    /// </summary>
    public static class RoomBotLogic
    {
        /// <summary>남은 자리에 맞춘 봇 수 — 사람이 들어오면 자동으로 줄어든다.</summary>
        public static int ClampBots(int _requested, int _humanCount)
        {
            int seats = Mathf.Max(0, RoomLogic.MAX_PLAYERS - _humanCount);
            return Mathf.Clamp(_requested, 0, seats);
        }

        /// <summary>봇을 사람 수에 더해 시작 조건을 본다.</summary>
        public static bool CanStartWithBots(int _humanCount, int _botCount, bool _isHost)
        {
            return RoomLogic.CanStart(_humanCount + Mathf.Max(0, _botCount), _isHost);
        }

        /// <summary>버튼 옆 한 줄 — 봇을 포함한 총원.</summary>
        public static string BotHint(int _humanCount, int _botCount)
        {
            int bots = Mathf.Max(0, _botCount);
            return bots == 0
                ? "봇 없음"
                : $"봇 쥐 {bots} · 총 {_humanCount + bots}명으로 시작";
        }
    }
}
