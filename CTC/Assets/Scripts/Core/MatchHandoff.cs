using System.Collections.Generic;
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 대기실이 정한 것을 인게임 씬으로 넘기는 **서버 전용 쪽지**(2026-08-30).
    ///
    /// 왜 NetworkVariable이 아닌가: 씬이 바뀌는 그 순간에 살아 있어야 하는데, 대기실의
    /// NetworkObject는 씬과 함께 사라지고 인게임의 것은 아직 스폰 전이다. 그 틈에서
    /// 값을 나를 수 있는 건 씬을 타지 않는 정적 상태뿐이다. **복제하지 않는다** —
    /// 서버만 읽고, 클라이언트는 인게임에서 복제된 `LobbySession`으로 결과를 본다.
    ///
    /// 비어 있으면 아무 일도 안 한다 — `Game.unity`를 혼자 열었을 때(wesqa·MPPM)의 흐름은
    /// 지금까지와 완전히 같다.
    /// </summary>
    public static class MatchHandoff
    {
        private static readonly Dictionary<ulong, RoleId> s_Roles = new Dictionary<ulong, RoleId>();

        /// <summary>대기실에서 고른 맵. -1이면 인게임 기본값을 쓴다.</summary>
        public static int MapIndex { get; private set; } = -1;

        /// <summary>직전 판의 고양이 — 다음 추첨에서 뺀다(연속 당첨 방지).</summary>
        public static ulong PreviousCat { get; private set; }

        /// <summary>대기실에서 채운 봇 쥐 수(개발 빌드 전용). 인게임이 판을 연 뒤 세운다.</summary>
        public static int BotMice { get; private set; }

        /// <summary>봇 고양이 수(0 또는 1) — 추첨에서 봇 자리가 걸렸을 때(2026-09-06).</summary>
        public static int BotCats { get; private set; }

        public static bool HasBots => BotMice + BotCats > 0;

        /// <summary>넘길 것이 있는가. 없으면 인게임은 예전 흐름 그대로 돈다.</summary>
        public static bool HasRoles => s_Roles.Count > 0;

        /// <summary>
        /// 이 판이 대기실에서 왔는가. 역할을 다 쓰고 비운 뒤에도 남는다 —
        /// 인게임의 옛 로비 패널(역할 선택·시작)을 판이 끝날 때까지 계속 숨겨야 하기 때문이다.
        /// `Game.unity`를 혼자 열면 false라 그 화면은 지금까지처럼 뜬다.
        /// </summary>
        public static bool FromRoom { get; private set; }

        public static int RoleCount => s_Roles.Count;

        /// <summary>모르는 클라이언트는 <see cref="RoleId.None"/> — 몸을 안 준다(관전).</summary>
        public static RoleId RoleOf(ulong _clientId)
        {
            return s_Roles.TryGetValue(_clientId, out RoleId role) ? role : RoleId.None;
        }

        /// <summary>서버가 추첨 직후 한 번 채운다.</summary>
        public static void Set(IReadOnlyDictionary<ulong, RoleId> _roles, int _mapIndex, ulong _cat,
            int _botMice = 0, int _botCats = 0)
        {
            BotMice = Mathf.Max(0, _botMice);
            BotCats = Mathf.Max(0, _botCats);
            s_Roles.Clear();
            if (_roles != null)
            {
                foreach (KeyValuePair<ulong, RoleId> pair in _roles) s_Roles[pair.Key] = pair.Value;
            }

            MapIndex = _mapIndex;
            PreviousCat = _cat;
            FromRoom = true;
            GameDebug.Log($"[Handoff] 역할 {s_Roles.Count}명 · 맵 {_mapIndex} · 고양이 {_cat} · 봇 쥐 {BotMice} · 봇 고양이 {BotCats}");
        }

        /// <summary>
        /// 인게임이 다 받아 간 뒤 비운다. **역할만 비우고 직전 고양이는 남긴다** —
        /// 재시작 때 다시 뽑을 때 그게 있어야 연속 당첨을 뺄 수 있다.
        /// </summary>
        public static void ConsumeRoles()
        {
            s_Roles.Clear();
            MapIndex = -1;
        }

        /// <summary>도메인 리로드를 끈 채로 플레이하면 옛 판의 값이 남는다 — 진입마다 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Roles.Clear();
            MapIndex = -1;
            PreviousCat = 0;
            FromRoom = false;
            BotMice = 0;
            BotCats = 0;
        }
    }
}
