using System.Collections.Generic;
using CTC.Runtime.Ai.Bt;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 행동트리 뼈대. 여기가 무너지면 봇·강아지의 **모든** 판단이 같이 무너진다.
    /// </summary>
    public class BtNodeTests
    {
        // 부를 때마다 정해진 답을 차례로 돌려주는 잎 — 몇 번 불렸는지도 센다.
        private sealed class Scripted : BtNode
        {
            private readonly Queue<BtStatus> m_Answers;
            public int Ticks { get; private set; }
            public int Resets { get; private set; }

            public Scripted(params BtStatus[] _answers)
            {
                m_Answers = new Queue<BtStatus>(_answers);
            }

            public override BtStatus Tick(float _deltaSeconds)
            {
                Ticks++;
                return m_Answers.Count > 0 ? m_Answers.Dequeue() : BtStatus.Success;
            }

            public override void Reset() => Resets++;
        }

        [Test]
        public void Sequence_AllSucceed_Succeeds()
        {
            var a = new Scripted(BtStatus.Success);
            var b = new Scripted(BtStatus.Success);

            Assert.AreEqual(BtStatus.Success, new BtSequence(a, b).Tick(0.1f));
            Assert.AreEqual(1, b.Ticks);
        }

        [Test]
        public void Sequence_OneFails_StopsThere()
        {
            var a = new Scripted(BtStatus.Failure);
            var b = new Scripted(BtStatus.Success);

            Assert.AreEqual(BtStatus.Failure, new BtSequence(a, b).Tick(0.1f));
            Assert.AreEqual(0, b.Ticks, "앞이 실패했으면 뒤는 보지도 않는다");
        }

        [Test]
        public void Sequence_Running_ResumesAtTheSameChildNextFrame()
        {
            // 문서(5p)의 핵심: RUNNING이면 다음 프레임에 지나온 자식을 건너뛰고 그 자리부터 잇는다.
            var a = new Scripted(BtStatus.Success, BtStatus.Success);
            var b = new Scripted(BtStatus.Running, BtStatus.Success);
            var seq = new BtSequence(a, b);

            Assert.AreEqual(BtStatus.Running, seq.Tick(0.1f));
            Assert.AreEqual(1, a.Ticks);

            Assert.AreEqual(BtStatus.Success, seq.Tick(0.1f));
            Assert.AreEqual(1, a.Ticks, "이미 성공한 앞 자식을 다시 굴리면 안 된다");
            Assert.AreEqual(2, b.Ticks);
        }

        [Test]
        public void Selector_FirstSuccessWins()
        {
            var a = new Scripted(BtStatus.Success);
            var b = new Scripted(BtStatus.Success);

            Assert.AreEqual(BtStatus.Success, new BtSelector(a, b).Tick(0.1f));
            Assert.AreEqual(0, b.Ticks, "위가 됐으면 아래는 안 본다 — 이게 우선순위다");
        }

        [Test]
        public void Selector_AllFail_Fails()
        {
            var a = new Scripted(BtStatus.Failure);
            var b = new Scripted(BtStatus.Failure);

            Assert.AreEqual(BtStatus.Failure, new BtSelector(a, b).Tick(0.1f));
            Assert.AreEqual(1, b.Ticks);
        }

        [Test]
        public void Selector_Running_ResumesAtTheSameChild()
        {
            var a = new Scripted(BtStatus.Failure, BtStatus.Failure);
            var b = new Scripted(BtStatus.Running, BtStatus.Success);
            var sel = new BtSelector(a, b);

            Assert.AreEqual(BtStatus.Running, sel.Tick(0.1f));
            Assert.AreEqual(BtStatus.Success, sel.Tick(0.1f));
            Assert.AreEqual(1, a.Ticks, "진행 중이던 자식부터 이어야 한다");
        }

        [Test]
        public void Condition_MapsBoolToStatus()
        {
            Assert.AreEqual(BtStatus.Success, new BtCondition(() => true).Tick(0f));
            Assert.AreEqual(BtStatus.Failure, new BtCondition(() => false).Tick(0f));
        }

        [Test]
        public void Action_Once_RunsAndSucceeds()
        {
            int calls = 0;
            Assert.AreEqual(BtStatus.Success, BtAction.Once(() => calls++).Tick(0f));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Succeeder_TurnsFailureIntoSuccess()
        {
            var child = new Scripted(BtStatus.Failure);
            Assert.AreEqual(BtStatus.Success, new BtSucceeder(child).Tick(0f));
        }

        [Test]
        public void Succeeder_KeepsRunning()
        {
            var child = new Scripted(BtStatus.Running);
            Assert.AreEqual(BtStatus.Running, new BtSucceeder(child).Tick(0f));
        }

        [Test]
        public void RandomSelector_IsDeterministicForTheSameSeed()
        {
            // 같은 씨앗이면 같은 결과여야 QA가 재현된다.
            int PickWith(int seed)
            {
                var a = new Scripted(BtStatus.Success);
                var b = new Scripted(BtStatus.Success);
                new BtRandomSelector(new System.Random(seed), a, b).Tick(0f);
                return a.Ticks > 0 ? 0 : 1;
            }

            Assert.AreEqual(PickWith(1234), PickWith(1234));
        }
    }
}
