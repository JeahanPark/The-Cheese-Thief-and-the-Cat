using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 대기실의 진행을 맡는 Worker(규칙 6) — 몸 세우기 · 고양이 추첨 · 인게임 씬 전환.
    ///
    /// **뽑는 것은 서버뿐이다.** 클라이언트는 <see cref="RoomSession.CatDrawn"/>으로 결과만 받는다.
    /// 뽑은 역할표는 <see cref="MatchHandoff"/>에 놓고 씬을 넘긴다 — 대기실의 NetworkObject는
    /// 씬과 함께 사라지고 인게임의 것은 아직 스폰 전이라, 그 틈을 건널 수 있는 건 정적 상태뿐이다.
    /// </summary>
    public class RoomDirector : MonoBehaviour
    {
        /// <summary>
        /// 씬을 넘기기 전에 추첨 연출이 도는 시간(초). <see cref="RoomDrawStage"/>의 타임라인과
        /// **같은 값을 쓴다** — 어긋나면 연출이 덜 끝난 채 넘어가거나 검은 화면이 오래 남는다.
        /// </summary>
        private const float REVEAL_SECONDS = RoomDrawStage.TOTAL_SECONDS;

        [SerializeField] private string m_GameSceneName = "Game";

        private RoomSession m_Session;
        private RoomAvatarSpawner m_Spawner;
        private System.IDisposable m_Subscription;
        private bool m_Handing;

        private void Awake()
        {
            m_Session = GetComponent<RoomSession>();
            m_Spawner = GetComponent<RoomAvatarSpawner>();
        }

        /// <summary>
        /// 참가자가 바뀔 때만 몸을 맞춘다 — 매 프레임 목록을 훑지 않는다(규칙 3).
        ///
        /// **`OnEnable`이 아니라 `Start`다.** 같은 오브젝트의 `RoomSession.Awake`가 NetworkList를
        /// 만들기 전에 구독하면 `ViewChanged`가 대체용 1회성 스트림을 내주고, 그게 곧바로 완료돼
        /// **구독이 죽는다** — 참가자가 들어와도 몸이 영영 안 선다(2026-08-30 실측:
        /// 보드에는 이름이 뜨는데 3D에는 아무도 없었다. 보드는 Start에서 구독하고 있었다).
        /// </summary>
        private void Start()
        {
            if (m_Session == null)
            {
                GameDebug.LogError("[Room] RoomSession이 같은 오브젝트에 없다");
                return;
            }

            m_Subscription = m_Session.ViewChanged.Subscribe(_ => SyncBodies());

            // 대기실이 섰다 — 이제 들어와도 된다(2026-09-14, R.E.P.O. `LobbyMenuOpen`과 같은 자리).
            // 방을 만든 순간이 아니라 여기서 여는 이유: 씬이 넘어가는 사이에 들어온 사람은 빈 화면을 본다.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer) Networking.ConnectionGate.SetAccepting(true);
        }

        private void OnDestroy()
        {
            m_Subscription?.Dispose(); // 순수 C# 객체 — 규칙 9 예외
            m_Subscription = null;
        }

        private void SyncBodies()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || m_Session == null || m_Session.Starting) return;
            if (m_Spawner != null) m_Spawner.ServerSync();
        }

        /// <summary>
        /// 방장이 시작을 눌렀다 — 서버에서만 실제로 돈다.
        /// 인원 조건은 <see cref="RoomLogic.CanStart"/>가 판정하고, 여기서는 절차만 진행한다.
        /// </summary>
        public void ServerStartMatch()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || m_Session == null) return;
            if (!m_Session.CanStart || m_Handing) return;

            // 봇 자리도 후보다(2026-09-06). 사람만 뽑으면 혼자+봇일 때 사람이 늘 고양이였다.
            List<ulong> members = m_Session.SnapshotClientIds();
            List<ulong> pool = CatDrawLogic.WithBots(members, m_Session.BotCount);
            ulong cat = CatDrawLogic.Pick(
                pool, MatchHandoff.PreviousCat,
                Random.Range(0, pool.Count * 7 + 1), nm.LocalClientId);

            if (!m_Session.ServerBeginStart(cat)) return;

            // 시작했다 — 문을 닫는다. 대기실로 돌아오면 Start가 다시 연다.
            Networking.ConnectionGate.SetAccepting(false);
            Handoff(members, cat);
            GameDebug.Log($"[Room] 고양이 추첨 → client={cat} (후보 {pool.Count}명, 봇 {m_Session.BotCount})");

            m_Handing = true;
            LoadGameScene().Forget();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// **QA 전용** — 인원 조건을 건너뛰고 판을 연다. 혼자서 추첨·역할 인계·씬 전환까지
        /// 한 번에 확인하기 위한 것이다(인게임의 `DevForceStartGame`과 같은 취지).
        /// 에디터·개발 빌드에만 존재하므로 배포 빌드의 시작 조건은 그대로다.
        /// </summary>
        public void DevForceStartMatch()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || m_Session == null || m_Handing) return;

            List<ulong> members = m_Session.SnapshotClientIds();
            List<ulong> pool = CatDrawLogic.WithBots(members, m_Session.BotCount);
            ulong cat = CatDrawLogic.Pick(pool, MatchHandoff.PreviousCat, 0, nm.LocalClientId);
            if (!m_Session.ServerBeginStart(cat)) return;

            Networking.ConnectionGate.SetAccepting(false);
            Handoff(members, cat);
            m_Handing = true;
            LoadGameScene().Forget();
        }
#endif

        /// <summary>역할표는 사람만, 봇은 (쥐, 고양이) 수로 넘긴다 — 봇이 걸렸으면 봇 고양이 1.</summary>
        private void Handoff(List<ulong> _members, ulong _cat)
        {
            (int mice, int cats) = CatDrawLogic.BotSplit(m_Session.BotCount, _cat);
            MatchHandoff.Set(CatDrawLogic.RoleTable(_members, _cat), m_Session.MapIndex, _cat, mice, cats);
            // 판이 끝나 돌아오면 이 방 설정 그대로 다시 선다 — 돌아온 대기실이 꺼내 간다.
            RoomReturnMemo.Remember(m_Session.BotCount, m_Session.MapIndex);
        }

        // 결과가 화면에 뜰 시간을 준 뒤 넘긴다. **NGO의 SceneManager로만** 넘긴다 —
        // UnityEngine.SceneManagement를 직접 부르면 클라이언트가 따라오지 않는다.
        private async UniTask LoadGameScene()
        {
            var cancel = this.GetCancellationTokenOnDestroy();
            await UniTask.Delay((int)(REVEAL_SECONDS * 1000f), cancellationToken: cancel);

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            // 대기실 몸은 여기서 걷는다 — 동적 스폰은 씬을 넘어 살아남는다(실측).
            if (m_Spawner != null) m_Spawner.ServerDespawnAll();

            // 로딩창 안전망 — 추첨 연출이 띄웠으면 그대로 둔다(이미 떠 있으면 아무것도 안 한다).
            UI.LoadingScreen.Begin();

            var status = nm.SceneManager.LoadScene(m_GameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                GameDebug.LogError($"[Room] 인게임 씬 전환 실패: {status}");
                m_Handing = false;
            }
        }
    }
}
