using System;

namespace CTC.Runtime.Ai.Bt
{
    /// <summary>
    /// **묻는다.** 참이면 성공, 거짓이면 실패. 자식은 못 갖는다.
    /// `[보인다]` `[사거리 안이다]` 같은 것들이 이 노드다.
    /// </summary>
    public sealed class BtCondition : BtNode
    {
        private readonly Func<bool> m_Test;

        public BtCondition(Func<bool> _test)
        {
            m_Test = _test;
        }

        public override BtStatus Tick(float _deltaSeconds)
        {
            return m_Test != null && m_Test() ? BtStatus.Success : BtStatus.Failure;
        }
    }

    /// <summary>
    /// **한다.** 돌려주는 값이 곧 이 행동의 결과다 —
    /// 여러 프레임에 걸치는 행동은 `Running`을 돌려주면 다음 프레임에 여기부터 이어진다.
    /// </summary>
    public sealed class BtAction : BtNode
    {
        private readonly Func<float, BtStatus> m_Run;
        private readonly Action m_OnReset;

        public BtAction(Func<float, BtStatus> _run, Action _onReset = null)
        {
            m_Run = _run;
            m_OnReset = _onReset;
        }

        /// <summary>한 번에 끝나는 행동 — 언제나 성공.</summary>
        public static BtAction Once(Action _run)
        {
            return new BtAction(_ =>
            {
                if (_run != null) _run();
                return BtStatus.Success;
            });
        }

        public override BtStatus Tick(float _deltaSeconds)
        {
            return m_Run != null ? m_Run(_deltaSeconds) : BtStatus.Failure;
        }

        public override void Reset()
        {
            if (m_OnReset != null) m_OnReset();
        }
    }
}
