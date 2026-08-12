namespace CTC.Runtime.Ai.Bt
{
    /// <summary>자식을 여럿 갖는 노드의 공통 부분.</summary>
    public abstract class BtComposite : BtNode
    {
        protected readonly BtNode[] m_Children;

        /// <summary>다음 프레임에 이어서 볼 자식. Running을 돌려준 자리를 기억한다.</summary>
        protected int m_Resume;

        protected BtComposite(params BtNode[] _children)
        {
            m_Children = _children ?? new BtNode[0];
        }

        public override void Reset()
        {
            m_Resume = 0;
            for (int i = 0; i < m_Children.Length; i++) m_Children[i].Reset();
        }
    }

    /// <summary>
    /// **전부 성공해야 성공.** 하나라도 실패하면 거기서 멈춘다.
    /// "조건 → 조건 → 행동" 꼴로 쓴다.
    /// </summary>
    public sealed class BtSequence : BtComposite
    {
        public BtSequence(params BtNode[] _children) : base(_children) { }

        public override BtStatus Tick(float _deltaSeconds)
        {
            for (int i = m_Resume; i < m_Children.Length; i++)
            {
                BtStatus status = m_Children[i].Tick(_deltaSeconds);
                if (status == BtStatus.Running)
                {
                    m_Resume = i;
                    return BtStatus.Running;
                }
                if (status == BtStatus.Failure)
                {
                    Reset();
                    return BtStatus.Failure;
                }
            }

            Reset();
            return BtStatus.Success;
        }
    }

    /// <summary>
    /// **하나만 성공하면 성공.** 위에서부터 시도하다 되는 걸 만나면 멈춘다.
    /// **위아래 순서가 곧 우선순위**다 — 봇의 판단 규칙이 여기 적힌다.
    /// </summary>
    public sealed class BtSelector : BtComposite
    {
        public BtSelector(params BtNode[] _children) : base(_children) { }

        public override BtStatus Tick(float _deltaSeconds)
        {
            for (int i = m_Resume; i < m_Children.Length; i++)
            {
                BtStatus status = m_Children[i].Tick(_deltaSeconds);
                if (status == BtStatus.Running)
                {
                    m_Resume = i;
                    return BtStatus.Running;
                }
                if (status == BtStatus.Success)
                {
                    Reset();
                    return BtStatus.Success;
                }
            }

            Reset();
            return BtStatus.Failure;
        }
    }

    /// <summary>
    /// 자식 하나를 무작위로 골라 굴린다. 배회처럼 **매번 같으면 안 되는** 행동에 쓴다.
    /// 씨앗을 밖에서 받는다 — 같은 판을 다시 돌리면 같은 결과가 나와야 QA가 성립한다.
    /// </summary>
    public sealed class BtRandomSelector : BtComposite
    {
        private readonly System.Random m_Rng;
        private int m_Picked = -1;

        public BtRandomSelector(System.Random _rng, params BtNode[] _children) : base(_children)
        {
            m_Rng = _rng;
        }

        public override BtStatus Tick(float _deltaSeconds)
        {
            if (m_Children.Length == 0) return BtStatus.Failure;
            if (m_Picked < 0) m_Picked = m_Rng != null ? m_Rng.Next(m_Children.Length) : 0;

            BtStatus status = m_Children[m_Picked].Tick(_deltaSeconds);
            if (status != BtStatus.Running) Reset();
            return status;
        }

        public override void Reset()
        {
            m_Picked = -1;
            base.Reset();
        }
    }

    /// <summary>
    /// 자식이 뭘 돌려주든 **성공**으로 바꾼다(Running은 그대로).
    /// "해 보고 안 되면 그냥 넘어가라"를 표현한다.
    /// </summary>
    public sealed class BtSucceeder : BtNode
    {
        private readonly BtNode m_Child;

        public BtSucceeder(BtNode _child)
        {
            m_Child = _child;
        }

        public override BtStatus Tick(float _deltaSeconds)
        {
            if (m_Child == null) return BtStatus.Success;

            BtStatus status = m_Child.Tick(_deltaSeconds);
            return status == BtStatus.Running ? BtStatus.Running : BtStatus.Success;
        }

        public override void Reset()
        {
            if (m_Child != null) m_Child.Reset();
        }
    }
}
