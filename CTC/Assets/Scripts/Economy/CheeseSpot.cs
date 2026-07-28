using System.Collections.Generic;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점(원작 돈줄). 서버만 반경 내 플레이어를 검사해
    /// 선점자 1명에게 초당 수입을 지급한다. 동기화는 CheeseWallet(NetworkVariable)이 담당.
    ///
    /// v3.4: 매장량이 생겼다. 다 캐면 지점이 사라져(시각·수입 모두) 후반엔 남은 자리로 몰리게 된다.
    /// 잔량은 NetworkVariable로 복제 — 클라가 디스크를 끄고 남은 자리를 판단해야 하기 때문.
    /// </summary>
    public class CheeseSpot : NetworkBehaviour
    {
        private const int OVERLAP_BUFFER_SIZE = 16;

        [SerializeField] private int m_Grade = 1;
        [SerializeField] private float m_Radius = 1.5f;
        [SerializeField] private GameObject m_Visual;        // 디스크·라벨 루트 — 고갈 시 숨긴다
        [SerializeField] private TMPro.TextMeshPro m_Label;  // "등급 · 잔량" + 점유 색
        [SerializeField] private Renderer m_Disc;

        private static readonly Color COLOR_FREE = new Color(1f, 0.92f, 0.3f);   // 노랑 — 비었음
        private static readonly Color COLOR_MINE = new Color(0.45f, 0.95f, 0.45f); // 초록 — 내가 캐는 중
        private static readonly Color COLOR_TAKEN = new Color(1f, 0.4f, 0.35f);  // 빨강 — 남이 점유

        private readonly NetworkVariable<int> m_Remaining =
            new NetworkVariable<int>(CheeseSpotLogic.SPOT_RESERVE);

        // 누가 캐고 있는지 전원이 알아야 한다 — 멀리서 보고 다른 자리로 갈지 판단하는 정보
        private readonly NetworkVariable<ulong> m_Earner =
            new NetworkVariable<ulong>(CheeseSpotLogic.NO_EARNER);

        private float m_Accumulated;
        private LobbyManager m_Lobby;
        private readonly List<ulong> m_Present = new List<ulong>();
        private readonly List<ulong> m_EntryOrder = new List<ulong>(); // 먼저 온 순서(서버 전용)
        private readonly Dictionary<ulong, CheeseWallet> m_Wallets = new Dictionary<ulong, CheeseWallet>();
        private readonly Collider[] m_Overlap = new Collider[OVERLAP_BUFFER_SIZE];
        private MaterialPropertyBlock m_Mpb;

        public int Grade => m_Grade;
        public int Remaining => m_Remaining.Value;
        public bool Depleted => m_Remaining.Value <= 0;
        public ulong EarnerClientId => m_Earner.Value;

        private void Start()
        {
            m_Lobby = FindFirstObjectByType<LobbyManager>();
        }

        public override void OnNetworkSpawn()
        {
            m_Remaining.OnValueChanged += OnRemainingChanged;
            m_Earner.OnValueChanged += OnEarnerChanged;
            ApplyVisual();
            ApplyLabel();
        }

        public override void OnNetworkDespawn()
        {
            m_Remaining.OnValueChanged -= OnRemainingChanged;
            m_Earner.OnValueChanged -= OnEarnerChanged;
        }

        /// <summary>서버 전용 — 매치 재시작 시 매장량 복구. 고갈된 채로 다음 판이 시작되면 안 된다.</summary>
        public void ServerResetReserve()
        {
            if (!IsServer) return;
            m_Remaining.Value = CheeseSpotLogic.SPOT_RESERVE;
            m_Accumulated = 0f;
            m_Earner.Value = CheeseSpotLogic.NO_EARNER;
            m_EntryOrder.Clear();
        }

        /// <summary>서버 전용 — 마지막 부활(모든 지점 고갈 후 1곳만). CheeseSpotDirector가 호출한다.</summary>
        public void ServerRevive(int _reserve)
        {
            if (!IsServer) return;
            m_Remaining.Value = Mathf.Max(0, _reserve);
            m_Accumulated = 0f;
        }

        private void OnRemainingChanged(int _previous, int _current)
        {
            ApplyVisual();
            ApplyLabel();
            if (_previous > 0 && _current <= 0)
            {
                Audio.SfxPlayer.PlayAt(Audio.SfxId.CheeseDepleted, transform.position);
            }
        }

        private void OnEarnerChanged(ulong _previous, ulong _current)
        {
            ApplyLabel();
        }

        private void ApplyVisual()
        {
            if (m_Visual != null && m_Visual.activeSelf == Depleted) m_Visual.SetActive(!Depleted);
        }

        // 라벨 = "등급 · 남은 매장량", 색 = 점유 상태. 멀리서 보고 갈지 말지 정하는 정보다.
        private void ApplyLabel()
        {
            if (m_Label == null) return;

            NetworkManager nm = NetworkManager.Singleton;
            ulong local = nm != null ? nm.LocalClientId : CheeseSpotLogic.NO_EARNER;
            CheeseSpotLogic.Occupancy occupancy = CheeseSpotLogic.OccupancyOf(m_Earner.Value, local);

            Color color = occupancy == CheeseSpotLogic.Occupancy.Mine ? COLOR_MINE
                : occupancy == CheeseSpotLogic.Occupancy.Others ? COLOR_TAKEN
                : COLOR_FREE;

            m_Label.text = occupancy == CheeseSpotLogic.Occupancy.Others
                ? $"{m_Grade} · {m_Remaining.Value}\n점유 중"
                : $"{m_Grade} · {m_Remaining.Value}";
            m_Label.color = color;

            if (m_Disc == null) return;
            m_Mpb ??= new MaterialPropertyBlock();
            m_Disc.GetPropertyBlock(m_Mpb);
            // 남이 점유 중이면 디스크를 어둡게 — 색약에도 밝기 차로 구분된다
            m_Mpb.SetColor("_BaseColor", occupancy == CheeseSpotLogic.Occupancy.Others
                ? new Color(0.45f, 0.45f, 0.45f) : Color.white);
            m_Disc.SetPropertyBlock(m_Mpb);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;
            if (m_Lobby == null || !m_Lobby.IsSpawned || !m_Lobby.GameStarted) return;
            if (Depleted)
            {
                if (m_Earner.Value != CheeseSpotLogic.NO_EARNER) m_Earner.Value = CheeseSpotLogic.NO_EARNER;
                return;
            }

            CollectPresent();
            CheeseSpotLogic.UpdateEntryOrder(m_EntryOrder, m_Present);
            ulong earner = CheeseSpotLogic.SelectEarner(m_Earner.Value, m_EntryOrder);
            if (m_Earner.Value != earner) m_Earner.Value = earner;

            if (earner == CheeseSpotLogic.NO_EARNER)
            {
                m_Accumulated = 0f;
                return;
            }

            m_Accumulated = CheeseSpotLogic.Accumulate(m_Accumulated, m_Grade, Time.deltaTime, out int whole);
            if (whole <= 0) return;

            int granted = CheeseSpotLogic.Extract(m_Remaining.Value, whole);
            m_Remaining.Value -= granted;
            if (granted > 0 && m_Wallets.TryGetValue(earner, out CheeseWallet wallet) && wallet != null)
            {
                wallet.AddCheese(granted);
            }
        }

        // CharacterController도 Collider라 OverlapSphere에 잡힌다.
        private void CollectPresent()
        {
            m_Present.Clear();
            m_Wallets.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, m_Radius, m_Overlap);
            for (int i = 0; i < count; i++)
            {
                CheeseWallet wallet = m_Overlap[i].GetComponentInParent<CheeseWallet>();
                if (wallet == null || !wallet.IsSpawned) continue;

                // 수입은 쥐 전용 (기획 변경 2026-07-22 — 고양이 재화는 처치 보상만)
                Combat.RoleTag tag = wallet.GetComponent<Combat.RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                ulong owner = wallet.OwnerClientId;
                if (m_Wallets.ContainsKey(owner)) continue;

                m_Present.Add(owner);
                m_Wallets.Add(owner, wallet);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_Radius);
        }
    }
}
