using System.Collections.Generic;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using CTC.Runtime.Core;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 매치 승패 관리(LobbyManager GO의 in-scene NetworkObject에 동승).
    /// 서버가 0.5s 간격으로 생존 수를 세어 판정하고 결과를 NetworkVariable로 복제.
    /// 재시작: 비-씬 NetworkObject 전부 despawn → 결과 리셋 → 로비 복귀(역할 유지).
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        private const float POLL_INTERVAL_SECONDS = 0.5f;

        [SerializeField] private LobbyManager m_Lobby;

        // MatchResult. 서버 기록, 전원 읽기(ResultHud).
        private readonly NetworkVariable<int> m_Result = new NetworkVariable<int>();

        // 남은 시간(초). 서버 기록, 전원 읽기(TimerHud). 시작 전/재시작 후엔 음수(-1)로 비활성.
        private readonly NetworkVariable<float> m_RemainingSeconds = new NetworkVariable<float>(-1f);

        private bool m_Armed;
        private float m_Timer;
        private bool m_WasStarted;

        public MatchResult Result => (MatchResult)m_Result.Value;

        /// <summary>결과 스트림(View용) — 구독 즉시 현재 값이 흐른다.</summary>
        public Observable<MatchResult> ResultChanged =>
            m_Result.AsObservableWithCurrent().Select(v => (MatchResult)v);

        public float RemainingSeconds => m_RemainingSeconds.Value;

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            bool started = m_Lobby != null && m_Lobby.GameStarted;
            if (started && !m_WasStarted)
            {
                m_RemainingSeconds.Value = MatchFlowLogic.MATCH_DURATION_SECONDS;
            }
            m_WasStarted = started;

            if (!started || Result != MatchResult.None) return;

            // 타이머
            m_RemainingSeconds.Value = Mathf.Max(0f, m_RemainingSeconds.Value - Time.deltaTime);

            m_Timer += Time.deltaTime;
            if (m_Timer < POLL_INTERVAL_SECONDS && m_RemainingSeconds.Value > 0f) return;
            m_Timer = 0f;

            CountAlive(out int cats, out int mice);
            if (MatchFlowLogic.ShouldArm(started, m_Armed, cats, mice)) m_Armed = true;

            MatchResult result = m_RemainingSeconds.Value <= 0f && m_Armed
                ? MatchFlowLogic.EvaluateAtTimeUp(mice)
                : MatchFlowLogic.Evaluate(m_Armed, cats, mice);
            if (result == MatchResult.None) return;

            m_Result.Value = (int)result;
            Debug.Log($"[MatchManager] 매치 종료: {result} (cats={cats}, mice={mice}, 남은시간={m_RemainingSeconds.Value:F0}s)");
        }

        // 쥐 = 스폰된 플레이어 오브젝트 존재(탈락=despawn). 고양이 = hp>0 (사망해도 despawn 안 함).
        // 요새/미끼도 RoleTag.Mouse라 IsPlayerObject 필터가 필수.
        private static void CountAlive(out int _cats, out int _mice)
        {
            _cats = 0;
            _mice = 0;
            foreach (RoleTag tag in FindObjectsByType<RoleTag>(FindObjectsSortMode.None))
            {
                var no = tag.GetComponent<NetworkObject>();
                if (no == null || !no.IsSpawned || !no.IsPlayerObject) continue;

                Health health = tag.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                if (tag.Role == RoleId.Cat) _cats++;
                else if (tag.Role == RoleId.Mouse) _mice++;
            }
        }

        /// <summary>호스트 Restart 버튼/QA. 서버에서만 동작.</summary>
        public void RequestRestart()
        {
            if (!IsServer || Result == MatchResult.None) return;

            // 스냅샷 후 순회 — despawn이 SpawnedObjects를 변경한다
            var toDespawn = new List<NetworkObject>();
            foreach (KeyValuePair<ulong, NetworkObject> kv in NetworkManager.SpawnManager.SpawnedObjects)
            {
                NetworkObject no = kv.Value;
                if (no != null && !no.IsSceneObject.GetValueOrDefault()) toDespawn.Add(no);
            }
            foreach (NetworkObject no in toDespawn)
            {
                no.Despawn(true);
            }

            m_Armed = false;
            m_Result.Value = (int)MatchResult.None;
            m_RemainingSeconds.Value = -1f;
            m_Lobby.ResetToLobby();
            Debug.Log($"[MatchManager] 재시작 — 로비 복귀 (동적 오브젝트 {toDespawn.Count}개 정리)");
        }

        /// <summary>QA: 타이머 강제 설정(서버 전용) — 만료 판정 검증용.</summary>
        public void DevSetRemaining(float _seconds)
        {
            if (IsServer) m_RemainingSeconds.Value = _seconds;
        }
    }
}
