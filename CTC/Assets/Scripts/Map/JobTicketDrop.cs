using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 바닥에 떨어진 직업권 (v3.9). 쥐가 **닿기만 하면** 그 직업이 되고 사라진다.
    /// 치즈 덩이(`CheeseDrop`)와 같은 형식이다 — 상자가 뿌리고, 먼저 닿는 놈이 임자.
    ///
    /// **이미 직업이 있는 쥐는 못 줍는다.** 안 그러면 한 명이 다 주워 직업이 독점되고,
    /// 특히 깡패권을 용사가 덮어써서 2단계가 영영 안 열릴 수 있다.
    ///
    /// 고양이는 못 줍는다. 판정은 서버만 한다 — 둘이 같은 프레임에 닿아도 먼저 처리된 쪽이 갖는다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class JobTicketDrop : NetworkBehaviour
    {
        [SerializeField] private MouseJobId m_Job = MouseJobId.Hero;

        private bool m_Taken;

        /// <summary>서버 전용 — 스폰 직전에 상자가 무슨 권인지 새긴다.</summary>
        public void ServerSetJob(MouseJobId _job)
        {
            m_Job = _job;
        }

        /// <summary>QA·표시용.</summary>
        public MouseJobId Job => m_Job;

        private void OnTriggerEnter(Collider _other)
        {
            if (!IsServer || m_Taken) return;

            NetworkObject no = _other.GetComponentInParent<NetworkObject>();
            if (no == null || !no.IsPlayerObject) return;

            var tag = no.GetComponent<Combat.RoleTag>();
            if (tag == null || tag.Role != RoleId.Mouse) return;

            var job = no.GetComponent<MouseJob>();
            if (job == null || job.Value != MouseJobId.None) return;   // 한 명이 다 줍지 못하게

            m_Taken = true;
            job.ServerSetJob(m_Job);
            NetworkObject.Despawn(true);
        }
    }
}
