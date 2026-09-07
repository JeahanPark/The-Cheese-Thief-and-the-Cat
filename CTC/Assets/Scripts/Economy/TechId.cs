namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 테크 = 판 안에서 얻는 모든 구매·업그레이드 (테크 v3, 2026-09-07).
    /// **값은 NetworkList·CSV에 직렬화될 것이므로 바꾸지 않는다** — 새 테크는 뒤에만 추가한다.
    /// 계획 1은 기존 8항목만 쓴다. 쥐 테크는 계획 2a에서 전 레벨 행이 들어왔고,
    /// 고양이 신규 4종(CatHide·CatHunger·CatFrenzy·CatEar)만 아직 행이 없다.
    ///
    /// `TechInfo.csv`가 **판매 목록의 단일 원본**이다(2026-09-07 — 구 `ShopInfo.csv`는 제거됐다).
    /// 가격은 테크 v3 기획 값이라 구 표와 일부러 다르다 — 예: Pepper 200→120(초반형으로 재조정),
    /// Fortress 100(설치 "권한" 구매) + 설치마다 `BalanceInfo.FortressPlaceCost` 80.
    /// </summary>
    public enum TechId
    {
        None = 0,
        // 쥐
        // Lv1 새총 150 · Lv2 기관총 600(가격 봉우리) · Lv3 긴 총열 450 — 사거리·피해만 얹어 일부러 더 싸다(기획서 §4-1)
        Gun = 1,             // 총 — Lv1 새총 · Lv2 기관총 (기존 MousePopgun 티어)
        Pepper = 2,          // 후추 탄 (기존 PepperRounds, 200→120 — 테크 v3 초반형 재조정)
        // HP·반사의 살아 있는 원본은 **TechInfo P1/P2**다(480·900·1200 / 0·0·25).
        // BalanceInfo.FortressHp는 이제 프리팹을 구울 때의 기본값 겸 표를 못 읽었을 때의 대체값일 뿐이다.
        Fortress = 3,        // 요새 — Lv1 설치 "권한" 구매(100), 설치마다 FortressPlaceCost(80)
        CheeseShield = 4,    // 치즈 방패 (계획 2)
        // 창고 — 설치권 50 + 설치마다 BalanceInfo.WarehousePlaceCost 100 (기존 WarehouseProduction).
        // 첫 창고 = 50 + 100 = 150으로 옛 값 그대로다. 고양이가 부순 뒤 다시 짓는 건 100 —
        // 재설치가 공짜면 째기 응징이 안 된다(2026-09-07).
        Warehouse = 5,
        BigPouch = 6,        // 큰 주머니 (계획 2)
        HiddenWarehouse = 7, // 숨은 창고 (계획 2)
        Sticky = 8,          // 끈끈이 (계획 2)
        Dash = 9,            // 대시 (기존 MouseDash)
        FastFeet = 10,       // 빠른 발 (계획 2)
        // 고양이
        CatClaw = 20,        // 발톱 (기존 CatPounce 스택)
        CatHide = 21,        // 두꺼운 가죽 (계획 2)
        CatHunger = 22,      // 배고픔 (계획 2)
        CatFrenzy = 23,      // 광분 (계획 2)
        CatStomp = 24,       // 발 구르기 (기존 PlayerMovement 스택)
        CatEar = 25,         // 예민한 귀 (계획 2)
    }

    /// <summary>5축 (사용자 정의 2026-09-06). CSV `Axis.TechAxis` 컬럼이 이 이름을 쓴다.</summary>
    public enum TechAxis
    {
        Attack = 0,
        Defense = 1,
        Resource = 2,
        Escape = 3,
        Utility = 4,
    }
}
