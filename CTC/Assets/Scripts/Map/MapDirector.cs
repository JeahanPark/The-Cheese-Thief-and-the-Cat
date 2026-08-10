using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 선택과 재구축을 맡는 Worker (개편 3단계, 2026-07-31).
    ///
    /// 지오메트리는 **복제하지 않는다** — 전 클라가 같은 레이아웃 인덱스로 각자 짓는다.
    /// 복제되는 건 인덱스 하나뿐이라 대역폭이 사실상 0이고, 맵이 커져도 그대로다.
    /// 맵을 새로 지은 뒤 치즈 감독에 재바인딩하는 **순서가 중요**하다 —
    /// 지점이 만들어진 뒤에 복제 상태를 붙여야 인덱스가 어긋나지 않는다.
    /// </summary>
    public class MapDirector : NetworkBehaviour
    {
        [SerializeField] private MapLayout[] m_MapLayouts;
        [SerializeField] private MapBuilder m_MapBuilder;
        [SerializeField] private Economy.CheeseSpotDirector m_CheeseDirector;
        [SerializeField] private Match.JailDirector m_JailDirector;

        private readonly NetworkVariable<int> m_MapIndex = new NetworkVariable<int>();

        public int MapCount => m_MapLayouts != null ? m_MapLayouts.Length : 0;
        public int MapIndex => LobbyRoleLogic.ClampMapIndex(m_MapIndex.Value, MapCount);

        /// <summary>선택 맵이 바뀌었다는 신호 — 로비 UI가 버튼 강조를 다시 그린다.</summary>
        public Observable<int> MapIndexChanged => m_MapIndex.AsObservable();

        /// <summary>선택된 맵. 스폰 공식·요새 자리가 이걸 읽는다.</summary>
        public MapLayout ActiveLayout => MapCount == 0 ? null : m_MapLayouts[MapIndex];

        public string MapNameAt(int _index) =>
            MapCount == 0 ? "" : m_MapLayouts[LobbyRoleLogic.ClampMapIndex(_index, MapCount)].DisplayName;

        public override void OnNetworkSpawn()
        {
            m_MapIndex.OnValueChanged += OnMapIndexChanged;
            RebuildMap();
        }

        public override void OnNetworkDespawn()
        {
            m_MapIndex.OnValueChanged -= OnMapIndexChanged;
        }

        /// <summary>로컬 UI/QA 진입점. 실제 권한 판정은 서버.</summary>
        public void SelectMap(int _index)
        {
            SelectMapRpc(_index);
        }

        [Rpc(SendTo.Server)]
        private void SelectMapRpc(int _index, RpcParams _rpcParams = default)
        {
            // 방장만 바꾼다 — 시작 버튼과 같은 권한 구조
            if (_rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;

            var session = GetComponent<LobbySession>();
            if (session != null && session.GameStarted) return;

            m_MapIndex.Value = LobbyRoleLogic.ClampMapIndex(_index, MapCount);
        }

        private void OnMapIndexChanged(int _previous, int _current)
        {
            RebuildMap();
        }

        private void RebuildMap()
        {
            if (m_MapBuilder == null || ActiveLayout == null) return;

            m_MapBuilder.Build(ActiveLayout);
            if (m_JailDirector != null) m_JailDirector.Rebind(m_MapBuilder.JailPositions);

            if (m_CheeseDirector != null)
            {
                m_CheeseDirector.Rebind(
                    m_MapBuilder.Spots,
                    AlcoveLogic.TotalCheese(AlcoveLogic.CountCheese(ActiveLayout.Alcoves))
                    + LootBoxLogic.TotalCheese(ActiveLayout.Boxes));
            }
        }
    }
}
