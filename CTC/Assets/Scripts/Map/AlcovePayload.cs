namespace CTC.Runtime.Map
{
    /// <summary>벽감을 부수면 나오는 것. 값은 프리팹·레이아웃에 직렬화되므로 바꾸지 않는다.</summary>
    public enum AlcovePayload
    {
        /// <summary>즉시 획득 치즈 여러 덩이 — 쥐끼리의 달리기 경쟁을 만든다.</summary>
        Cheese = 0,

        /// <summary>
        /// 강아지 — 불리한 쪽이 판을 엎는 카드. **아직 미구현(v3.8 5번 스펙)**.
        /// 자리만 잡아 둔다: 이 값이 있어야 레이아웃·생성기가 페이로드 종류를 표현할 수 있다.
        /// </summary>
        Dog = 1,
    }
}
