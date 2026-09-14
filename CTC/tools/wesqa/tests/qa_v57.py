# -*- coding: utf-8 -*-
"""기술 이펙트가 **남의 화면에서도** 실제로 뜨는가. MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이** — 세팅: `python setup_match.py 0 cat 3` (클론 둘 다 쥐).

## 왜 이 스위트가 있는가

2026-09-14 기술 이펙트 패스는 이미 전 클라이언트가 도는 `*FxRpc`에 `FxPlayer.PlayAt` 한 줄씩을 붙였다.
EditMode는 표(`FxCatalogLogic`)만 본다 — **줄을 빠뜨리거나 서버 전용 자리에 붙이면** 초록인 채로
행동한 사람 화면에서만 뜨거나 아무 데서도 안 뜬다. 그래서 **행동한 인스턴스가 아닌 다른 인스턴스에서**
`FxPlayer.Played`를 센다(`TestResetFxCounts` → 행동 → `TestGetFxCounts`).

**기대 이름은 파이썬에 그대로 적는다**(qa_v53과 같은 이유 — 훅에서 되읽으면 항진명제).
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 11     # ⓪ 전제 + ① 구매 + ② 광분 + ③ 전직 + ④ 전직 스킬 + ⑤ 덮치기 + ⑥ 대시 + ⑦ 요새 + ⑧ 끈끈이 + ⑨ 사격 + ⑩ 채굴

BUY, FRENZY, ADVANCE_FIRE, SKILL_FIRE, CLAW = "BuyCoins", "FrenzyShock", "AdvanceFire", "SkillFire", "ClawSlash"
DASH, PLACE, STICKY, MUZZLE, IMPACT, CRUMBS = "DashDust", "PlaceDust", "StickySplat", "MuzzleFlash", "BulletImpact", "EatCrumbs"


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def note(name, detail):
    EXTRA.append((name, detail))
    print("[NOTE] " + name + "  " + detail)
    sys.stdout.flush()


def counts(inst):
    raw = inst.invoke("TestGetFxCounts") or ""
    out = {}
    for part in raw.split(","):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = int(v)
    return out


def seen_on(observer, action, settle=1.2):
    """관측 창을 열고 → 행동 → 관측자 화면에서 뜬 이펙트 수."""
    observer.invoke("TestResetFxCounts")
    result = action()
    time.sleep(settle)
    return result, counts(observer)


def warp_to_shop(host, client_id):
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.2)
    return bool(host.invoke("TestIsInShop", _clientId=client_id))


def buy_slot(inst, host, client_id, tech):
    before = host.invoke("TestGetTechSlotCount", _clientId=client_id)
    inst.invoke("TestBuyTechLocal", _techId=tech)
    time.sleep(1.0)
    after = host.invoke("TestGetTechSlotCount", _clientId=client_id)
    return before if after == before + 1 else -1


def main():
    host = WesPoco(instance=0)
    m1i = WesPoco(instance=1)
    m2i = WesPoco(instance=2)

    roles = [host.invoke("TestGetRole"), m1i.invoke("TestGetRole"), m2i.invoke("TestGetRole")]
    m1 = m1i.invoke("TestGetLocalClientId")
    m2 = m2i.invoke("TestGetLocalClientId")
    if not check("⓪ 전제 — 호스트=고양이 · 클론 둘=쥐", roles == ["Cat", "Mouse", "Mouse"], "역할=%s" % roles):
        print("`python setup_match.py 0 cat 3`")
        return summary(aborted=True)

    host.invoke("TestClearSoldOut")
    for _ in range(5):
        host.invoke("TestGrantCheeseAll")
    time.sleep(1.0)

    # ===== 고양이(호스트)의 행동 → 쥐 m1 화면에서 센다 =====
    c = host.invoke("TestGetLocalClientId")
    _, got = seen_on(m1i, lambda: buy_slot(host, host, c, "CatFrenzy"), 0.8)
    check("① 구매 — 고양이가 사면 쥐 화면에 BuyCoins가 뜬다", got.get(BUY, 0) >= 1, "m1 관측=%s" % got)

    used, got = seen_on(m1i, lambda: bool(host.invoke("TestPressActiveKey")))
    check("② 광분 — Space로 켜면 쥐 화면에 FrenzyShock이 한 번 뜬다(오라 아님)",
          used and got.get(FRENZY, 0) == 1, "눌림=%s m1 관측=%s" % (used, got))

    host.invoke("TestGrantAdvanceMischief")
    time.sleep(1.0)
    _, got = seen_on(m1i, lambda: host.invoke("TestChooseCatClass", _id="Fire"))
    check("③ 전직 — 불로 전직하면 쥐 화면에 AdvanceFire가 뜬다", got.get(ADVANCE_FIRE, 0) >= 1, "m1 관측=%s" % got)

    pressed, got = seen_on(m1i, lambda: bool(host.invoke("TestPressSkillKey")))
    check("④ 전직 스킬 — Q를 쓰면 쥐 화면에 SkillFire가 한 번 뜬다", pressed and got.get(SKILL_FIRE, 0) == 1,
          "눌림=%s m1 관측=%s" % (pressed, got))

    time.sleep(1.5)   # 덮치기 쿨다운이 광분·스킬과 겹치지 않게
    _, got = seen_on(m1i, lambda: host.invoke("TestPounce"))
    check("⑤ 덮치기 — 쥐 화면에 ClawSlash가 뜬다", got.get(CLAW, 0) >= 1, "m1 관측=%s" % got)

    # ===== 쥐 m1의 행동 → 고양이(호스트) 화면에서 센다 =====
    warp_to_shop(host, m1)
    slots = {t: buy_slot(m1i, host, m1, t) for t in ("Dash", "Fortress", "Sticky")}
    note("m1 구매", "칸=%s" % slots)
    spot = host.invoke("TestGetBestCheeseSpotPos")
    sx, sz = [float(v) for v in spot.split(",")]
    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 9.0, _z=sz + 9.0)
    time.sleep(1.5)

    pressed, got = seen_on(host, lambda: bool(m1i.invoke("TestPressSlotKey", _slot=slots["Dash"])))
    check("⑥ 대시 — 고양이 화면에 DashDust가 뜬다", pressed and got.get(DASH, 0) >= 1, "눌림=%s 호스트 관측=%s" % (pressed, got))

    time.sleep(2.0)
    pressed, got = seen_on(host, lambda: bool(m1i.invoke("TestPressSlotKey", _slot=slots["Fortress"])), 1.5)
    check("⑦ 요새 — 서는 자리에 PlaceDust가 고양이 화면에 뜬다", pressed and got.get(PLACE, 0) >= 1,
          "눌림=%s 호스트 관측=%s" % (pressed, got))

    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 16.0, _z=sz + 16.0)
    time.sleep(1.5)
    pressed, got = seen_on(host, lambda: bool(m1i.invoke("TestPressSlotKey", _slot=slots["Sticky"])), 1.5)
    check("⑧ 끈끈이 — 장판 자리에 StickySplat이 고양이 화면에 뜬다", pressed and got.get(STICKY, 0) >= 1,
          "눌림=%s 호스트 관측=%s" % (pressed, got))

    time.sleep(1.0)
    shot, got = seen_on(host, lambda: bool(m1i.invoke("TestShootForward")), 1.0)
    check("⑨ 사격 — 고양이 화면에 총구 섬광과 탄착이 같은 수로 뜬다",
          shot and got.get(MUZZLE, 0) >= 1 and got.get(MUZZLE, 0) == got.get(IMPACT, 0),
          "발사=%s 호스트 관측=%s" % (shot, got))

    # ===== 쥐 m2 채굴 → 고양이 화면 =====
    m2i.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(1.5)
    _, got = seen_on(host, lambda: None, 2.5)
    check("⑩ 채굴 — 지점에서 갉는 동안 EatCrumbs가 고양이 화면에 뜬다", got.get(CRUMBS, 0) >= 1, "호스트 관측=%s" % got)

    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다. 안 돈 항목은 통과가 아니다."""
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  " + detail if detail else ""))
    for name, detail in EXTRA:
        print("  NOTE  " + name + "  " + detail)

    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    unrun = max(0, EXPECTED - len(RESULTS))
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d · NOTE %d"
          % (passed, failed, skipped, unrun, EXPECTED, len(EXTRA)))
    return 0 if (failed == 0 and skipped == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
