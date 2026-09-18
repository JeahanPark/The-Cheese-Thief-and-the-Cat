# -*- coding: utf-8 -*-
"""테크 뼈대 + 상점 자리 검증 — 테크 v3 계획 1 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=쥐** — `TestBuyTech`/`TestUseTech`는 소유자 RPC라 **제 몸에만** 듣는다.
쥐가 사고·쓰는 쪽이 대부분이므로 호스트가 쥐여야 한 인스턴스에서 다 몰아볼 수 있다.
고양이(클론)는 소유자 제약 때문에 `TestBuyTechLocal`로 제 인스턴스에서 산다.

세팅: `python setup_match.py 0 mouse 3` (호스트=쥐, 클론 하나가 고양이)
한 판에 한 번만 돌린다 — 칸을 5개까지 채우고 요새를 세우므로 잔여물이 남는다.

오늘의 품절(계획 3)은 판마다 쥐 테크 둘을 막는다 — 이 스크립트는 **효과**를 재는 것이라
`TestClearSoldOut`으로 주사위를 먼저 걷는다. 품절 자체의 검증은 `qa_v50`이 한다.
다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 12      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다. 붙이면 출력이 곧
    # 「무엇이 언제 돌았나」가 되고, 선언과 어긋나면 대장 드리프트 테스트가 깨진다.
    if case:
        name = "[%s] %s" % (case, name)
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def skip(name, why):
    """돌지 않은 항목을 **결과에 남긴다**. 조용한 건너뛰기는 통과가 아니다."""
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def note(name, detail):
    """분모에 안 들어가는 참고 기록 — 판정을 바꾸지 않는다."""
    EXTRA.append((name, detail))
    print("[NOTE] " + name + "  " + detail)
    sys.stdout.flush()


def wallet_of(host, client_id):
    """그 몸의 (잔액, 누적 획득) — 두 값 사이에 수입이 끼면 다시 읽는다.

    쓴 돈은 **잔액 차이 + 그 사이 번 돈**이다. 잔액만 보면 상점 곁 채굴 수입이 섞여
    「80을 냈는데 74가 빠졌다」·「거절됐는데 치즈가 5 늘었다」가 된다(2026-09-19 실측).
    """
    for _ in range(5):
        e0 = host.invoke("TestGetEarnedOf", _clientId=client_id)
        c = host.invoke("TestGetCheeseOf", _clientId=client_id)
        e1 = host.invoke("TestGetEarnedOf", _clientId=client_id)
        if e0 == e1:
            return c, e1
    return c, e1


def warp_to_shop(host, client_id):
    """가장 가까운 상점 자리로 옮긴다 — qa_v36과 같은 규약.

    `TestWarpToNearestShop`은 `BodyPush.ServerPush` 경유라 **호스트 인스턴스**에서 불러야 하고,
    그 안에서 다시 소유자 RPC를 타므로 반환값은 "보냈다"지 "도착했다"가 아니다 — 쉬었다 확인한다.
    """
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.0)
    return bool(host.invoke("TestIsInShop", _clientId=client_id))


def move_out_of_shops(host, actor, client_id):
    """`actor`의 몸을 **모든 상점 원반 밖**으로 옮긴다 → 실제로 밖에 섰는가(bool).

    상점 자리는 판마다 시드로 정해진다(`ShopSpotLogic`, 계획 3). 그래서 고양이 스폰이
    상점 원반 안에 들어앉는 판이 있다 — 실측: 기본 맵 상점 (0.0, 1.5) 반경 3 m,
    고양이 스폰 (0.0, -0.25) → 1.75 m로 원반 안. 그 상태로 「상점 밖에서도 산다」를
    재면 전제부터 거짓이라 항목이 아무것도 검증하지 않는다.

    그래서 **전제를 가정하지 말고 세운다**: 맵 격자를 훑어 상점에서 가장 먼 칸으로
    옮긴 뒤 `TestIsInShop`으로 확인한다. 옮기는 것은 소유자만 되므로 그 몸의
    인스턴스(`actor`)에서 부르고, 읽기는 서버(`host`)에서 한다.
    """
    raw = host.invoke("TestGetShopSpotPositions") or ""
    spots = []
    for chunk in raw.split("|"):
        if "," not in chunk:
            continue
        sx, sz = chunk.split(",")
        spots.append((float(sx), float(sz)))
    if not spots:
        return not bool(host.invoke("TestIsInShop", _clientId=client_id))

    shape = host.invoke("TestGetMapShape") or ""
    half = 15.0
    for part in shape.split(";"):
        if part.startswith("half="):
            half = float(part[5:])
    edge = max(4.0, half - 3.0)

    best, best_gap = None, -1.0
    step = max(2.0, edge / 12.0)
    y = -edge
    while y <= edge:
        x = -edge
        while x <= edge:
            gap = min(((x - sx) ** 2 + (y - sz) ** 2) ** 0.5 for sx, sz in spots)
            if gap > best_gap:
                best, best_gap = (x, y), gap
            x += step
        y += step

    actor.invoke("TestTeleportLocalPlayer", _x=best[0], _z=best[1])
    time.sleep(1.5)
    return not bool(host.invoke("TestIsInShop", _clientId=client_id))


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [int(x.invoke("TestGetLocalClientId")) for x in p]
    roles = [x.invoke("TestGetRole") for x in p]
    ok_setup = check("3인 접속 · 호스트=쥐 · 고양이 1",
                     all(i >= 0 for i in ids) and roles[0] == "Mouse" and roles.count("Cat") == 1,
                     "roles=%s ids=%s" % (roles, ids))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 0 mouse 3`으로 판을 다시 연다")
        return summary(aborted=True)

    host = p[0]                       # 호스트 = 쥐. 제 몸이라 소유자 RPC 훅이 다 듣는다
    m = ids[0]

    # 오늘의 품절이 판마다 쥐 테크 둘을 막는다(테크 v3 계획 3). 이 스크립트가 재는 것은
    # **테크 효과**라 열 종류를 다 사야 하고, 그러면 한 판으로는 구조적으로 불가능해진다 —
    # 2026-09-08 실측에서 그 판에 품절이던 두 항목만 빨간 줄이 떴다. 주사위를 치우고 효과만 잰다.
    # **품절 자체는 qa_v50이 본다** — 여기서 끄는 것이 그 검증을 덮지 않는다.
    host.invoke("TestClearSoldOut")
    time.sleep(0.5)
    left = host.invoke("TestGetSoldOutTechs")
    # 예전엔 여기서 print만 하고 **그대로 진행**했다 — 품절이 안 걷히면 아래 구매가
    # 그 테크에서 전부 거절돼 **거짓 FAIL 무더기**가 난다(감사 E §2-3). 전제로 승격했다.
    if not check("전제 — 오늘의 품절을 걷었다", not left,
                 "남은 품절 %s" % (left or "없음")):
        print("!! 품절이 안 걷혔다 — 아래 구매가 그 테크에서 거절되므로 여기서 멈춘다")
        return summary(aborted=True)
    cat_idx = roles.index("Cat")
    cat = p[cat_idx]
    c = ids[cat_idx]

    # 지갑을 먼저 채운다 — ②의 거절이 "돈이 없어서"가 아니라 "상점 밖이라서"여야 한다
    host.invoke("TestGrantCheeseAll")
    time.sleep(0.4)
    host.invoke("TestGrantCheeseAll")
    time.sleep(0.6)

    # ---------- ① 상점 자리가 맵에 섰다 ----------
    # CSV의 ShopSpotCount(4)와 어긋나면 ShopSpotSpawner 배선 사고다.
    spots = host.invoke("TestGetShopSpotCount")
    check("① 상점 자리가 맵에 섰다", spots >= 1, "%s곳 (CSV 기대 4)" % spots)

    gun_cost = host.invoke("TestGetTechCost", _techId="Gun", _level=1)

    # ---------- ② 상점 밖 구매는 거절된다 ----------
    # 스폰 자리가 우연히 상점 안일 수 있어, 재기 전에 치즈 지점으로 치운다.
    sx, sz = [float(v) for v in host.invoke("TestGetBestCheeseSpotPos").split(",")]
    host.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(0.8)
    outside = not host.invoke("TestIsInShop", _clientId=m)
    cheese_before = host.invoke("TestGetCheeseOf", _clientId=m)
    host.invoke("TestBuyTech", _clientId=m, _techId="Gun")
    time.sleep(1.0)
    lv_outside = host.invoke("TestGetTechLevel", _clientId=m, _techId="Gun")
    cheese_after = host.invoke("TestGetCheeseOf", _clientId=m)
    # 치즈는 **늘 수 있다** — 지점 위에 서 있으면 초당 채굴 수입이 들어온다(실측 +8).
    # 그래서 "그대로"가 아니라 "값이 안 빠졌다"를 잰다.
    check("② 상점 밖에서 산 것은 거절된다(치즈도 안 빠진다)",
          outside and lv_outside == 0 and cheese_after >= cheese_before,
          "밖=%s lv=%s 치즈 %s→%s (값이 있어도 %s 필요)"
          % (outside, lv_outside, cheese_before, cheese_after, gun_cost))

    # ---------- ③ 상점 안 구매 · 업그레이드는 같은 칸 ----------
    in_shop = warp_to_shop(host, m)
    check("③-a 상점 자리 안으로 들어갔다", in_shop, "inShop=%s" % in_shop)

    host.invoke("TestBuyTech", _clientId=m, _techId="Gun")
    time.sleep(1.0)
    lv1 = host.invoke("TestGetTechLevel", _clientId=m, _techId="Gun")
    check("③-b 상점 안에서 Gun Lv1 구매", lv1 == 1, "lv=%s" % lv1)

    host.invoke("TestBuyTech", _clientId=m, _techId="Gun")
    time.sleep(1.0)
    lv2 = host.invoke("TestGetTechLevel", _clientId=m, _techId="Gun")
    slots1 = host.invoke("TestGetTechSlotCount", _clientId=m)
    check("③-c 업그레이드는 새 칸을 안 먹는다(Gun Lv2 · 칸 1)", lv2 == 2 and slots1 == 1,
          "lv=%s 칸=%s" % (lv2, slots1))

    # ---------- ④ 칸 5개까지 채운다 ----------
    for tech in ("Pepper", "Fortress", "Warehouse", "Dash"):
        host.invoke("TestBuyTech", _clientId=m, _techId=tech)
        time.sleep(0.9)
    slots5 = host.invoke("TestGetTechSlotCount", _clientId=m)
    levels = {t: host.invoke("TestGetTechLevel", _clientId=m, _techId=t)
              for t in ("Gun", "Pepper", "Fortress", "Warehouse", "Dash")}
    check("④ 테크 5종 보유 = 칸 5", slots5 == 5, "칸=%s %s" % (slots5, levels))

    # ---------- ⑤ 요새는 설치권과 별개로 설치비가 나간다 ----------
    # 설치권(TechInfo Lv1)은 위에서 이미 샀다. 여기서 재는 건 **쓸 때마다 나가는 설치비**다.
    # 서 있는 요새 안이면 겹치기 금지로 거절되므로 상점 자리에서 그대로 세운다(아직 요새 없음).
    place_cost = host.invoke("TestGetPlaceCost", _techId="Fortress")
    fc0, fe0 = wallet_of(host, m)
    host.invoke("TestUseTech", _clientId=m, _techId="Fortress")
    time.sleep(1.5)
    fc1, fe1 = wallet_of(host, m)
    fort_spent = (fc0 - fc1) + (fe1 - fe0)
    fort_pos = host.invoke("TestGetFortressPos")        # 실제로 선 FortressShield — Model 값이 아니다
    check("⑤ 요새 설치비가 설치마다 나간다", fort_spent == place_cost,
          "%s 나감 (기대 %s)" % (fort_spent, place_cost))
    check("⑤-b 요새가 실제로 씬에 섰다", fort_pos != "", "pos=%s" % fort_pos)

    # ---------- ⑥ 고양이는 상점 없이 산다 ----------
    # 상점 자리는 쥐 규칙(TechLogic.NeedsShopSpot)이다 — 고양이가 묶이면 사냥이 멈춘다.
    # 고양이는 클론이라 소유자 RPC 제약 때문에 **제 인스턴스**에서 산다.
    # 전제(상점 밖)를 **가정하지 말고 세운다** — 상점 자리는 판마다 바뀌어 고양이 스폰이
    # 원반 안에 들어앉는 판이 있다.
    cat_outside = move_out_of_shops(host, cat, c)
    cat.invoke("TestBuyTechLocal", _techId="CatClaw")
    time.sleep(1.2)
    claw = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatClaw")
    check("⑥ 고양이는 상점 밖에서도 산다(CatClaw Lv1)", cat_outside and claw == 1,
          "상점밖=%s lv=%s" % (cat_outside, claw))

    # ---------- ⑦ 역할이 다른 테크는 칸을 안 늘린다 ----------
    # 쥐가 고양이 테크를 사면 WrongRole로 거절 — 칸이 늘면 6번째가 새는 것이다.
    before_slots = host.invoke("TestGetTechSlotCount", _clientId=m)
    wc0, we0 = wallet_of(host, m)
    host.invoke("TestBuyTech", _clientId=m, _techId="CatClaw")
    time.sleep(1.2)
    after_slots = host.invoke("TestGetTechSlotCount", _clientId=m)
    wrong_lv = host.invoke("TestGetTechLevel", _clientId=m, _techId="CatClaw")
    wc1, we1 = wallet_of(host, m)
    wrong_spent = (wc0 - wc1) + (we1 - we0)
    check("⑦ 쥐가 고양이 테크를 사면 거절(칸 5 유지 · 치즈 안 나감)",
          before_slots == 5 and after_slots == 5 and wrong_lv == 0 and wrong_spent == 0,
          "칸 %s→%s lv=%s 치즈 %s→%s(그 사이 번 돈 %s) → 쓴 돈 %s"
          % (before_slots, after_slots, wrong_lv, wc0, wc1, we1 - we0, wrong_spent))

    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다.

    **안 돈 항목은 통과가 아니다.** 조건부로 건너뛴 항목이 결과에 안 남으면
    "16/16 통과"가 실은 24항목 중 8개를 안 돈 것일 수 있다(감사 E §2-4).
    실패 · 미실행 · 중단은 전부 종료 코드 1이다 — CI가 읽을 수 있어야 한다.
    """
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  | " + detail if detail else ""))
    for name, detail in EXTRA:
        print("  NOTE " + name + ("  | " + detail if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    unrun = EXPECTED - len(RESULTS)
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d · NOTE %d"
          % (passed, failed, skipped, unrun, EXPECTED, len(EXTRA)))
    if unrun > 0:
        print("!! 선언 %d · 실행 %d — 안 돈 항목은 통과가 아니다" % (EXPECTED, len(RESULTS)))
    if unrun < 0:
        print("!! 실행 %d > 선언 %d — EXPECTED를 고쳐라" % (len(RESULTS), EXPECTED))
    if aborted:
        print("!! 중단·열화된 실행이다 — 통과로 읽지 마라")
    return 0 if (failed == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
