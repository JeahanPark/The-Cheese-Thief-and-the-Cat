using System.Collections.Generic;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 치즈 지점(원작 돈줄). 반경 내 쥐를 검사해 선점자 1명에게 초당 수입을 준다.
    ///
    /// v3.5: 맵이 런타임에 지어지면서 in-scene NetworkObject를 그만뒀다. 잔량·점유의 복제는
    /// CheeseSpotDirector가 레이아웃 순서 인덱스로 한꺼번에 담당한다 — 지점은 판정과 표시만 한다.
    /// 서버에서만 ServerTick이 불리고, 클라이언트는 ApplyState로 결과만 받는다.
    /// </summary>
    public class CheeseSpot : MonoBehaviour
    {
        private const int OVERLAP_BUFFER_SIZE = 16;

        private static readonly Color COLOR_FREE = new Color(1f, 0.92f, 0.3f);     // 노랑 — 비었음
        private static readonly Color COLOR_MINE = new Color(0.45f, 0.95f, 0.45f); // 초록 — 내가 캐는 중
        private static readonly Color COLOR_TAKEN = new Color(1f, 0.4f, 0.35f);    // 빨강 — 남이 점유

        [SerializeField] private int m_Grade = 1;
        [SerializeField] private float m_Radius = 1.5f;
        [SerializeField] private GameObject m_Visual;        // 디스크·라벨 루트 — 고갈 시 숨긴다
        [SerializeField] private TMPro.TextMeshPro m_Label;  // "등급 · 잔량" + 점유 색
        [SerializeField] private Renderer m_Disc;

        private int m_Remaining;
        private ulong m_Earner = CheeseSpotLogic.NO_EARNER;
        private float m_Accumulated;
        private readonly List<ulong> m_Present = new List<ulong>();
        private readonly List<ulong> m_EntryOrder = new List<ulong>(); // 먼저 온 순서(서버 전용)
        private readonly Dictionary<ulong, CheeseWallet> m_Wallets = new Dictionary<ulong, CheeseWallet>();
        private readonly Collider[] m_Overlap = new Collider[OVERLAP_BUFFER_SIZE];
        private MaterialPropertyBlock m_Mpb;

        public int Grade => m_Grade;
        public int Remaining => m_Remaining;
        public bool Depleted => m_Remaining <= 0;
        /// <summary>
        /// 지금 캐는 사람의 **NetworkObjectId**(클라이언트 id가 아니다, 2026-08-11).
        ///
        /// 봇은 전부 서버가 소유해서 `OwnerClientId`가 다 같다 — 그걸 열쇠로 쓰면 봇끼리
        /// 점유가 뭉개지고, 더 나쁘게는 **호스트(클라 0)에게 봇이 캐는 지점이 전부 '내 것'으로 보인다**.
        /// 오브젝트 id는 사람이든 봇이든 하나씩이라 그런 충돌이 없다.
        /// </summary>
        public ulong EarnerClientId => m_Earner;

        /// <summary>QA — 지금 화면에 걸린 라벨 문자열. 표시가 갱신되는지 확인하는 유일한 창구다.</summary>
        public string LabelText => m_Label != null ? m_Label.text : "";

        /// <summary>MapBuilder가 지점을 만들 때 1회 설정.</summary>
        public void Configure(int _grade, float _radius, int _reserve,
            GameObject _visual, TMPro.TextMeshPro _label, Renderer _disc)
        {
            m_Grade = _grade;
            m_Radius = _radius;
            m_Remaining = _reserve;
            m_Visual = _visual;
            m_Label = _label;
            m_Disc = _disc;
            ApplyVisual();
        }

        /// <summary>서버 전용 — 매장량 설정(매치 시작 복구·부활·QA 강제 고갈).</summary>
        public void ServerSetReserve(int _reserve)
        {
            m_Remaining = Mathf.Max(0, _reserve);
            m_Accumulated = 0f;
            m_Earner = CheeseSpotLogic.NO_EARNER;
            m_EntryOrder.Clear();
            ApplyVisual();
        }

        /// <summary>
        /// 서버 전용 — 한 프레임 진행. 선점자를 갱신하고 지갑에 지급한 뒤 바뀐 상태를 알린다.
        /// 반환: 상태가 변했는가(Director가 복제 목록을 갱신할지 판단).
        /// </summary>
        public bool ServerTick(float _deltaSeconds, bool _matchRunning)
        {
            int prevRemaining = m_Remaining;
            ulong prevEarner = m_Earner;

            Advance(_deltaSeconds, _matchRunning);

            bool changed = prevRemaining != m_Remaining || prevEarner != m_Earner;
            // 서버(=호스트)는 복제 이벤트를 스스로 받지 않는다(Director가 IsServer면 되먹임을 끊는다).
            // 그래서 여기서 직접 표시를 갱신하지 않으면 호스트 화면에서만 잔량·점유가 얼어붙는다.
            if (changed) ServerRefreshVisual(prevRemaining);
            return changed;
        }

        private void Advance(float _deltaSeconds, bool _matchRunning)
        {
            if (!_matchRunning || Depleted)
            {
                m_Earner = CheeseSpotLogic.NO_EARNER;
                m_Accumulated = 0f;
                return;
            }

            CollectPresent();
            CheeseSpotLogic.UpdateEntryOrder(m_EntryOrder, m_Present);
            m_Earner = CheeseSpotLogic.SelectEarner(m_Earner, m_EntryOrder);

            if (m_Earner == CheeseSpotLogic.NO_EARNER)
            {
                m_Accumulated = 0f;
                return;
            }

            m_Accumulated = CheeseSpotLogic.Accumulate(m_Accumulated, m_Grade, _deltaSeconds, out int whole);
            if (whole <= 0) return;

            int granted = CheeseSpotLogic.Extract(m_Remaining, whole);
            m_Remaining -= granted;   // 매장량은 배율 전 값으로 깎는다 — 아래 주석 참고
            if (granted > 0 && m_Wallets.TryGetValue(m_Earner, out CheeseWallet wallet) && wallet != null)
            {
                // 큰 주머니 — 지갑에 들어가는 양에만 배율을 건다.
                // 매장량(m_Remaining)은 그대로 깎는 게 핵심이다: "더 잘 캔다"가 아니라 "더 챙긴다".
                // 배율을 매장량에도 걸면 좋은 자리가 그만큼 빨리 말라 고갈 = 성장 기한 설계가 흔들린다.
                //
                // 열쇠(m_Earner)는 clientId가 아니라 **NetworkObjectId**라서 NetUtil.FindPlayerObject를
                // 쓸 수 없다. 어차피 wallet이 선점자의 플레이어 루트에 붙어 있고(TechSlots와 같은 오브젝트)
                // 배율 원본은 CSV(TechInfo P1 — 1.3/1.6/1.9)다.
                float pouchMul = TechOwner.Param(wallet.gameObject, TechId.BigPouch, 1);
                wallet.AddCheese(CheeseSpotLogic.ScaleMined(granted, pouchMul));
            }
        }

        // 서버 전용 — 클라의 ApplyState와 같은 결과(표시 + 고갈음)를 로컬에 낸다.
        private void ServerRefreshVisual(int _prevRemaining)
        {
            ApplyVisual();
            if (_prevRemaining > 0 && Depleted)
            {
                Audio.SfxPlayer.PlayAt(Audio.SfxId.CheeseDepleted, transform.position);
            }
        }

        /// <summary>클라이언트 — 복제된 상태를 받아 표시에 반영한다.</summary>
        public void ApplyState(int _remaining, ulong _earner)
        {
            bool wasDepleted = Depleted;
            m_Remaining = _remaining;
            m_Earner = _earner;

            ApplyVisual();
            if (!wasDepleted && Depleted)
            {
                Audio.SfxPlayer.PlayAt(Audio.SfxId.CheeseDepleted, transform.position);
            }
        }

        // 라벨 = "등급 · 남은 매장량", 색 = 점유 상태. 멀리서 보고 갈지 말지 정하는 정보다.
        private void ApplyVisual()
        {
            if (m_Visual != null && m_Visual.activeSelf == Depleted) m_Visual.SetActive(!Depleted);
            if (m_Label == null) return;

            NetworkManager nm = NetworkManager.Singleton;
            // 내 몸의 오브젝트 id와 견준다 — 위 주석대로 열쇠가 클라 id가 아니다
            Unity.Netcode.NetworkObject me =
                nm != null && nm.SpawnManager != null ? nm.SpawnManager.GetLocalPlayerObject() : null;
            ulong local = me != null ? me.NetworkObjectId : CheeseSpotLogic.NO_EARNER;
            CheeseSpotLogic.Occupancy occupancy = CheeseSpotLogic.OccupancyOf(m_Earner, local);

            m_Label.text = occupancy == CheeseSpotLogic.Occupancy.Others
                ? $"{m_Grade} · {m_Remaining}\n점유 중"
                : $"{m_Grade} · {m_Remaining}";
            m_Label.color = occupancy == CheeseSpotLogic.Occupancy.Mine ? COLOR_MINE
                : occupancy == CheeseSpotLogic.Occupancy.Others ? COLOR_TAKEN
                : COLOR_FREE;

            if (m_Disc == null) return;
            m_Mpb ??= new MaterialPropertyBlock();
            m_Disc.GetPropertyBlock(m_Mpb);
            // 남이 점유 중이면 디스크를 어둡게 — 색약에도 밝기 차로 구분된다
            m_Mpb.SetColor("_BaseColor", occupancy == CheeseSpotLogic.Occupancy.Others
                ? new Color(0.45f, 0.45f, 0.45f) : Color.white);
            m_Disc.SetPropertyBlock(m_Mpb);
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

                ulong owner = wallet.NetworkObjectId;
                if (m_Wallets.ContainsKey(owner)) continue;

                m_Present.Add(owner);
                m_Wallets.Add(owner, wallet);
            }
        }

        // ===== 채굴 모션(테크 v3 계획 2b Task 5) =====
        //
        // "저 쥐가 지금 돈을 캐는 중"이 화면에서 읽혀야 고양이가 달려올 이유가 생긴다.
        // **여기는 Rpc를 쓰지 않는다** — 지점은 NetworkBehaviour가 아니고(복제는 Director가 대신한다),
        // 선점자(m_Earner)는 이미 전 클라에 있다: 서버는 Advance가, 클라는 ApplyState가 세운다.
        // 그러니 각 클라가 제 화면의 선점자에게 원샷을 걸면 된다 — 대역폭 0, 소리 없는 반복.
        //
        // 0.5초 걸고 0.8초마다 갱신하는 것은 **일부러**다. PlayOneShot은 그 시간 동안 이동 전이를
        // 잠그므로 0.8초 중 0.3초는 항상 풀려 있다 — 캐다 말고 걷기 시작한 쥐도 그 틈에 Walk로 넘어간다.
        // (지점 밖으로 나가면 선점이 풀려 아래 첫 줄에서 바로 멈춘다.)
        private const float EAT_ANIM_SECONDS = 0.5f;
        private const float EAT_ANIM_INTERVAL_SECONDS = 0.8f;

        private ulong m_FxEarner = CheeseSpotLogic.NO_EARNER;
        private Player.PlayerAnimationDriver m_FxDriver; // 선점자가 바뀔 때만 다시 찾는다
        private float m_NextEatAnimTime;

        private void Update()
        {
            if (Depleted || m_Earner == CheeseSpotLogic.NO_EARNER)
            {
                m_FxEarner = CheeseSpotLogic.NO_EARNER;
                m_FxDriver = null;
                return;
            }

            if (m_Earner != m_FxEarner)
            {
                m_FxEarner = m_Earner;
                m_FxDriver = FindDriver(m_Earner);
                m_NextEatAnimTime = 0f;   // 막 자리를 잡은 쥐는 기다리지 않고 바로 한 번 갉는다
            }

            if (m_FxDriver == null || Time.time < m_NextEatAnimTime) return;
            m_NextEatAnimTime = Time.time + EAT_ANIM_INTERVAL_SECONDS;
            m_FxDriver.PlayOneShot(Player.AnimationStateLogic.Eat, EAT_ANIM_SECONDS);
        }

        // 열쇠가 clientId가 아니라 NetworkObjectId라 NetUtil.FindPlayerObject를 못 쓴다(위 주석).
        // 스폰 목록에서 바로 집는다 — 서버·클라 어디서든 같은 표다.
        private static Player.PlayerAnimationDriver FindDriver(ulong _networkObjectId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(_networkObjectId, out NetworkObject body)) return null;
            return body != null ? body.GetComponentInChildren<Player.PlayerAnimationDriver>() : null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_Radius);
        }
    }
}
