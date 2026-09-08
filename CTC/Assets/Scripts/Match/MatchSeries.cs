using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using CTC.Runtime.Room;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 3판 시리즈 진행 (2026-08-30). `LobbyController` GO에 동승한다.
    ///
    /// 한 판이 끝나면 결과를 잠깐 보여 주고 **다음 판을 서버가 알아서 연다.**
    /// 3판을 다 하면 대기실로 돌아간다. 사람이 "재시작"을 누를 자리가 없다 —
    /// 시리즈가 곧 한 경기이기 때문이다.
    ///
    /// **판마다 고양이를 다시 뽑는다.** 직전 고양이는 후보에서 빠지므로(참가자 3명 이상)
    /// 3판이면 대체로 세 사람이 돌아가며 고양이를 해 본다.
    ///
    /// **대기실에서 온 판에서만 돈다.** `Game.unity`를 혼자 열었을 때(wesqa·MPPM)는
    /// 아무 일도 안 한다 — 그쪽은 지금까지처럼 사람이 재시작을 누르는 흐름이다.
    /// </summary>
    public class MatchSeries : NetworkBehaviour
    {
        /// <summary>판이 끝나고 결과를 보여 주는 시간(초).</summary>
        private const float RESULT_SECONDS = 5f;

        /// <summary>마지막 판 뒤 대기실로 돌아가기 전에 시리즈 결과를 보여 주는 시간(초).</summary>
        private const float FINAL_SECONDS = 7f;

        /// <summary>판이 서고 스폰이 끝나기를 기다렸다 봇을 세운다(ms). `MatchAutoStart`와 같은 값.</summary>
        private const int BOT_SPAWN_DELAY_MS = 1000;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private MatchManager m_Match;
        [SerializeField] private string m_RoomSceneName = "Room";

        private readonly NetworkVariable<int> m_Round = new NetworkVariable<int>(1);
        private readonly NetworkVariable<int> m_CatWins = new NetworkVariable<int>();
        private readonly NetworkVariable<int> m_MouseWins = new NetworkVariable<int>();

        /// <summary>이 판이 시리즈인가 — 아니면 HUD가 아예 안 뜬다.</summary>
        private readonly NetworkVariable<bool> m_Active = new NetworkVariable<bool>();

        private bool m_Handling;

        public bool Active => m_Active.Value;
        public int Round => m_Round.Value;
        public int CatWins => m_CatWins.Value;
        public int MouseWins => m_MouseWins.Value;

        /// <summary>판·점수가 바뀔 때마다 울린다(View용).</summary>
        public Observable<Unit> Changed => Observable.Merge(
            m_Active.AsObservable().AsUnitObservable(),
            m_Round.AsObservable().AsUnitObservable(),
            m_CatWins.AsObservable().AsUnitObservable(),
            m_MouseWins.AsObservable().AsUnitObservable()).Prepend(Unit.Default);

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // 대기실에서 온 판만 시리즈로 돈다. 쪽지가 비어 있으면(단독 실행) 손대지 않는다.
            m_Active.Value = MatchHandoff.FromRoom;
            if (!m_Active.Value || m_Match == null) return;

            m_Match.ResultChanged
                .Where(r => r != MatchResult.None)
                .Subscribe(OnRoundEnded)
                .AddTo(this);
        }

        private void OnRoundEnded(MatchResult _result)
        {
            if (!IsServer || m_Handling) return;

            m_Handling = true;
            if (_result == MatchResult.CatWin) m_CatWins.Value++;
            else m_MouseWins.Value++;

            GameDebug.Log($"[Series] {m_Round.Value}판 종료: {_result} → " +
                          $"고양이 {m_CatWins.Value} : 쥐 {m_MouseWins.Value}");

            Advance().Forget();
        }

        private async UniTask Advance()
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            bool final = MatchSeriesLogic.IsFinalRound(m_Round.Value);

            // 결과를 읽을 시간을 준다. 마지막 판은 시리즈 총점까지 보여야 하니 더 길다.
            await UniTask.Delay((int)((final ? FINAL_SECONDS : RESULT_SECONDS) * 1000f),
                cancellationToken: cancel);

            if (!IsServer) return;

            if (final) ReturnToRoom();
            else await StartNextRound(cancel);
        }

        /// <summary>
        /// 다음 판을 연다. 순서가 네 번 중요하다.
        ///
        /// 1. **역할을 먼저 앉히고 시작을 부른다** — `RequestStartGame`이 그 역할표를 보고 몸을 세운다.
        /// 2. **재시작과 시작 사이에 한 프레임을 준다.** `MatchManager`는 "시작됨"이 false로
        ///    떨어지는 순간을 봐야 타이머를 다시 채운다. 같은 프레임에 껐다 켜면 그 순간을 못 봐
        ///    타이머가 0에 머물고 **2판부터 시작하자마자 시간 만료로 끝난다**(2026-08-30 실측).
        /// 3. **봇 수를 스윕 전에 집어 둔다.** `MatchHandoff.Set`의 봇 인자를 생략하면 0으로 덮여
        ///    2판부터 봇이 통째로 사라졌다 — 3판제가 2판째부터 다른 게임이 됐다(감사 P0-4).
        /// 4. **판이 안 열리면 대기실로 돌아간다.** 사람이 빠져 시작 조건이 깨지면 예전엔 조용히
        ///    아무 일도 안 했고, `FromRoom`이라 로비 패널도 결과 패널도 없어 **빈 씬에 갇혔다**(감사 P0-6).
        /// </summary>
        private async UniTask StartNextRound(System.Threading.CancellationToken _cancel)
        {
            if (m_Lobby == null || m_Match == null) return;

            // 직전 고양이가 봇이면 사람 역할표에는 고양이가 없다 — 쪽지 쪽 값이 그때의 진실이다.
            ulong previousCat = CatDrawLogic.IsBot(MatchHandoff.PreviousCat)
                ? MatchHandoff.PreviousCat
                : CurrentCat();

            // 봇 수는 쪽지에 남아 있다(`ConsumeRoles`는 역할만 비운다). 아래 `Set`이 덮기 전에 집는다.
            int totalBots = MatchHandoff.BotMice + MatchHandoff.BotCats;

            // 스윕: 동적 오브젝트를 전부 걷고 로비 상태로 되돌린다(역할은 남는다). 봇의 몸도 여기서 걷힌다.
            m_Match.RequestRestart();

            await UniTask.NextFrame(_cancel);
            if (!IsServer) return;

            List<ulong> members = MemberIds();
            int botMice = MatchHandoff.BotMice;
            int botCats = MatchHandoff.BotCats;
            if (members.Count > 0)
            {
                // 대기실과 같은 규칙 — **봇 자리도 후보다**(`RoomDirector.StartMatch`).
                // 사람만 뽑으면 혼자+봇일 때 사람이 늘 고양이가 된다.
                List<ulong> pool = CatDrawLogic.WithBots(members, totalBots);
                ulong cat = CatDrawLogic.Pick(
                    pool, previousCat, Random.Range(0, pool.Count * 7 + 1), previousCat);
                (botMice, botCats) = CatDrawLogic.BotSplit(totalBots, cat);

                m_Lobby.ServerAssignRoles(CatDrawLogic.RoleTable(members, cat));
                MatchHandoff.Set(CatDrawLogic.RoleTable(members, cat), m_Lobby.MapIndex, cat, botMice, botCats);
                GameDebug.Log($"[Series] {MatchSeriesLogic.NextRound(m_Round.Value)}판 고양이 → client={cat}" +
                              $" · 봇 쥐 {botMice} · 봇 고양이 {botCats}");
            }

            m_Round.Value = MatchSeriesLogic.NextRound(m_Round.Value);

            if (!BeginRound(botMice + botCats > 0))
            {
                GameDebug.LogWarning(
                    $"[Series] {m_Round.Value}판을 열 수 없다(시작 조건 미달 — 사람 {members.Count}명) — 대기실로 돌아간다");
                ReturnToRoom();
                return;
            }

            await SpawnBotsForRound(botMice, botCats, _cancel);
            m_Handling = false;
        }

        /// <summary>
        /// 판을 연다. **봇이 낀 판은 인원 조건을 건너뛴다** — 시작 조건은 대기실 사람만 세는데
        /// 봇은 인게임에서 서므로, 혼자+봇이면 쥐 0명으로 판정돼 시작이 거부된다
        /// (`MatchAutoStart`와 같은 사정, 2026-09-03 실측).
        /// </summary>
        private bool BeginRound(bool _hasBots)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hasBots) return m_Lobby.DevForceStartGame();
