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
    /// 매치 승패 관리(LobbyController GO의 in-scene NetworkObject에 동승).
    /// 서버가 0.5s 간격으로 생존 수를 세어 판정하고 결과를 NetworkVariable로 복제.
    /// 재시작: 비-씬 NetworkObject 전부 despawn → 결과 리셋 → 로비 복귀(역할 유지).
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        private const float POLL_INTERVAL_SECONDS = 0.5f;

        [SerializeField] private LobbyController m_Lobby;

        // MatchResult. 서버 기록, 전원 읽기(ResultHud).
        private readonly NetworkVariable<int> m_Result = new NetworkVariable<int>();

        // 2단계 감독(v3.9) — 같은 오브젝트에 동승한다.
        private PhaseDirector m_Phase;

        // 남은 시간(초). 서버 기록, 전원 읽기(TimerHud). 시작 전/재시작 후엔 음수(-1)로 비활성.
        private readonly NetworkVariable<float> m_RemainingSeconds = new NetworkVariable<float>(-1f);

        private bool m_Armed;
        private float m_Timer;
        private bool m_WasStarted;

        public MatchResult Result => (MatchResult)m_Result.Value;

        private void Awake()
        {
            m_Phase = GetComponent<PhaseDirector>();
        }

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

            // 2단계(v3.9)에 들어갔으면 판정 주체가 통째로 바뀐다 — 고양이는 이미 없다.
            if (m_Phase != null && m_Phase.IsGangPhase)
            {
                MatchResult gang = m_Phase.EvaluateGangPhase();
                if (gang == MatchResult.None) return;

                m_Result.Value = (int)gang;
                Debug.Log($"[MatchManager] 2단계 종료: {gang} (남은시간={m_Phase.Remaining:F0}s)");
                return;
            }

            MatchResult result = m_RemainingSeconds.Value <= 0f && m_Armed
                ? MatchFlowLogic.EvaluateAtTimeUp(mice)
                : MatchFlowLogic.Evaluate(m_Armed, cats, mice);
            if (result == MatchResult.None) return;

            // 고양이가 죽어 쥐가 이기려는 참이다. **깡패가 있으면 판이 안 끝나고 2단계로 간다**(v3.9).
            if (result == MatchResult.MouseWin && cats <= 0
                && m_Phase != null && m_Phase.ServerTryEnterGangPhase())
            {
                Debug.Log("[MatchManager] 고양이 사망 — 깡패가 있어 2단계로 간다");
                return;
            }

            m_Result.Value = (int)result;
            Debug.Log($"[MatchManager] 매치 종료: {result} (cats={cats}, mice={mice}, 남은시간={m_RemainingSeconds.Value:F0}s)");
        }

        // 쥐 = 스폰된 플레이어 오브젝트 존재(영구 탈락=despawn). 고양이 = hp>0 (사망해도 despawn 안 함).
        // 요새/창고도 RoleTag.Mouse라 "몸" 필터가 필수.
        private static void CountAlive(out int _cats, out int _mice)
        {
            _cats = 0;
            _mice = 0;
            foreach (RoleTag tag in FindObjectsByType<RoleTag>(FindObjectsSortMode.None))
            {
                var no = tag.GetComponent<NetworkObject>();
                if (no == null || !no.IsSpawned) continue;

                // **판의 참가자 = 플레이어의 몸**(2026-08-11). 예전엔 `IsPlayerObject`로 걸렀는데
                // 그건 "클라이언트에 붙었나"라는 네트워크 구현 사실이지 참가 여부가 아니다.
                // 측정용 봇은 붙일 클라이언트가 없어 그 필터에 안 걸리고, 그러면 자유 쥐가 0으로
                // 세어져 **판이 시작하자마자 끝난다**. 요새·창고·벽감·강아지는 이 컴포넌트가 없다.
                if (tag.GetComponent<Player.PlayerMovement>() == null) continue;

                Health health = tag.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                if (tag.Role == RoleId.Cat) { _cats++; continue; }
                if (tag.Role == RoleId.Mouse) _mice++;
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

            // **단계도 되돌린다.** 안 되돌리면 2단계(깡패)로 끝난 판 뒤에 Phase가 Gang으로 남아,
            // 다음 판이 서자마자 `EvaluateGangPhase`가 「깡패 0 · 나머지 0」을 보고 즉시 끝낸다
            // (2026-09-09 감사 P0-5 — ServerReset은 그때까지 호출자가 0인 죽은 코드였다).
            if (m_Phase != null) m_Phase.ServerReset();

            // 같은 클래스 Update(:52)는 널을 보는데 여기만 안 봤다 — 씬 배선이 빠지면 재시작에서 터졌다.
            if (m_Lobby != null) m_Lobby.ResetToLobby();
            else Debug.LogWarning("[MatchManager] LobbyController가 안 붙어 있다 — 로비 복귀를 건너뛴다");

            Debug.Log($"[MatchManager] 재시작 — 로비 복귀 (동적 오브젝트 {toDespawn.Count}개 정리)");
        }

        /// <summary>QA: 타이머 강제 설정(서버 전용) — 만료 판정 검증용.</summary>
        public void DevSetRemaining(float _seconds)
        {
            if (IsServer) m_RemainingSeconds.Value = _seconds;
        }
    }
}
