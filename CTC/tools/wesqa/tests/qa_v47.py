# -*- coding: utf-8 -*-
"""쥐 테크 효과 9항목 — 테크 v3 계획 2a (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=쥐** — 세팅: `python setup_match.py 0 mouse 3` (클론 하나가 고양이, 나머지 하나는 쥐).

왜 쥐가 둘 필요한가: 칸이 **5개뿐**이다(TechSlotCap). 재야 할 쥐 테크는 10개라 한 몸에 다 못 든다.
그래서 둘로 나눈다 —
  호스트 쥐(m) : Gun · Pepper · Fortress · CheeseShield · Sticky   → ①②③④⑧
  클론 쥐(m2)  : BigPouch · Warehouse · HiddenWarehouse · Dash · FastFeet → ⑤⑥⑦⑨

`TestBuyTech`/`TestUseTech`는 소유자 RPC라 **제 인스턴스에서만** 듣는다 — 그래서 구매·사용은
언제나 그 몸의 인스턴스에서 `TestBuyTechLocal`/`TestUseTechLocal`로 부르고, **읽기**만 호스트에서 한다.
`TestGetMaxHpOf`·`TestGetNearestFortressMaxHp`는 `Health.MaxHp`가 복제되지 않아 **호스트에서만** 참이다.

값은 전부 훅에서 읽는다(`TestGetTechCost`·`TestGetPlaceCost`) — 가격을 손으로 적지 않는다.
한 판에 한 번만 돌린다(칸을 다 채우고 요새·창고·장판이 남는다). 다시 돌리려면 플레이모드를 껐다 켠다.

오늘의 품절(계획 3)은 판마다 쥐 테크 둘을 막는다 — 이 스크립트는 **효과**를 재는 것이라
`TestClearSoldOut`으로 주사위를 먼저 걷는다. 품절 자체의 검증은 `qa_v50`이 한다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def warp_to_shop(host, client_id):
    """가장 가까운 상점 자리로 옮긴다 — qa_v36·v46과 같은 규약.

    `TestWarpToNearestShop`은 `BodyPush.ServerPush` 경유라 **호스트 인스턴스**에서 불러야 하고,
    그 안에서 다시 소유자 RPC를 타므로 반환값은 "보냈다"지 "도착했다"가 아니다 — 쉬었다 확인한다.
    """
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.2)
    return bool(host.invoke("TestIsInShop", _clientId=client_id))


def buy_to(inst, host, client_id, tech, level):
    """그 몸의 인스턴스에서 목표 레벨까지 산다. 값은 CSV 훅에서 읽어 로그로만 남긴다."""
    costs = [host.invoke("TestGetTechCost", _techId=tech, _level=lv) for lv in range(1, level + 1)]
    for _ in range(level):
        inst.invoke("TestBuyTechLocal", _techId=tech)
        time.sleep(0.9)
    got = host.invoke("TestGetTechLevel", _clientId=client_id, _techId=tech)
    print("    %s Lv%s 구매 시도 (비용 %s) → Lv%s" % (tech, level, costs, got))
    sys.stdout.flush()
    return got


def try_place_warehouses(inst, host, client_id, cx, cz, want):
    """창고를 want개 세울 때까지 자리를 바꿔 가며 시도한다.

    `PlacementFits`는 벽·구조물에 걸리면 **조용히** 거절한다(로그는 그 인스턴스에만 남는다).
    좌표 하나만 찍고 실패하면 "창고 상한 2"를 재는 게 아니라 "그 자리가 비었나"를 재게 된다.
    설치비는 자리 검증을 통과한 뒤에만 빠지므로 헛시도는 공짜다.
    """
    offsets = [(0, 3), (3, 0), (0, -3), (-3, 0), (2, 2), (-2, 2), (2, -2), (-2, -2),
               (0, 5), (5, 0), (0, -5), (-5, 0)]
    tried = 0
    for ox, oz in offsets:
        if host.invoke("TestGetWarehouseCountOf", _clientId=client_id) >= want:
            break
        inst.invoke("TestPlaceWarehouseAt", _x=cx + ox, _z=cz + oz)
        tried += 1
        time.sleep(1.6)
    got = host.invoke("TestGetWarehouseCountOf", _clientId=client_id)
    print("    창고 설치 %d자리 시도 → %d개" % (tried, got))
    sys.stdout.flush()
    return got


def pounce_for_damage(cat, host, target_id, attempts=3):
    """고양이를 목표 앞에 세우고 실제로 HP가 깎일 때까지 덮친다 → (깎인 HP, 시도 수).

    한 번 쳐서 0이 나와도 "안 맞는다"가 아니다 — 덮치기는 **차폐**(요새 벽·치즈 지점 같은 콜라이더)에
    막히면 그 대상만 건너뛴다. 자리를 다시 잡고 다시 친다.
    """
    for i in range(1, attempts + 1):
        cat.invoke("TestTeleportToOtherPlayer", _clientId=target_id)
        time.sleep(1.5)
        before = host.invoke("TestGetHpOf", _clientId=target_id)
        cat.invoke("TestPounce")
        time.sleep(1.5)
        loss = before - host.invoke("TestGetHpOf", _clientId=target_id)
        if loss > 0:
            return loss, i
        time.sleep(3.0)   # 덮치기 쿨
    return 0, attempts


def sample_income(host, client_id, seconds):
    """지갑이 seconds 동안 얼마나 늘었나 — 채굴·창고 수입 계측의 공통 자."""
    before = host.invoke("TestGetCheeseOf", _clientId=client_id)
    time.sleep(seconds)
    return host.invoke("TestGetCheeseOf", _clientId=client_id) - before


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [int(x.invoke("TestGetLocalClientId")) for x in p]
    roles = [x.invoke("TestGetRole") for x in p]
    ok_setup = check("⓪ 3인 접속 · 호스트=쥐 · 고양이 1 · 쥐 2",
                     all(i >= 0 for i in ids) and roles[0] == "Mouse"
                     and roles.count("Cat") == 1 and roles.count("Mouse") == 2,
                     "roles=%s ids=%s" % (roles, ids))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 0 mouse 3`으로 판을 다시 연다")
        return 1

    host = p[0]
    m = ids[0]
    cat_idx = roles.index("Cat")
    cat, c = p[cat_idx], ids[cat_idx]
    m2_idx = [i for i in range(3) if i != 0 and i != cat_idx][0]
    m2i, m2 = p[m2_idx], ids[m2_idx]
    print("호스트 쥐=%s · 고양이=%s(inst %s) · 클론 쥐=%s(inst %s)" % (m, c, cat_idx, m2, m2_idx))

    # 오늘의 품절이 판마다 쥐 테크 둘을 막는다(테크 v3 계획 3). 이 스크립트가 재는 것은
    # **테크 효과**라 열 종류를 다 사야 하고, 그러면 한 판으로는 구조적으로 불가능해진다 —
    # 2026-09-08 실측에서 그 판에 품절이던 숨은 창고·빠른 발에서만 빨간 줄이 떴다.
    # 주사위를 치우고 효과만 잰다. **품절 자체는 qa_v50이 본다.**
    host.invoke("TestClearSoldOut")
    time.sleep(0.5)
    left = host.invoke("TestGetSoldOutTechs")
    if left:
        print("!! 품절이 안 걷혔다: %s — 아래 구매가 그 테크에서 거절된다" % left)

    # 지갑 채우기 — 두 쥐가 5레벨씩 사고 설치까지 하므로 넉넉히. **계측 전에 다 준다**(수입 계측 오염 방지).
    for _ in range(7):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)
    time.sleep(0.6)
    print("치즈: 쥐1=%s 쥐2=%s" % (host.invoke("TestGetCheeseOf", _clientId=m),
                                   host.invoke("TestGetCheeseOf", _clientId=m2)))

    spot = host.invoke("TestGetBestCheeseSpotPos")
    sx, sz = [float(v) for v in spot.split(",")]

    # ================= 클론 쥐(m2) — 자원·이동 =================

    # ---------- ⑤ 큰 주머니 Lv3 — 채굴 지갑 적립 ×1.9 ----------
    # 창고를 세우기 **전에** 잰다 — 창고 수입이 섞이면 배율이 뜻을 잃는다.
    m2i.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(2.0)
    base_gain = sample_income(host, m2, 8.0)
    earner = host.invoke("TestGetCheeseEarnerNear", _x=sx, _z=sz)

    warp_to_shop(host, m2)
    pouch = buy_to(m2i, host, m2, "BigPouch", 3)

    m2i.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(2.0)
    boost_gain = sample_income(host, m2, 8.0)
    ratio = (float(boost_gain) / base_gain) if base_gain > 0 else 0.0
    want = 1.9   # TechInfo BigPouch Lv3 P1 — 정수 캐리라 딱 떨어지지는 않는다
    check("⑤ 큰 주머니 Lv3 — 채굴 적립이 약 1.9배",
          pouch == 3 and base_gain > 0 and 1.5 <= ratio <= 2.4,
          "lv=%s 채굴 %s→%s (×%.2f, 기대 ×%s) earner=%s"
          % (pouch, base_gain, boost_gain, ratio, want, earner))

    # ---------- ⑨ 빠른 발 Lv3 + 대시 Lv3 ----------
    speed_before = host.invoke("TestGetMoveSpeedOf", _clientId=m2)
    warp_to_shop(host, m2)
    feet = buy_to(m2i, host, m2, "FastFeet", 3)
    dash = buy_to(m2i, host, m2, "Dash", 3)
    time.sleep(0.8)
    speed_after = host.invoke("TestGetMoveSpeedOf", _clientId=m2)
    cat_speed = host.invoke("TestGetMoveSpeedOf", _clientId=c)
    gain = speed_after - speed_before
    check("⑨ 빠른 발 Lv3 이속 +1.2 · 대시 Lv3 · 그래도 고양이보다 느리다",
          feet == 3 and dash == 3 and abs(gain - 1.2) < 0.05 and speed_after < cat_speed,
          "이속 %.2f→%.2f (+%.2f) 고양이 %.2f · FastFeet=%s Dash=%s"
          % (speed_before, speed_after, gain, cat_speed, feet, dash))

    # ---------- ⑥⑦ 창고 Lv2(상한 2) + 숨은 창고 Lv3(HP 450) ----------
    # 숨은 창고를 **세우기 전에** 사야 한다 — MaxHp는 스폰 시 한 번 박힌다.
    wh = buy_to(m2i, host, m2, "Warehouse", 2)
    hidden = buy_to(m2i, host, m2, "HiddenWarehouse", 3)

    place_cost = host.invoke("TestGetPlaceCost", _techId="Warehouse")
    hp_before = host.invoke("TestGetLowestStructureHp")
    m2i.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(1.2)
    wh_count = try_place_warehouses(m2i, host, m2, sx, sz, 2)
    check("⑥ 창고 Lv2 — 창고 2개까지 선다", wh == 2 and wh_count == 2,
          "Lv=%s 창고=%s개 (설치비 %s/개)" % (wh, wh_count, place_cost))

    # 창고 HP는 구조물 최저 HP로 잰다 — 아직 요새가 없어 가장 무른 구조물이 창고다.
    # (소리 0m 무음은 **재는 훅이 없다** — 아래 SKIP 참고)
    hp_after = host.invoke("TestGetLowestStructureHp")
    check("⑦-a 숨은 창고 Lv3 — 창고 HP 150→450",
          hidden == 3 and hp_after == 450,
          "Lv=%s 구조물 최저 HP %s→%s (기대 450)" % (hidden, hp_before, hp_after))
    skip("⑦-b 숨은 창고 Lv3 — 생산음 무음(0m)",
         "재는 훅이 없다 — `SfxPlayer.PlayAt`의 실제 재생 여부를 세는 훅(예: TestGetSfxPlayCount)이 필요")

    # ================= 호스트 쥐(m) — 전투·방어·도주 =================

    in_shop = warp_to_shop(host, m)
    check("①-a 호스트 쥐가 상점 자리 안에 섰다", in_shop, "inShop=%s" % in_shop)

    # ---------- ① 총 Lv3 ----------
    gun = buy_to(host, host, m, "Gun", 3)
    check("① 총 Lv3(긴 총열) 보유", gun == 3, "lv=%s" % gun)

    # ---------- ② 후추 Lv3 — 도트가 6초 간다 ----------
    pep = buy_to(host, host, m, "Pepper", 3)
    cat.invoke("TestTeleportToOtherPlayer", _clientId=m)
    time.sleep(1.5)
    hp0 = host.invoke("TestGetHpOf", _clientId=c)
    host.invoke("TestShootAtCat")
    time.sleep(1.0)
    hp1 = host.invoke("TestGetHpOf", _clientId=c)      # 명중 직후
    time.sleep(2.5)
    hp2 = host.invoke("TestGetHpOf", _clientId=c)      # +3.5s — 도트가 아직 돈다
    time.sleep(2.5)
    hp3 = host.invoke("TestGetHpOf", _clientId=c)      # +6.0s — 도트 마지막 즈음
    time.sleep(2.5)
    hp4 = host.invoke("TestGetHpOf", _clientId=c)      # +8.5s — 끝났어야 한다
    check("② 후추 Lv3 — 도트가 6초간 계속 깎는다",
          pep == 3 and hp1 < hp0 and hp3 < hp2 and hp4 == hp3,
          "lv=%s 고양이 HP %s→%s(명중)→%s(+3.5s)→%s(+6s)→%s(+8.5s)" % (pep, hp0, hp1, hp2, hp3, hp4))

    # ---------- ④ 치즈 방패 Lv3 — 피해 60%를 치즈로 ----------
    # 기준선은 **방패 없는 다른 쥐**다. 쥐 HP는 100, 덮치기는 한 방이 크므로 각 쥐를 한 번씩만 친다.
    warp_to_shop(host, m)
    shield = buy_to(host, host, m, "CheeseShield", 3)

    # 클론 쥐는 치즈 지점 위에 서 있다 — 지점 콜라이더가 덮치기를 가리므로 먼저 옆으로 물린다.
    m2i.invoke("TestTeleportLocalPlayer", _x=sx + 7.0, _z=sz + 7.0)
    time.sleep(1.5)
    base_loss, base_tries = pounce_for_damage(cat, host, m2)

    time.sleep(3.0)   # 덮치기 쿨
    sh_hp0 = host.invoke("TestGetHpOf", _clientId=m)
    sh_ch0 = host.invoke("TestGetCheeseOf", _clientId=m)
    cat.invoke("TestTeleportToOtherPlayer", _clientId=m)
    time.sleep(1.5)
    cat.invoke("TestPounce")
    time.sleep(1.5)
    sh_loss = sh_hp0 - host.invoke("TestGetHpOf", _clientId=m)
    ch_loss = sh_ch0 - host.invoke("TestGetCheeseOf", _clientId=m)
    check("④ 치즈 방패 Lv3 — HP를 덜 잃고 대신 치즈가 빠진다",
          shield == 3 and base_loss > 0 and 0 < sh_loss < base_loss and ch_loss > 0,
          "lv=%s 방패없는 쥐 HP -%s(%s번째 덮치기) · 방패 쥐 HP -%s 치즈 -%s"
          % (shield, base_loss, base_tries, sh_loss, ch_loss))

    # ---------- ⑧ 끈끈이 — 장판 1장 · 고양이 감속 · 10초 뒤 사라짐 ----------
    warp_to_shop(host, m)
    sticky = buy_to(host, host, m, "Sticky", 3)
    patches_before = host.invoke("TestGetStickyPatchCount")
    # 자유 이속은 **장판을 깔기 전에** 잰다 — ④에서 고양이가 이미 쥐 옆에 서 있어서,
    # 깔고 나서 재면 이미 밟은 값(감속된 값)을 기준선으로 삼게 된다(2026-09-07 실측).
    cat_free = host.invoke("TestGetMoveSpeedOf", _clientId=c)
    host.invoke("TestUseTechLocal", _techId="Sticky")
    time.sleep(1.5)
    patches = host.invoke("TestGetStickyPatchCount")
    cat.invoke("TestTeleportToOtherPlayer", _clientId=m)
    time.sleep(2.0)
    cat_stuck = host.invoke("TestGetMoveSpeedOf", _clientId=c)
    check("⑧-a 끈끈이 Lv3 — 장판이 1장 깔리고 고양이가 −75% 느려진다",
          sticky == 3 and patches == patches_before + 1 and cat_stuck < cat_free * 0.5,
          "lv=%s 장판 %s→%s · 고양이 이속 %.2f→%.2f" % (sticky, patches_before, patches, cat_free, cat_stuck))

    time.sleep(11.0)   # BalanceInfo.StickySeconds = 10
    patches_gone = host.invoke("TestGetStickyPatchCount")
    cat_back = host.invoke("TestGetMoveSpeedOf", _clientId=c)
    check("⑧-b 10초 뒤 장판이 사라지고 이속이 돌아온다",
          patches_gone == patches_before and abs(cat_back - cat_free) < 0.05,
          "장판=%s 고양이 이속 %.2f (원래 %.2f)" % (patches_gone, cat_back, cat_free))

    # ---------- ③ 요새 Lv3 — HP 1200 · 가시 벽 반사 ----------
    # 마지막에 둔다 — 요새는 몸을 밀어내므로 다른 계측의 자리를 흐트러뜨린다.
    warp_to_shop(host, m)
    fort = buy_to(host, host, m, "Fortress", 3)
    fort_place = host.invoke("TestGetPlaceCost", _techId="Fortress")
    host.invoke("TestUseTechLocal", _techId="Fortress")
    time.sleep(2.0)
    fort_hp = host.invoke("TestGetNearestFortressMaxHp", _clientId=m)
    check("③-a 요새 Lv3 — 최대 HP 1200", fort == 3 and fort_hp == 1200,
          "lv=%s 요새 MaxHp=%s (설치비 %s)" % (fort, fort_hp, fort_place))

    cat.invoke("TestTeleportToOtherPlayer", _clientId=m)
    time.sleep(2.0)
    cat_hp0 = host.invoke("TestGetHpOf", _clientId=c)
    struct_hp0 = host.invoke("TestGetLowestStructureHp")
    cat.invoke("TestPounce")
    time.sleep(1.5)
    cat_hp1 = host.invoke("TestGetHpOf", _clientId=c)
    check("③-b 가시 벽 — 요새를 친 고양이가 25 되맞는다",
          cat_hp0 - cat_hp1 >= 25,
          "고양이 HP %s→%s (-%s) · 구조물 최저 HP %s" % (cat_hp0, cat_hp1, cat_hp0 - cat_hp1, struct_hp0))

    print("\n===== 요약 =====")
    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  | " + detail if detail else ""))
    print("PASS %d · FAIL %d · SKIP %d" % (passed, failed, skipped))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
