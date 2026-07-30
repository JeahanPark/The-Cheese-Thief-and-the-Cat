using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 밸런스 데이터 로거(서버 전용, 스펙 v3.3). 매치 중 킬·파괴 타임라인과 참가자 최종 지갑을
    /// 기록해 종료 시 persistentDataPath/MatchStats/match_*.json으로 덤프.
    /// 플레이테스트 몇 판이면 승률·판 길이·첫 킬 시점이 자동 집계된다.
    /// </summary>
    public class MatchStatsRecorder : MonoBehaviour
    {
        [System.Serializable]
        private class KillEvent
        {
            public float timeSec;
            public string victimRole;
            public bool isPlayer; // false = 요새/미끼 파괴
        }

        [System.Serializable]
        private class PlayerEntry
        {
            public ulong clientId;
            public string role;
            public int finalCheese;
        }

        [System.Serializable]
        private class MatchRecord
        {
            public string endedAtUtc;
            public string winner;
            public float durationSec;
            public bool timeUp;
            public List<KillEvent> kills = new List<KillEvent>();
            public List<PlayerEntry> players = new List<PlayerEntry>();
        }

        [SerializeField] private LobbyController m_Lobby;
        [SerializeField] private MatchManager m_Match;

        private readonly HashSet<Health> m_Hooked = new HashSet<Health>();
        private readonly Dictionary<ulong, PlayerEntry> m_Players = new Dictionary<ulong, PlayerEntry>();
        private MatchRecord m_Record;
        private float m_StartTime;
        private bool m_WasStarted;
        private MatchResult m_LastResult = MatchResult.None;

        private void Update()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            bool started = m_Lobby != null && m_Lobby.IsSpawned && m_Lobby.GameStarted;
            if (started && !m_WasStarted) BeginRecord();
            m_WasStarted = started;
            if (!started || m_Record == null) return;

            TrackEntities();

            MatchResult result = m_Match != null && m_Match.IsSpawned ? m_Match.Result : MatchResult.None;
            if (result != m_LastResult && result != MatchResult.None) EndRecord(result);
            m_LastResult = result;
        }

        private void BeginRecord()
        {
            foreach (Health h in m_Hooked)
            {
                if (h != null) h.Died -= OnDied;
            }
            m_Hooked.Clear();
            m_Players.Clear();
            m_Record = new MatchRecord();
            m_StartTime = Time.time;
            m_LastResult = MatchResult.None;
        }

        // 스폰된 Health 구독(사망 이벤트) + 플레이어 지갑 최신값 갱신
        private void TrackEntities()
        {
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (m_Hooked.Add(h)) h.Died += OnDied;

                var no = h.GetComponent<NetworkObject>();
                if (no == null || !no.IsSpawned || !no.IsPlayerObject) continue;

                if (!m_Players.TryGetValue(no.OwnerClientId, out PlayerEntry entry))
                {
                    RoleTag tag = h.GetComponent<RoleTag>();
                    entry = new PlayerEntry
                    {
                        clientId = no.OwnerClientId,
                        role = tag != null ? tag.Role.ToString() : "?",
                    };
                    m_Players.Add(no.OwnerClientId, entry);
                    m_Record.players.Add(entry);
                }

                CheeseWallet wallet = h.GetComponent<CheeseWallet>();
                if (wallet != null) entry.finalCheese = wallet.CheeseValue;
            }
        }

        private void OnDied(Health _health)
        {
            if (m_Record == null || _health == null) return;

            RoleTag tag = _health.GetComponent<RoleTag>();
            var no = _health.GetComponent<NetworkObject>();
            m_Record.kills.Add(new KillEvent
            {
                timeSec = Time.time - m_StartTime,
                victimRole = tag != null ? tag.Role.ToString() : "?",
                isPlayer = no != null && no.IsPlayerObject,
            });
        }

        private void EndRecord(MatchResult _result)
        {
            m_Record.endedAtUtc = System.DateTime.UtcNow.ToString("o");
            m_Record.winner = _result.ToString();
            m_Record.durationSec = Time.time - m_StartTime;
            m_Record.timeUp = m_Match != null && m_Match.RemainingSeconds <= 0f;

            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "MatchStats");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(
                    dir, $"match_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(m_Record, true));
                Debug.Log($"[MatchStats] 저장: {path} (winner={m_Record.winner}, " +
                          $"{m_Record.durationSec:F0}s, kills={m_Record.kills.Count})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MatchStats] 저장 실패: {e.Message}");
            }
            m_Record = null;
        }
    }
}
