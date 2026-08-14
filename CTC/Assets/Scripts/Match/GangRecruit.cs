using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CTC.Runtime.Match
{
    /// <summary>
    /// 깡패쥐가 다른 쥐를 부하로 임명한다 (v3.9). **거절은 없다** —
    /// 다가가서 F를 누르면 그걸로 끝이다.
    ///
    /// 거절을 안 두는 이유: 거절할 수 있으면 아무도 부하가 되지 않고, 깡패권이 죽은 콘텐츠가 된다.
    /// 대신 승리 조건이 팀에 묶여 있어 부하도 이길 이유가 생긴다(배신할 이유가 없다).
    ///
    /// 판정은 서버가 한다 — 거리를 클라가 주장하면 맵 반대편에서도 임명할 수 있다.
    /// </summary>
    public class GangRecruit : NetworkBehaviour
    {
        // 첫 임명 여부 — 표식(JobBadge)이 이걸 보고 보스를 숨긴다(2026-08-14).
        // 임명 전의 보스는 "누가 위험분자인가"가 공개 정보고,
        // 임명을 시작한 보스는 숨어서 세를 불린다.
        private readonly NetworkVariable<bool> m_HasRecruited = new NetworkVariable<bool>();

        public bool HasRecruited => m_HasRecruited.Value;
        public Observable<bool> HasRecruitedChanged => m_HasRecruited.AsObservableWithCurrent();

        private MouseJob m_Job;

        private void Awake()
        {
            m_Job = GetComponent<MouseJob>();
        }

        private void Update()
        {
            if (!IsOwner || m_Job == null) return;
            if (m_Job.Value != MouseJobId.Gang) return;          // 깡패만 쓴다

            if (UiFocus.BlocksGameplay) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.fKey.wasPressedThisFrame) return;

            RecruitRpc();
        }

        /// <summary>QA 전용 — 키 없이 임명한다.</summary>
        public void DevRecruit() => RecruitRpc();

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void RecruitRpc()
        {
            if (m_Job.Value != MouseJobId.Gang) return;

            MouseJob nearest = FindNearestRecruitable();
            if (nearest == null) return;

            nearest.ServerSetJob(MouseJobId.Minion);
            m_HasRecruited.Value = true;
        }

        // 가장 가까운 '아직 깡패 편이 아닌 쥐'. 갇힌 쥐는 임명하지 않는다 —
        // 감옥 안에서 팀이 바뀌면 누가 구하러 가야 하는지가 뒤집힌다.
        private MouseJob FindNearestRecruitable()
        {
            MouseJob best = null;
            float bestDistance = float.MaxValue;

            foreach (MouseJob other in FindObjectsByType<MouseJob>(FindObjectsSortMode.None))
            {
                if (other == m_Job) continue;

                var tag = other.GetComponent<RoleTag>();
                if (tag == null || tag.Role != RoleId.Mouse) continue;

                var jailed = other.GetComponent<JailedTag>();
                if (jailed != null && jailed.Jailed) continue;

                float distance = Vector3.Distance(transform.position, other.transform.position);
                if (!MouseJobLogic.CanRecruit(m_Job.Value, other.Value, distance, MouseJobLogic.RecruitRadius))
                {
                    continue;
                }

                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = other;
            }
            return best;
        }
    }
}
