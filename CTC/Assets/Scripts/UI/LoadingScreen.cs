using System.Threading;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 접속 대기 로딩창 (2026-09-19, 사용자 시안 5) — 화면 전체를 그림 한 장(<c>T_LoadingBackdrop</c>)으로 가리고,
    /// 오른쪽 아래에 「로딩중」과 점(. → .. → ...)을 띄운다.
    ///
    /// **씬을 넘어 산다**(DontDestroyOnLoad). 대기실 추첨 연출이 검게 닫히는 순간(<c>RoomDrawStage</c>) 전원의 화면에서
    /// <see cref="Begin"/>으로 뜨고, 게임 씬이 로드되는 동안 계속 가리다가 그 씬의 판이 서면
    /// (<see cref="LobbyController.GameStartedChanged"/> — 서버가 전원 접속 또는 접속 대기 시간이 끝났을 때 켠다) 걷히고 사라진다.
    /// 예전엔 게임 씬 캔버스에 붙어 있어 **씬 로딩 동안의 검은 화면을 못 덮었고**, 로컬에선 판이 곧바로 서서
    /// 한 번도 안 보였다(2026-09-19 3인 실측) — 그래서 대기실에서 띄워 씬을 건너오게 바꿨다.
    ///
    /// 프리팹은 <c>Resources/UI/LoadingOverlay</c>(씬 빌더가 굽는다). 한 번 쓰고 버린다 — 다음 판에 다시 만든다.
    /// 서버가 멈춰도 영영 가리지 않게 상한이 있다(<see cref="LoadingScreenLogic"/>).
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public const string RESOURCE_PATH = "UI/LoadingOverlay";

        private const int TICK_MS = 50;

        [SerializeField] private CanvasGroup m_Group;
        [SerializeField] private TMP_Text m_Dots;

        private static LoadingScreen s_Current;

        private LobbyController m_Lobby;
        private System.IDisposable m_GameStartedSub;
        private bool m_GameStarted;
        private CancellationTokenSource m_Cts;

        /// <summary>지금 떠 있는 로딩창(없으면 null) — QA가 본다.</summary>
        public static LoadingScreen Current => s_Current;

        /// <summary>QA: 지금 가리고 있나(걷히는 중에도 참).</summary>
        public bool IsShowing => m_Group != null && m_Group.alpha > 0f && gameObject.activeInHierarchy;

        /// <summary>QA: 지금 찍힌 점(". .. ...") — 점이 도는지 두 번 읽어 본다.</summary>
        public string DotsText => m_Dots != null ? m_Dots.text : "";

        /// <summary>
        /// 로딩창을 띄운다. **이미 떠 있으면 아무것도 안 한다** — 추첨 연출(전원)과 감독의 씬 전환(서버) 두 자리에서
        /// 부르기 때문이다. 처음부터 다시 재면 떠오르던 그림이 한 번 꺼졌다 켜진다.
        /// </summary>
        public static void Begin()
        {
            if (s_Current != null) return;
            var prefab = Resources.Load<GameObject>(RESOURCE_PATH);
            if (prefab == null)
            {
                GameDebug.LogWarning($"[LoadingScreen] 프리팹이 없다: Resources/{RESOURCE_PATH} — Setup Networking Scene을 돌린다");
                return;
            }
            GameObject go = Instantiate(prefab);
            go.name = prefab.name;
            DontDestroyOnLoad(go);
            s_Current = go.GetComponent<LoadingScreen>();
            if (s_Current != null) s_Current.Restart();
        }

        private void OnDestroy()
        {
            if (s_Current == this) s_Current = null;
            Cancel();
            m_GameStartedSub?.Dispose();   // 순수 C# — 규칙 9 예외
        }

        private void Restart()
        {
            Cancel();
            m_GameStartedSub?.Dispose();
            m_GameStartedSub = null;
            m_Lobby = null;
            m_GameStarted = false;
            if (m_Group != null) m_Group.alpha = 0f;
            gameObject.SetActive(true);
            m_Cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunAsync(m_Cts.Token).Forget();
        }

        // 점과 걷을 때를 한 박자로 돈다. 걷히면 끝나고 오브젝트째 버린다 — 판 도중에 다시 가리지 않는다.
        private async UniTaskVoid RunAsync(CancellationToken _token)
        {
            float start = Time.unscaledTime;
            while (true)
            {
                float elapsed = Time.unscaledTime - start;
                BindGameStarted();
                if (!LoadingScreenLogic.ShouldShow(m_GameStarted, elapsed)) break;
                if (m_Group != null) m_Group.alpha = LoadingScreenLogic.FadeInAlpha(elapsed);
                if (m_Dots != null) m_Dots.text = LoadingScreenLogic.Dots(elapsed);

                bool cancelled = await UniTask.Delay(TICK_MS, ignoreTimeScale: true, cancellationToken: _token)
                    .SuppressCancellationThrow();
                if (cancelled) return;
            }

            float hideStart = Time.unscaledTime;
            while (true)
            {
                float alpha = LoadingScreenLogic.FadeOutAlpha(Time.unscaledTime - hideStart);
                if (m_Group != null) m_Group.alpha = alpha;
                if (alpha <= 0f) break;
                bool cancelled = await UniTask.NextFrame(PlayerLoopTiming.Update, _token).SuppressCancellationThrow();
                if (cancelled) return;
            }
            if (this != null) Destroy(gameObject);
        }

        // 게임 씬의 LobbyController는 씬이 넘어오고 **네트워크 스폰까지 끝나야** 판 시작 값을 준다 — 그 전에 구독하면
        // 「false 한 번」으로 끝난다. 설 때까지 매 박자 한 번씩 찾아보고, 서면 한 번만 구독한다.
        // (대기실 씬에는 LobbyController가 없어 넘어가기 전엔 계속 null이다.)
        private void BindGameStarted()
        {
            if (m_GameStartedSub != null) return;
            if (m_Lobby == null) m_Lobby = FindAnyObjectByType<LobbyController>();
            if (m_Lobby == null || !m_Lobby.IsSpawned) return;
            m_GameStartedSub = m_Lobby.GameStartedChanged.Subscribe(_started => m_GameStarted = _started);
        }

        private void Cancel()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
        }
    }
}
