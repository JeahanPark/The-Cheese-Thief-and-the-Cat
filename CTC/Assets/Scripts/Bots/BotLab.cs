#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using CTC.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// **봇 전용 실험실**(BotLab.unity, 2026-08-18). 사람 없이 봇만 도는 판을 자동으로 연다 —
    /// 플레이 버튼 하나로 호스트 시작 → 맵 선택 → 판 강제 시작 → 폴리곤이 설 때까지 대기 → 봇 스폰.
    ///
    /// 왜 따로 씬을 두나: 길찾기를 고칠 때 필요한 건 "봇이 어디로 가려 하고 어디서 막히나"인데,
    /// Game.unity에서는 사람 카메라·HUD·로비가 화면과 입력을 다 가져가서 그게 안 보인다.
    /// 여기서는 자유 카메라가 위에서 내려다보고(<see cref="BotLabCamera"/>),
    /// 경로선이 그려지고(<see cref="BotPathDebug"/>), 사람 입력은 통째로 막힌다.
    ///
    /// **사람 입력을 막는 방법**: 봇 몸은 서버 소유라 호스트에서 `IsOwner == true`가 된다 —
    /// 즉 게임 뷰에 포커스를 준 채 WASD를 누르면 그 키가 봇을 조종해 측정이 오염된다.
    /// `UiFocus.SetModalOpen(true)`로 한 번에 막는다(봇의 `DevSetMoveInput`은 그 뒤에 덮어쓰므로 무사하다).
    /// </summary>
    public class BotLab : MonoBehaviour
    {
        private const float NAVMESH_WAIT_SECONDS = 20f;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private int m_MapIndex;          // 0 기본(30×30) · 1 확장(42×42) · 2 대형(120×120)
        [SerializeField] private int m_Mice = 8;
        [SerializeField] private int m_Cats = 2;
        [SerializeField] private bool m_AutoStart = true;
        [SerializeField] private float m_TimeScale = 1f;
        [SerializeField] private bool m_Overlay = true;

        private static readonly float[] SPEED_STEPS = { 1f, 3f, 10f };

        private int m_SpeedIndex;
        private bool m_Paused;
        private GUIStyle m_Style;

        public static BotLab Instance { get; private set; }

        /// <summary>지금 어느 단계인가 — 오버레이·QA가 읽는다.</summary>
        public string Status { get; private set; } = "대기";

        /// <summary>경로선을 그릴까(F2).</summary>
        public bool DrawPaths { get; private set; } = true;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (m_AutoStart) Run().Forget();
        }

        /// <summary>판을 처음부터 연다. 다시 부르면 봇만 새로 세운다.</summary>
        public async UniTask Run()
        {
            // 사람 WASD가 봇 몸으로 새는 것을 막는다 — 봇 몸은 호스트에서 IsOwner다
            UiFocus.SetModalOpen(true);

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Status = "NetworkManager 없음";
                return;
            }

            var cancel = this.GetCancellationTokenOnDestroy();

            if (!nm.IsListening)
            {
                Status = "호스트 시작";
                if (!HostPortFallback.StartHost(nm))
                {
                    Status = "호스트 시작 실패";
                    return;
                }
            }

            // 로비는 in-scene NetworkObject다 — 호스트가 서고 나서 몇 프레임 뒤에 스폰된다
            Status = "로비 대기";
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && (m_Lobby == null || !m_Lobby.IsSpawned))
            {
                await UniTask.Delay(100, DelayType.Realtime, cancellationToken: cancel);
            }
            if (m_Lobby == null || !m_Lobby.IsSpawned)
            {
                Status = "로비가 안 섰다";
                return;
            }

            // 맵 선택은 반드시 판이 열리기 **전**이다 — 열린 뒤에는 조용히 무시된다
            if (!m_Lobby.GameStarted)
            {
                Status = $"맵 {m_MapIndex} 선택";
                m_Lobby.DevSelectMap(m_MapIndex);
                await UniTask.Delay(500, DelayType.Realtime, cancellationToken: cancel);

                Status = "판 시작";
                m_Lobby.DevForceStartGame();
            }

            // 맵을 짓고 폴리곤을 굽는 데 몇 프레임 걸린다 — 서기 전에 세우면 한 발도 못 뗀다
            Status = "폴리곤 대기";
            deadline = Time.realtimeSinceStartup + NAVMESH_WAIT_SECONDS;
            while (Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Delay(200, DelayType.Realtime, cancellationToken: cancel);
                if (!m_Lobby.GameStarted) continue;

                UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
                if (tri.indices != null && tri.indices.Length > 0) break;
            }

            TestBotManager bots = TestBotManager.Instance;
            if (bots == null)
            {
                Status = "TestBotManager 없음";
                return;
            }

            bots.ServerSpawn(m_Mice, m_Cats);
            SetSpeed(m_TimeScale);
            Status = $"돌고 있음 — 쥐 {m_Mice} · 고양이 {m_Cats}";
        }

        /// <summary>봇만 다시 세운다(F5) — 판은 그대로 둔다.</summary>
        public void Respawn()
        {
            TestBotManager bots = TestBotManager.Instance;
            if (bots == null) return;
            bots.ServerSpawn(m_Mice, m_Cats);
            Status = $"다시 세움 — 쥐 {m_Mice} · 고양이 {m_Cats}";
        }

        private void SetSpeed(float _scale)
        {
            Time.timeScale = Mathf.Clamp(_scale, 0f, 20f);
            // 물리도 같이 당겨야 CharacterController가 배속에서 벽을 뚫지 않는다
            if (Time.timeScale > 0f) Time.fixedDeltaTime = 0.02f / Time.timeScale;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) m_Overlay = !m_Overlay;
            if (kb.f2Key.wasPressedThisFrame) DrawPaths = !DrawPaths;
            if (kb.f5Key.wasPressedThisFrame) Respawn();
            if (kb.f6Key.wasPressedThisFrame)
            {
                m_Paused = !m_Paused;
                SetSpeed(m_Paused ? 0f : SPEED_STEPS[m_SpeedIndex]);
            }
            if (kb.f7Key.wasPressedThisFrame)
            {
                m_SpeedIndex = (m_SpeedIndex + 1) % SPEED_STEPS.Length;
                m_Paused = false;
                SetSpeed(SPEED_STEPS[m_SpeedIndex]);
            }
        }

        private void OnGUI()
        {
            if (!m_Overlay) return;

            m_Style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            TestBotManager bots = TestBotManager.Instance;
            int count = bots != null ? bots.Count : 0;

            GUILayout.BeginArea(new Rect(10f, 10f, 560f, Screen.height - 20f));
            GUILayout.Label($"<b>BotLab</b> — {Status}   ×{Time.timeScale:0.#}", m_Style);
            GUILayout.Label("F1 오버레이 · F2 경로선 · F5 봇 다시 · F6 일시정지 · F7 배속", m_Style);

            if (bots != null && count > 0)
            {
                GUILayout.Space(6f);
                foreach (BotAgent a in bots.Agents)
                {
                    string role = a.Role == Lobby.RoleId.Cat ? "<color=#FF9A6A>고양이</color>"
                                                             : "<color=#8FC7FF>쥐</color>";
                    GUILayout.Label($"#{a.Id} {role} {a.Activity} · {a.TravelledMeters:F0}m · {a.NavDiagnostics}",
                        m_Style);
                }
            }
            GUILayout.EndArea();
        }
    }
}
#endif
