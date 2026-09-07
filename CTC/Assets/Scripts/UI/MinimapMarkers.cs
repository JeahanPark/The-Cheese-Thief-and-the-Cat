using System;
using System.Collections.Generic;
using System.Threading;
using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using CTC.Runtime.Map;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>미니맵에 찍히는 점 한 종류. 색·모양은 View(<see cref="MinimapHud"/>)가 정한다.</summary>
    public enum MinimapMarkerKind
    {
        Shop,       // 상점 자리 — 쥐도 고양이도 본다. 여기서 만난다.
        Cheese,     // 치즈 매장지 — 쥐만, 그것도 발견분만
        Structure   // 내 창고·내 요새 — 쥐만, 내 것만
    }

    /// <summary>마커 하나. 값 타입이라 리스트에 담아도 할당이 안 생긴다.</summary>
    public readonly struct MinimapMarker
    {
        public readonly MinimapMarkerKind Kind;

        /// <summary>월드 (x, z) — 미니맵은 위에서 내려다보는 평면도라 y는 안 쓴다.</summary>
        public readonly Vector2 World;

        public MinimapMarker(MinimapMarkerKind _kind, Vector2 _world)
        {
            Kind = _kind;
            World = _world;
        }
    }

    /// <summary>
    /// 미니맵 Model(Worker, 2026-09-08) — 내 화면에 찍을 마커를 모아 R3로 흘린다.
    ///
    /// **역할 판정은 전부 <see cref="MinimapLogic"/>에 있다.** 여기서 새 if를 쓰면
    /// 「남은 안 그린다」는 원칙이 View·Model 두 곳으로 흩어져 조용히 깨진다.
    ///
    /// 「발견」은 **클라 전용 기억**이다. 네트워크로 보내지 않는다 — 서버가 알아야 할 일이
    /// 아니고, 보내는 순간 「누가 어디를 봤나」가 남의 정보가 된다. 판이 다시 시작하면 비운다.
    ///
    /// 갱신은 0.2초 주기 UniTask 루프다(코루틴 금지·매 프레임 금지). 마커는 5 Hz면 충분하다 —
    /// 상점은 안 움직이고 치즈·창고는 세워지고 부서질 뿐이다. 매 프레임 따라가야 하는 건
    /// **내 화살표뿐**이라, 그건 목록이 아니라 <see cref="Me"/>(내 몸)를 View가 그대로 읽는다.
    /// </summary>
    public class MinimapMarkers : MonoBehaviour
    {
        // 갱신 주기 — 밸런스가 아니라 「얼마나 자주 다시 세는가」다. CSV로 내리지 않는다.
        private const int REFRESH_MILLIS = 200;

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private CheeseSpotDirector m_CheeseDirector;

        // 재사용 버퍼 — 0.2초마다 새 리스트를 만들면 판당 수천 개가 GC로 간다
        private readonly List<MinimapMarker> m_Buffer = new List<MinimapMarker>();

        // 가 본 적 있는 치즈 매장지(레이아웃 순서 인덱스). **클라 전용**이고 판마다 비운다.
        private readonly HashSet<int> m_DiscoveredCheese = new HashSet<int>();

        private readonly Subject<IReadOnlyList<MinimapMarker>> m_Changed =
            new Subject<IReadOnlyList<MinimapMarker>>();

        private readonly ReactiveProperty<bool> m_Active = new ReactiveProperty<bool>(false);

        private bool m_GameStarted;
        private RoleId m_Role = RoleId.None;
        private Transform m_Me;

        /// <summary>0.2초마다 흐르는 마커 목록. **넘기는 리스트는 재사용된다** — 즉시 읽고 보관하지 않는다.</summary>
        public Observable<IReadOnlyList<MinimapMarker>> MarkersChanged => m_Changed;

        /// <summary>미니맵을 띄울 때인가 — 게임 중 + 내 몸이 있을 때(관전자·로비는 false).</summary>
        public ReadOnlyReactiveProperty<bool> Active => m_Active;

        /// <summary>
        /// 내 몸(없으면 null). 화살표는 매 프레임 따라가야 매끄러워서 View가 이 Transform을 직접 읽는다 —
        /// 「내 몸이 무엇인가」라는 판정은 여기(Model)가 하고, View는 그리기만 한다.
        /// </summary>
        public Transform Me => m_Me;

        /// <summary>QA·디버그용 — 지금 걸린 마커 목록.</summary>
        public IReadOnlyList<MinimapMarker> Markers => m_Buffer;

        /// <summary>QA·디버그용 — 지금까지 발견한 치즈 매장지 수.</summary>
        public int DiscoveredCheeseCount => m_DiscoveredCheese.Count;

        private static Info.BalanceInfo Balance => Info.InfoManager.Instance.Balance;

        private void Start()
        {
            m_Active.AddTo(this);

            m_Lobby.GameStartedChanged.Subscribe(OnGameStartedChanged).AddTo(this);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);

            RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnGameStartedChanged(bool _started)
        {
            m_GameStarted = _started;
            // 판이 새로 서면 발견 기억을 비운다 — 지난 판에서 본 자리가 다음 판에 남아 있으면
            // 「탐색해서 알아낸다」가 첫 판에만 있는 일이 된다(맵도 다시 지어진다).
            if (_started) m_DiscoveredCheese.Clear();
            ApplyActive();
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            RoleTag tag = _player != null ? _player.GetComponent<RoleTag>() : null;
            m_Role = tag != null ? tag.Role : RoleId.None;
            m_Me = _player != null ? _player.transform : null;
            ApplyActive();
        }

        private void ApplyActive()
        {
            m_Active.Value = m_GameStarted && m_Me != null;
        }

        private async UniTaskVoid RunAsync(CancellationToken _token)
        {
            try
            {
                while (!_token.IsCancellationRequested)
                {
                    Refresh();
                    await UniTask.Delay(REFRESH_MILLIS, cancellationToken: _token);
                }
            }
            catch (OperationCanceledException)
            {
                // 씬이 바뀌거나 오브젝트가 사라졌다 — 정상 종료다
            }
        }

        private void Refresh()
        {
            if (!m_Active.Value) return;

            m_Buffer.Clear();
            CollectShops();
            CollectCheese();
            CollectMyStructures();
            m_Changed.OnNext(m_Buffer);
        }

        // 상점 4곳 — 안 움직이지만 스폰이 매치 시작에 일어나 목록이 늦게 찬다
        private void CollectShops()
        {
            if (!MinimapLogic.ShowsShops(m_Role)) return;

            foreach (ShopSpot spot in ShopSpot.All)
            {
                if (spot == null) continue;
                m_Buffer.Add(new MinimapMarker(MinimapMarkerKind.Shop, Flat(spot.transform.position)));
            }
        }

        // 치즈 매장지 — **가 본 적 있는 자리만.** 고갈된 자리는 뺀다: 세상에서 사라진 것을
        // 지도에만 남기면 「저기 가면 있다」는 거짓말이 된다.
        private void CollectCheese()
        {
            if (!MinimapLogic.ShowsCheese(m_Role) || m_CheeseDirector == null) return;

            IReadOnlyList<CheeseSpot> spots = m_CheeseDirector.Spots;
            Vector2 me = Flat(m_Me.position);
            float discoverMeters = Balance.MinimapDiscoverMeters;

            for (int i = 0; i < spots.Count; i++)
            {
                CheeseSpot spot = spots[i];
                if (spot == null) continue;

                Vector2 world = Flat(spot.transform.position);
                if (MinimapLogic.IsDiscovered(world, me, discoverMeters)) m_DiscoveredCheese.Add(i);

                if (!m_DiscoveredCheese.Contains(i) || spot.Depleted) continue;
                m_Buffer.Add(new MinimapMarker(MinimapMarkerKind.Cheese, world));
            }
        }

        // 내 창고·내 요새 — 소유자는 세울 때 서버가 새긴 클라이언트 ID다(남의 것은 안 그린다)
        private void CollectMyStructures()
        {
            if (!MinimapLogic.ShowsOwnStructures(m_Role)) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            ulong me = nm.LocalClientId;

            foreach (WarehouseProduction warehouse in WarehouseProduction.All)
            {
                if (warehouse == null || warehouse.OwnerMouseClientId != me) continue;
                m_Buffer.Add(new MinimapMarker(MinimapMarkerKind.Structure, Flat(warehouse.transform.position)));
            }

            foreach (FortressShield fortress in FortressShield.All)
            {
                if (fortress == null || fortress.OwnerMouseClientId != me) continue;
                m_Buffer.Add(new MinimapMarker(MinimapMarkerKind.Structure, Flat(fortress.transform.position)));
            }
        }

        private static Vector2 Flat(Vector3 _world) => new Vector2(_world.x, _world.z);
    }
}
