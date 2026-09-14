namespace CTC.Runtime.Fx
{
    /// <summary>
    /// 기술 이펙트 목록(2026-09-14). 값은 <see cref="FxCatalog"/> 배열 자리와 1:1이다 — 순서를 바꾸면 이펙트가 뒤섞인다.
    /// 지운 자리는 번호를 비워 둔다(<c>SfxId</c>와 같은 규칙).
    ///
    /// **넣지 않은 것**(기획서 §11 「없던 정보를 만들지 않는다」): 패시브(큰 주머니·빠른 발·숨은 창고·고양이 잠복/허기/발 구르기/예민한 귀),
    /// 켜져 있는 동안 도는 오라(불·바람·광분 — "아직 켜져 있다"를 흘린다), 후추 도트 틱(테크 레벨), 치즈 방패 파편(지갑).
    /// </summary>
    public enum FxId
    {
        // 쥐
        MuzzleFlash = 0,
        BulletImpact = 1,
        PlaceDust = 2,        // 요새·창고가 서는 자리
        FortressPush = 3,     // 요새가 고양이를 밀어낸 자리 — 충격파 링
        ThornSpark = 4,       // 가시 벽 반사
        StickySplat = 5,
        DashDust = 6,
        BuyCoins = 7,
        EatCrumbs = 8,
        CheesePickup = 9,
        HeroBurst = 10,
        GangSmoke = 11,
        ClubSmash = 12,       // 깡패 몽둥이 내려치기(2026-09-14)

        // 고양이
        ClawSlash = 20,
        AdvanceFire = 21,
        AdvanceWind = 22,
        AdvanceLight = 23,
        AdvanceDark = 24,
        SkillFire = 25,
        SkillWind = 26,
        SkillLight = 27,
        SkillDark = 28,
        FrenzyShock = 29,
    }
}
