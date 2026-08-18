#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Ai;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 밸런스 측정용 봇을 **밖에서** 조종한다 (2026-08-11).
    ///
    /// 2:8은 10명이 필요한데 MPPM은 최대 4명이다. 지금까지 검증한 건 "수치가 제대로 꽂혔는가"지
    /// "그래서 5:5인가"가 아니었다 — 그 간극을 메우는 계측기다.
    ///
    /// **봇의 몸에는 아무것도 안 붙인다.** 그냥 플레이어 프리팹을 스폰하고 여기서 손만 움직인다.
    /// 게임 객체가 "나를 조종하는 무언가가 있다"는 걸 몰라야, 봇이 재는 값이 사람의 값과 같다.
    ///
    /// 파일 전체가 개발 빌드 전용이라 배포판에는 봇 코드가 들어가지 않는다(`TestManager`와 같은 기준).
    /// </summary>
    public class TestBotManager : MonoBehaviour
    {
        /// <summary>씬에 하나. QA가 리플렉션으로 부른다.</summary>
        public static TestBotManager Instance { get; private set; }

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private CheeseSpotDirector m_Spots;
        [SerializeField] private MapDirector m_Map;
        [SerializeField] private GameObject m_CatPrefab;
        [SerializeField] private GameObject m_MousePrefab;

        // 스폰 자리가 비었는지 볼 때 쓰는 몸통. 바닥에 안 걸리게 살짝 띄운다.
        private const float BODY_CENTER_HEIGHT = 0.6f;
        private const float BODY_CLEARANCE = 0.45f;
        private const float SPAWN_STEP_METERS = 1.2f;

        private readonly List<BotAgent> m_Agents = new List<BotAgent>();
        private readonly SoundLog m_Sound = new SoundLog();

        // 예약(쥐가 뭉치지 않게) · 명령판(고양이 지휘관) — 자세한 건 각 클래스 주석에 있다
        private readonly Ai.Reservations m_Reservations = new Ai.Reservations();
        private readonly BotOrders m_Orders = new BotOrders();
        private BotCommander m_Commander;
        private bool m_Running;
        private string m_Setup = "";

        /// <summary>지금 도는 봇 수.</summary>
        public int Count => m_Agents.Count;

        /// <summary>쥐 봇 수 — 치즈 총량이 쥐 수에 비례하므로 로비가 이걸 더해 센다.</summary>
        /// <summary>디버그·시각화 전용(BotLab) — 지금 도는 봇들. 수정하면 안 된다.</summary>
        public System.Collections.Generic.IReadOnlyList<BotAgent> Agents => m_Agents;

        public int MouseCount => CountRole(RoleId.Mouse);
        public int CatCount => CountRole(RoleId.Cat);

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Stop();
        }

        /// <summary>
        /// 서버 전용 — 봇을 세운다. 매치가 시작된 뒤에 부른다(맵과 치즈 지점이 서 있어야 한다).
        /// 사람과 **같은 프리팹**을 쓴다. 다른 몸이면 이속·발소리가 달라져 측정이 무의미해진다.
        /// </summary>
        public void ServerSpawn(int _mice, int _cats)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            Stop();

            MapLayout layout = m_Map != null ? m_Map.ActiveLayout : null;
            m_Commander = new BotCommander(m_Orders, layout != null ? layout.Seed : 0);
            m_Commander.SetMapExtent(layout != null ? layout.HalfExtent : 60f);

            var forts = PlayerSpawner.FortressSpots(layout);
            m_Setup = $"map={(layout != null ? layout.name : "null-map")}" +
                      $" forts={forts.Count} navmesh={NavMeshArea()}";
            for (int i = 0; i < _mice; i++) SpawnOne(RoleId.Mouse, ClearSpawn(MouseSpawnPos(forts, i)));
            for (int i = 0; i < _cats; i++) SpawnOne(RoleId.Cat, ClearSpawn(new Vector3(i * 2f + 4f, 0.1f, 0f)));

            m_Commander.SetCats(m_Agents);

            // 총량이 쥐 수에 비례한다 — 봇이 늘었으니 다시 나눠야 한다
            if (m_Spots != null) m_Spots.ServerResetReserves();

            m_Sound.Start();
            m_Running = true;
            GameDebug.Log($"[Bots] 쥐 {_mice} · 고양이 {_cats} 세움 (폴리곤 {NavMeshArea()}개)");
        }

        /// <summary>서버 전용 — 봇을 걷어낸다.</summary>
        public void Stop()
        {
            foreach (BotAgent agent in m_Agents)
            {
                agent.Release();
                if (agent.Body != null && agent.Body.IsSpawned) agent.Body.Despawn(true);
            }
            m_Agents.Clear();
            m_Reservations.Clear();
            m_Orders.Clear();
            m_Commander = null;
            m_Sound.Stop();
            m_Running = false;
        }

        private void Update()
        {
            if (!m_Running) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            m_Sound.Forget(Time.time);

            float dt = Time.deltaTime;

            // 지휘관이 먼저 — 명령판을 갱신한 뒤에 봇들이 그걸 읽는다
            if (m_Commander != null) m_Commander.Tick(dt);

            for (int i = m_Agents.Count - 1; i >= 0; i--)
            {
                if (!m_Agents[i].Alive) { m_Agents.RemoveAt(i); continue; }
                m_Agents[i].Tick(dt, m_Spots, m_Sound);
            }
        }

        // **길찾기는 구운 폴리곤이 한다**(2026-08-12) — 매니저가 그래프를 따로 만들지 않는다.
        // `MapBuilder`가 지형을 짓고 바로 굽는다. 여기서는 그게 실제로 있는지만 확인한다 —
        // 0이면 봇이 한 발도 못 뗀다.
        private static int NavMeshArea()
        {
            UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            return tri.indices != null ? tri.indices.Length / 3 : 0;
        }

        private void SpawnOne(RoleId _role, Vector3 _pos)
        {
            GameObject prefab = _role == RoleId.Cat ? m_CatPrefab : m_MousePrefab;
            if (prefab == null)
            {
                GameDebug.LogError("[Bots] 플레이어 프리팹 미배선 — Setup Networking Scene 재실행 필요");
                return;
            }

            GameObject go = Instantiate(prefab, _pos, Quaternion.identity);
            var no = go.GetComponent<NetworkObject>();
            no.Spawn(true);   // SpawnAsPlayerObject가 아니다 — 붙일 클라이언트가 없다

            if (_role == RoleId.Mouse)
            {
                var wallet = go.GetComponent<CheeseWallet>();
                if (wallet != null) wallet.AddCheese(Info.InfoManager.Instance.Balance.MouseStartCheese);
            }

            m_Agents.Add(new BotAgent(no, _role, m_Reservations, m_Orders));
        }

        // 사람과 같은 자리 규칙 — 요새 입구 앞. 스폰 밀도가 달라지면 초반 동선이 통째로 달라진다.
        private static Vector3 MouseSpawnPos(IReadOnlyList<(Vector3 pos, float yaw)> _forts, int _nth)
        {
            if (_forts == null || _forts.Count == 0) return new Vector3(_nth * 1.5f, 0.1f, -4f);

            (Vector3 pos, float yaw) = _forts[_nth % _forts.Count];
            Vector3 spawn = pos + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward
                            * PlayerSpawner.FORTRESS_ENTRANCE_OFFSET_METERS;
            int wave = _nth / _forts.Count;
            if (wave > 0) spawn += Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (0.7f * wave);
            spawn.y = 0.1f;
            return spawn;
        }

        /// <summary>
        /// **누가 이미 서 있는 자리는 비껴 준다** (2026-08-11 실측으로 얻었다).
        ///
        /// 역할을 고르지 않은 사람(`RoleId.None`)도 고양이가 아니라는 이유로 **요새 0 앞**에
        /// 쥐로 세워진다. 봇 쥐 0번이 정확히 같은 좌표로 태어나면 두 캡슐이 서로를 밀어내
        /// **30초 동안 한 발도 못 뗀다** — 그 판의 승률은 못 믿는다.
        ///
        /// 벽도 같이 걸러진다(아무 콜라이더나 걸리면 그 자리는 버린다) — 비껴 준 자리가
        /// 벽 속이면 고친 게 아니다.
        /// </summary>
        private static Vector3 ClearSpawn(Vector3 _spawn)
        {
            if (!Occupied(_spawn)) return _spawn;

            const int RINGS = 8;
            for (int i = 0; i < RINGS; i++)
            {
                float rad = i * (Mathf.PI * 2f / RINGS);
                Vector3 candidate = _spawn + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * SPAWN_STEP_METERS;
                if (!Occupied(candidate)) return candidate;
            }
            return _spawn;   // 사방이 막혔다 — 제자리 감지가 빼내 준다
        }

        private static bool Occupied(Vector3 _pos)
        {
            Vector3 center = _pos + Vector3.up * BODY_CENTER_HEIGHT;
            foreach (Collider hit in Physics.OverlapSphere(
                         center, BODY_CLEARANCE, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit != null) return true;
            }
            return false;
        }

        private int CountRole(RoleId _role)
        {
            int count = 0;
            foreach (BotAgent a in m_Agents)
            {
                if (a.Alive && a.Role == _role) count++;
            }
            return count;
        }

        /// <summary>
        /// QA — 고양이 봇이 상대를 처음 본 시각(초). 못 봤으면 -1.
        /// **소리 밸런스의 답이 이 숫자다** — 18m 발소리가 실제로 얼마나 도움이 되는가.
        /// </summary>
        public float FirstSightSeconds()
        {
            foreach (BotAgent a in m_Agents)
            {
                if (a.Role == RoleId.Cat && a.FirstSightSeconds >= 0f) return a.FirstSightSeconds;
            }
            return -1f;
        }

        /// <summary>QA — 처음 봤을 때의 거리(m). 맵의 최장 직선보다 멀면 시야 판정이 새는 것이다.</summary>
        public float FirstSightDistance()
        {
            foreach (BotAgent a in m_Agents)
            {
                if (a.Role == RoleId.Cat && a.FirstSightDistance >= 0f) return a.FirstSightDistance;
            }
            return -1f;
        }

        /// <summary>QA — 쥐 봇들이 번 치즈 합. 0이면 아무도 안 캔 것이다.</summary>
        public int MouseCheese()
        {
            int sum = 0;
            foreach (BotAgent a in m_Agents)
            {
                if (a.Role != RoleId.Mouse || a.Body == null) continue;

                var wallet = a.Body.GetComponent<CheeseWallet>();
                if (wallet != null) sum += wallet.CheeseValue;
            }
            return sum;
        }

        /// <summary>QA — 지금 나가 있는 지휘관 명령 수.</summary>
        public int OrderCount => m_Commander != null ? m_Commander.OrderCount : 0;

        /// <summary>QA·통계 — 한 줄 요약. 벽에 붙박인 봇을 여기서 잡는다.</summary>
        public string Diagnostics()
        {
            var sb = new System.Text.StringBuilder(m_Setup + " | ");
            foreach (BotAgent a in m_Agents)
            {
                sb.Append($"#{a.Id}/{a.Role.ToString()[0]}/{a.Activity}/{a.TravelledMeters:F0}m/{a.NavDiagnostics} ");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
#endif
