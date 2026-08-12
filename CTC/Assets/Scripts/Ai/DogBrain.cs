using System.Collections.Generic;
using CTC.Runtime.Ai.Bt;
using CTC.Runtime.Combat;
using CTC.Runtime.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Ai
{
    /// <summary>
    /// 강아지가 **무엇을** 할지 정하는 서버 상태기 (v3.8 5번). 어떻게 가는지는 `DogMover`,
    /// 어떻게 무는지는 `DogBite`가 맡는다.
    ///
    /// 표적은 `CatPounce`와 **같은 판별면**을 쓴다 — 씬의 `Health`를 훑는다. 새 개념이 없고
    /// 요새·미끼가 자동으로 표적이 된다. 제외는 셋뿐이다:
    /// ① 벽감 — 강아지가 치즈 벽을 열면 "강아지를 풀어 파밍한다"가 성립해 무리수가 도구로 뒤집힌다.
    /// ② 다른 강아지 — 판에 하나뿐이지만 방어적으로 막는다.
    /// ③ **갇힌 쥐** — 못 뛰고 못 싸운다. 물면 감옥이 사형장이 되고 구출이라는 축이 사라진다.
    ///
    /// 훑기는 매 프레임이 아니라 `DogRepathSeconds`마다 한다. `FindObjectsByType`은 비싸고,
    /// 표적이 프레임 단위로 바뀔 이유도 없다.
    /// </summary>
    public class DogBrain : NetworkBehaviour
    {
        private readonly List<DogTarget> m_Candidates = new List<DogTarget>();
        private readonly List<Health> m_Healths = new List<Health>();

        private Health m_Target;
        private float m_SecondsSinceSeen;
        private float m_ScanTimer;
        private BtNode m_Tree;

        /// <summary>서버 판정 결과. 클라는 볼 일이 없다(연출은 Rpc로 따로 간다).</summary>
        public DogState State { get; private set; } = DogState.Roam;

        /// <summary>지금 쫓는 것. 없으면 null.</summary>
        public Health Target => m_Target;

        private void Update()
        {
            if (!IsServer) return;

            float dt = Time.deltaTime;

            m_ScanTimer -= dt;
            if (m_ScanTimer <= 0f)
            {
                m_ScanTimer = DogAiLogic.RepathSeconds;
                Rescan();
            }

            if (m_Target == null || m_Target.IsDead)
            {
                Forget();
                return;
            }

            bool visible = !SightLine.IsBlocked(transform, m_Target.transform);
            m_SecondsSinceSeen = visible ? 0f : m_SecondsSinceSeen + dt;

            if (DogAiLogic.ShouldDropAggro(visible, m_SecondsSinceSeen, DogAiLogic.AggroDropSeconds))
            {
                Forget();
                return;
            }

            if (m_Tree == null) m_Tree = Build();
            m_Tree.Tick(dt);
        }

        /// <summary>
        /// 강아지도 **봇과 같은 행동트리**를 쓴다 (2026-08-12).
        ///
        /// 기술소개서(리그오브레전드 7p)의 "몬스터가 달라도 행동을 공용으로 이용한다"가 그대로
        /// 적용된다 — 노드가 서로를 몰라서 쥐·고양이·강아지가 같은 뼈대를 나눠 쓴다.
        ///
        /// **지휘관은 없다.** 강아지는 제3세력이고 한 마리뿐이라 명령할 상대가 없다.
        /// <code>
        /// Selector
        /// ├ Seq [사거리 안] → 문다
        /// ├ Seq [표적 있다] → 쫓는다
        /// └ 배회
        /// </code>
        /// </summary>
        private BtNode Build()
        {
            return new BtSelector(
                new BtSequence(
                    new BtCondition(() => m_Target != null && DistanceToTarget() <= DogAiLogic.BiteRange),
                    BtAction.Once(() => State = DogState.Bite)),
                new BtSequence(
                    new BtCondition(() => m_Target != null),
                    BtAction.Once(() => State = DogState.Chase)),
                BtAction.Once(() => State = DogState.Roam));
        }

        private float DistanceToTarget()
        {
            if (m_Target == null) return float.MaxValue;
            return DogAiLogic.Flat(m_Target.transform.position - transform.position).magnitude;
        }

        private void Forget()
        {
            m_Target = null;
            m_SecondsSinceSeen = 0f;
            State = DogState.Roam;
        }

        // 표적을 새로 고른다. 이미 쫓는 게 보이는 동안에는 바꾸지 않는다 —
        // 매번 가장 가까운 쪽으로 갈아타면 두 사람 사이에서 진동한다.
        private void Rescan()
        {
            if (m_Target != null && !m_Target.IsDead && !SightLine.IsBlocked(transform, m_Target.transform)) return;

            m_Candidates.Clear();
            m_Healths.Clear();

            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health == null || health.IsDead) continue;
                if (health.transform.root == transform.root) continue;
                if (health.GetComponent<Map.Alcove>() != null) continue;
                if (health.GetComponent<DogBrain>() != null) continue;

                var jailed = health.GetComponent<Match.JailedTag>();
                if (jailed != null && jailed.Jailed) continue;

                RoleTag tag = health.GetComponent<RoleTag>();
                bool isCat = tag != null && tag.Role == RoleId.Cat;
                bool visible = !SightLine.IsBlocked(transform, health.transform);

                m_Candidates.Add(new DogTarget((ulong)m_Healths.Count, health.transform.position, isCat, visible));
                m_Healths.Add(health);
            }

            ulong chosen = DogAiLogic.ChooseTarget(m_Candidates, transform.position, DogAiLogic.DetectRadius);
            if (chosen == DogAiLogic.NO_TARGET)
            {
                Forget();
                return;
            }

            m_Target = m_Healths[(int)chosen];
            m_SecondsSinceSeen = 0f;
        }
    }
}
