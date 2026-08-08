namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 쥐는 미끼를 통과하고 고양이는 부딪힌다(2026-07-29 요청) — 레이어 충돌 매트릭스로 구현.
    /// 여기 남은 건 **레이어 이름 두 개**뿐이다. 설정 자체는 코드가 아니라 프로젝트에 있다.
    ///
    /// 앞서 두 방법이 실패했다. 둘 다 **CharacterController에는 적용되지 않는다**:
    /// - Physics.IgnoreCollision(미끼, 쥐 CC) → 쥐도 고양이도 미끼 앞 0.7m에서 정지
    /// - CC.excludeLayers |= 미끼 레이어  → 동일하게 정지
    /// CC 이동은 PhysX 캐릭터 컨트롤러의 자체 스윕이라 콜라이더 단위 필터를 보지 않고,
    /// **레이어 충돌 매트릭스만** 존중한다. 그래서 쥐를 전용 레이어로 옮기고 매트릭스에서 끊는다.
    ///
    /// 지금 상태(2026-08-01):
    /// - 레이어 `Decoy`(8)·`Mouse`(9) → `ProjectSettings/TagManager.asset` (커밋됨)
    /// - `Decoy × Mouse` 끔 → `ProjectSettings/DynamicsManager.asset` (커밋됨)
    /// - 프리팹 레이어 칠하기 + 위 매트릭스 굽기 → `Tools/CTC/Setup Networking Scene`
    ///
    /// 런타임 적용 코드는 삭제했다 — 세팅이 이미 커밋돼 있어 할 일이 없었다.
    /// **주의**: 매트릭스는 레이어 *번호*로 저장된다. 레이어가 8·9가 아닌 슬롯에 앉으면
    /// 구워둔 칸이 어긋난다 — 그때는 생성기를 다시 돌려 매트릭스를 다시 굽는다.
    /// </summary>
    public static class DecoyLayers
    {
        public const string DECOY = "Decoy";
        public const string MOUSE = "Mouse";
    }
}
