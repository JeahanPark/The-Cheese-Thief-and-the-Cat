using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 대기실에서 넘어온 판을 **서버가 알아서 연다** (2026-08-30).
    ///
    /// 대기실이 이미 고양이를 뽑고 맵을 정해 <see cref="MatchHandoff"/>에 놓았으므로, 여기서는
    /// 그 값을 <see cref="LobbySession"/>·<see cref="Map.MapDirector"/>에 얹고 판을 여는 일만 한다.
    /// 인게임 화면에 로비 UI가 다시 뜨면 안 되기 때문에 사람이 한 번 더 누를 자리가 없다.
    ///
    /// **쪽지가 비어 있으면 아무 일도 안 한다** — `Game.unity`를 혼자 열었을 때(wesqa·MPPM)는
    /// 지금까지처럼 로비 UI에서 역할을 고르고 시작을 누른다.
    /// </summary>
    public class MatchAutoStart : NetworkBehaviour
    {
        /// <summary>참가자가 다 붙을 때까지 기다리는 상한(초). 넘으면 있는 사람만으로 연다.</summary>
        private const float JOIN_TIMEOUT_SECONDS = 10f;
        private const int POLL_MS = 200;

        [SerializeField] private LobbyController m_Lobby;

        public override void OnNetworkSpawn()
        {
            if (!IsServer || !MatchHandoff.HasRoles) return;
            Run().Forget();
        }

        private async UniTask Run()
        {
            int expected = MatchHandoff.RoleCount;
            var cancel = this.GetCancellationTokenOnDestroy();

            // **한 프레임 양보하고 시작한다.** 이 메서드는 `OnNetworkSpawn`에서 불리는데, 같은
            // NetworkObject에 동승한 `MapDirector`는 아직 스폰 전일 수 있다. 그 상태로 맵을 고르면
            // NGO가 "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet"를
            // 뱉는다(2026-08-30 실측). 한 프레임이면 동승자 전원이 선다.
            await UniTask.DelayFrame(1, cancellationToken: cancel);

            // 씬 전환 직후에는 클라이언트가 아직 다 안 붙었다 — 인원이 차거나 시간이 다 될 때까지 기다린다.
            float deadline = Time.time + JOIN_TIMEOUT_SECONDS;
            while (Time.time < deadline)
            {
                if (m_Lobby != null && m_Lobby.PlayerCount >= expected) break;
                await UniTask.Delay(POLL_MS, cancellationToken: cancel);
            }

            if (m_Lobby == null)
            {
                GameDebug.LogError("[AutoStart] LobbyController가 안 붙어 있다 — 판을 못 연다");
                return;
            }

            if (MatchHandoff.MapIndex >= 0) m_Lobby.SelectMap(MatchHandoff.MapIndex);

            // 맵 선택이 레이아웃·요새자리를 갈아 끼운다 — 스폰 공식이 그걸 읽으므로 한 프레임 준다.
            await UniTask.Yield(cancel);

            GameDebug.Log($"[AutoStart] 대기실에서 넘어온 판을 연다 — {m_Lobby.PlayerCount}명");

            // 봇을 채운 판은 **인원 조건을 건너뛴다**(2026-09-03 실측 버그).
            // 시작 조건은 대기실 사람만 세는데 봇은 인게임에서 서므로, 혼자+봇이면 쥐 0명으로
            // 판정돼 시작이 거부되고 **내 몸조차 안 생긴다**(조작 불가로 보였다).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (MatchHandoff.BotMice > 0) m_Lobby.DevForceStartGame();
            else m_Lobby.RequestStartGame();
#else
            m_Lobby.RequestStartGame();
#endif

            // 대기실에서 채운 봇 쥐(2026-09-01). 판이 서고 스폰이 끝난 뒤에 세운다 —
            // 먼저 세우면 매치 시작이 봇을 다시 배치하며 겹친다. 개발 빌드에만 존재한다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (MatchHandoff.BotMice > 0)
            {
                await UniTask.Delay(1000, cancellationToken: cancel);
                var bots = Bots.TestBotManager.Instance;
                if (bots != null)
                {
                    bots.ServerSpawn(MatchHandoff.BotMice, 0);
                    GameDebug.Log($"[AutoStart] 봇 쥐 {MatchHandoff.BotMice}마리 투입");
                }
                else GameDebug.LogWarning("[AutoStart] TestBotManager가 없다 — 봇을 못 세운다");
            }
#endif

            // 역할은 다 쓰였다. 직전 고양이는 남긴다 — 재시작 때 연속 당첨을 빼는 데 쓴다.
            MatchHandoff.ConsumeRoles();
        }
    }
}
