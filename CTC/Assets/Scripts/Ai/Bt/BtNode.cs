namespace CTC.Runtime.Ai.Bt
{
    /// <summary>행동트리 노드가 부모에게 돌려주는 답.</summary>
    public enum BtStatus
    {
        /// <summary>끝났다 — 성공.</summary>
        Success = 0,

        /// <summary>못 했다 — 부모가 다음 자식을 본다.</summary>
        Failure = 1,

        /// <summary>
        /// 하는 중이다. 부모는 여기서 멈추고, **다음 프레임에 이 자식부터** 이어서 본다.
        /// 이게 없으면 "3초 동안 걸어간다" 같은 행동을 트리로 못 쓴다.
        /// </summary>
        Running = 2,
    }

    /// <summary>
    /// 행동트리의 노드 (2026-08-12).
    ///
    /// **왜 트리인가**: 그전에는 `Decide()`가 if 순서로 우선순위를 정했다. 규칙이 함수 안에
    /// 숨어 있어 "왜 이 봇이 지금 이걸 하나"를 읽으려면 코드를 따라가야 했고, 행동을 하나
    /// 끼우려면 그 함수를 다시 짜야 했다.
    ///
    /// 트리는 **위아래 순서가 곧 우선순위**다. 노드가 서로를 모르므로 쥐·고양이·강아지가
    /// 같은 잎을 나눠 쓸 수 있고, 하나씩 따로 검증된다.
    ///
    /// 씬을 모른다(순수 C#). 필요한 값은 트리를 조립하는 쪽이 델리게이트로 넣어 준다 —
    /// 그래서 EditMode가 트리 전체를 그대로 굴려 볼 수 있다.
    /// </summary>
    public abstract class BtNode
    {
        /// <summary>한 번 굴린다.</summary>
        public abstract BtStatus Tick(float _deltaSeconds);

        /// <summary>진행 상태를 버린다(부모가 다른 가지로 갈아탈 때).</summary>
        public virtual void Reset() { }
    }
}