#endif
            return m_Lobby.RequestStartGame();
        }

        /// <summary>
        /// 다음 판의 봇을 다시 세운다. `RequestRestart`가 1판의 봇 몸을 걷었으므로 **매 판 다시 세워야 한다** —
        /// `MatchAutoStart`는 씬 진입에 한 번만 도므로 2·3판은 아무도 안 세운다(감사 P0-4).
        /// 판이 서고 스폰이 끝난 뒤에 세운다 — 먼저 세우면 매치 시작이 봇을 다시 배치하며 겹친다.
        /// </summary>
        private async UniTask SpawnBotsForRound(int _mice, int _cats, System.Threading.CancellationToken _cancel)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_mice + _cats <= 0) return;

            await UniTask.Delay(BOT_SPAWN_DELAY_MS, cancellationToken: _cancel);
            if (!IsServer) return;

            var bots = Bots.TestBotManager.Instance;
            if (bots == null)
            {
                GameDebug.LogWarning("[Series] TestBotManager가 없다 — 봇을 못 세운다");
                return;
            }
            bots.ServerSpawn(_mice, _cats);
            GameDebug.Log($"[Series] 봇 쥐 {_mice} · 봇 고양이 {_cats} 재투입");
#else
            await UniTask.CompletedTask;
#endif
        }

        /// <summary>
        /// 대기실로 돌아간다. **NGO의 SceneManager로만** 넘긴다 —
        /// 유니티 쪽 로드는 클라이언트가 안 따라온다.
        /// </summary>
        private void ReturnToRoom()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            GameDebug.Log($"[Series] {MatchSeriesLogic.FinalLabel(m_CatWins.Value, m_MouseWins.Value)}");

            // 역할은 다 쓰였다. 직전 고양이는 남겨 다음 시리즈의 재추첨에서 뺀다.
            MatchHandoff.ConsumeRoles();

            var status = nm.SceneManager.LoadScene(
                m_RoomSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                GameDebug.LogError($"[Series] 대기실 복귀 실패: {status}");
                m_Handling = false;
            }
        }

        private ulong CurrentCat()
        {
            for (int i = 0; i < m_Lobby.PlayerCount; i++)
            {
                LobbyPlayerState p = m_Lobby.GetPlayer(i);
                if (p.Role == RoleId.Cat) return p.ClientId;
            }
            return 0UL;
        }

        private List<ulong> MemberIds()
        {
            var ids = new List<ulong>(m_Lobby.PlayerCount);
            for (int i = 0; i < m_Lobby.PlayerCount; i++) ids.Add(m_Lobby.GetPlayer(i).ClientId);
            return ids;
        }
    }
}
