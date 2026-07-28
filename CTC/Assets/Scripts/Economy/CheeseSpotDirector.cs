using System.Collections.Generic;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>지점 하나의 복제 상태. 인덱스는 MapLayout의 치즈 순서와 같다.</summary>
    public struct CheeseSpotState : INetworkSerializable, System.IEquatable<CheeseSpotState>
    {
        public int Remaining;
        public ulong Earner;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Remaining);
            serializer.SerializeValue(ref Earner);
        }

        public bool Equals(CheeseSpotState other) => Remaining == other.Remaining && Earner == other.Earner;
    }

    /// <summary>
    /// 치즈 지점 전체를 감독한다(in-scene NetworkObject, LobbyManager와 동승).
    /// - 지점 상태(잔량·점유)를 NetworkList로 복제 — 지점 자체는 네트워크 오브젝트가 아니다.
    ///   맵이 런타임에 지어지므로 in-scene 해시를 쓸 수 없고, 지점마다 동적 스폰하는 건 과하다.
    /// - 매치 시작 시 매장량 복구
    /// - 전 지점 고갈 시 최고 등급 1곳만 부활 — 뒤처진 쥐의 만회 기회이자 후반 결전지.
    ///   부활은 판당 1회. 무한 부활은 고갈로 만든 압축을 도로 없앤다.
    /// </summary>
    public class CheeseSpotDirector : NetworkBehaviour
    {
        private const float POLL_SECONDS = 1f;

        [SerializeField] private LobbyManager m_Lobby;
        [SerializeField] private MatchManager m_Match;

        private NetworkList<CheeseSpotState> m_States;
        private readonly List<CheeseSpot> m_Spots = new List<CheeseSpot>();
        private bool m_WasStarted;
        private bool m_RevivedThisMatch;
        private float m_Timer;

        public bool RevivedThisMatch => m_RevivedThisMatch;
        public IReadOnlyList<CheeseSpot> Spots => m_Spots;

        private void Awake()
        {
            // NetworkList는 필드 초기화 대신 Awake에서 생성 (NGO 권장 — 도메인 리로드 누수 방지)
            m_States = new NetworkList<CheeseSpotState>();
        }

        public override void OnNetworkSpawn()
        {
            m_States.OnListChanged += OnStatesChanged;
            PushAllToSpots();
        }

        public override void OnNetworkDespawn()
        {
            m_States.OnListChanged -= OnStatesChanged;
        }

        /// <summary>
        /// MapBuilder가 맵을 새로 지은 뒤 호출한다. 서버는 목록 길이를 지점 수에 맞추고,
        /// 클라는 이미 복제된 상태를 새 지점들에 즉시 반영한다.
        /// </summary>
        public void Rebind(IReadOnlyList<CheeseSpot> _spots)
        {
            m_Spots.Clear();
            m_Spots.AddRange(_spots);

            if (IsServer && IsSpawned) ResetAll();
            else PushAllToSpots();
        }

        private void OnStatesChanged(NetworkListEvent<CheeseSpotState> _event)
        {
            if (IsServer) return; // 서버는 지점이 원본이라 되먹임이 불필요
            if (_event.Index < 0 || _event.Index >= m_Spots.Count) return;
            m_Spots[_event.Index].ApplyState(_event.Value.Remaining, _event.Value.Earner);
        }

        private void PushAllToSpots()
        {
            if (IsServer) return;
            int count = Mathf.Min(m_States.Count, m_Spots.Count);
            for (int i = 0; i < count; i++)
            {
                m_Spots[i].ApplyState(m_States[i].Remaining, m_States[i].Earner);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            bool started = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (started && !m_WasStarted) ResetAll();
            m_WasStarted = started;

            bool matchRunning = started && (m_Match == null || m_Match.Result == MatchResult.None);
            TickSpots(matchRunning);

            if (!started || m_RevivedThisMatch) return;
            m_Timer += Time.deltaTime;
            if (m_Timer < POLL_SECONDS) return;
            m_Timer = 0f;

            if (!matchRunning || !AllDepleted()) return;
            ReviveBestSpot();
        }

        private void TickSpots(bool _matchRunning)
        {
            EnsureStateCount();
            for (int i = 0; i < m_Spots.Count; i++)
            {
                if (m_Spots[i] == null) continue;
                if (!m_Spots[i].ServerTick(Time.deltaTime, _matchRunning)) continue;
                WriteState(i);
            }
        }

        private void WriteState(int _index)
        {
            if (_index < 0 || _index >= m_States.Count) return;
            m_States[_index] = new CheeseSpotState
            {
                Remaining = m_Spots[_index].Remaining,
                Earner = m_Spots[_index].EarnerClientId,
            };
        }

        private void EnsureStateCount()
        {
            while (m_States.Count > m_Spots.Count) m_States.RemoveAt(m_States.Count - 1);
            while (m_States.Count < m_Spots.Count) m_States.Add(default);
        }

        private void ResetAll()
        {
            m_RevivedThisMatch = false;
            int reserve = CheeseSpotLogic.ReservePerSpot(m_Spots.Count);
            EnsureStateCount();
            for (int i = 0; i < m_Spots.Count; i++)
            {
                if (m_Spots[i] == null) continue;
                m_Spots[i].ServerSetReserve(reserve);
                WriteState(i);
            }
        }

        /// <summary>서버 전용 QA — 전 지점 즉시 고갈.</summary>
        public void ServerDrainAll()
        {
            if (!IsServer) return;
            EnsureStateCount();
            for (int i = 0; i < m_Spots.Count; i++)
            {
                if (m_Spots[i] == null) continue;
                m_Spots[i].ServerSetReserve(0);
                WriteState(i);
            }
        }

        private bool AllDepleted()
        {
            bool any = false;
            foreach (CheeseSpot spot in m_Spots)
            {
                if (spot == null) continue;
                any = true;
                if (!spot.Depleted) return false;
            }
            return any;
        }

        // 최고 등급 = 맵 중앙의 최고 수입 지점. 전원이 여기로 모이게 된다.
        private void ReviveBestSpot()
        {
            int best = -1;
            for (int i = 0; i < m_Spots.Count; i++)
            {
                if (m_Spots[i] == null) continue;
                if (best < 0 || m_Spots[i].Grade > m_Spots[best].Grade) best = i;
            }
            if (best < 0) return;

            m_Spots[best].ServerSetReserve(CheeseSpotLogic.LAST_STAND_RESERVE);
            WriteState(best);
            m_RevivedThisMatch = true;
            Debug.Log($"[CheeseSpotDirector] 전 지점 고갈 → G{m_Spots[best].Grade} 지점 부활 " +
                      $"({CheeseSpotLogic.LAST_STAND_RESERVE})");
        }
    }
}
