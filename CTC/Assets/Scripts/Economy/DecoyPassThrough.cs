using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 미끼는 쥐만 통과한다 — 고양이는 부딪힌다(2026-07-29 요청).
    ///
    /// 쥐 쪽: 도주 중 발밑에 떨군 미끼에 제가 걸려 넘어지면 카드로서 최악이다.
    /// 고양이 쪽: 몸으로 막히는 순간 "쥐다"라는 오인이 확정된다 — 소리에 이어 두 번째 속임수다.
    ///
    /// 충돌 무시는 콜라이더 쌍 단위(Physics.IgnoreCollision)라 각 클라이언트의 로컬 물리에
    /// 걸어야 한다. CharacterController 이동은 소유자 클라에서 시뮬레이션되므로 이 컴포넌트는
    /// 서버·클라 구분 없이 모두에서 돈다. 늦게 스폰된 쥐(재접속·관전 복귀)도 잡으려고
    /// 수명 동안 주기적으로 다시 훑는다 — 미끼는 30초짜리라 비용이 문제되지 않는다.
    /// </summary>
    public class DecoyPassThrough : MonoBehaviour
    {
        private const float RESCAN_SECONDS = 1f;

        private Collider m_Body;
        private float m_Timer;

        private void Awake()
        {
            m_Body = GetComponentInChildren<Collider>();
        }

        private void Start()
        {
            IgnoreMice();
        }

        private void Update()
        {
            m_Timer += Time.deltaTime;
            if (m_Timer < RESCAN_SECONDS) return;
            m_Timer = 0f;
            IgnoreMice();
        }

        private void IgnoreMice()
        {
            if (m_Body == null) return;

            foreach (RoleTag tag in FindObjectsByType<RoleTag>())
            {
                if (tag.Role != RoleId.Mouse) continue;

                // 요새·창고·다른 미끼도 RoleTag가 쥐다 — CharacterController가 있는 것만 플레이어
                var cc = tag.GetComponent<CharacterController>();
                if (cc != null) Physics.IgnoreCollision(m_Body, cc, true);
            }
        }
    }
}
