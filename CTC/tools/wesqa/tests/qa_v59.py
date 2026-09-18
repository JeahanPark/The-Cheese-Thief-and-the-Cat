# -*- coding: utf-8 -*-
"""상점 팝업 — 상점 원 안에서 E로 열고, 클릭으로 사고, 맞으면 닫힌다 (2026-09-19). MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=쥐** — 세팅: `python setup_match.py 0 mouse 3` (클론 하나가 고양이, 하나가 쥐).

## 왜 이 스위트가 있는가

구매는 2026-09-19에 핫바에서 **팝업**으로 옮겼다(기획 `2026-09-19-shop-popup-design.md`).
EditMode는 카드 상태(`ShopPopupLogic`)만 본다 — **커서를 풀고 클릭이 서버까지 닿는 배선**은
실플레이에서만 드러난다. 특히 「맞으면 닫힌다」가 빠지면 커서가 풀린 채 굳어 아무것도 못 하게 된다.

그래서 여기서는 사람이 밟는 길 그대로 잰다: 원 밖에서 E → 안 열림, 원 안에서 E → 열림,
카드 클릭 → 치즈가 줄고 레벨이 오름, 고양이가 때림 → 닫힘.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 7     # ⓪ 전제 + ①~⑥

CARD_COUNT = 10          # 쥐 카탈로그 10종 — 한 화면에 전부 보이는 것이 이 기획의 전부다
FIRST_BUY = "Warehouse"  # 설치권 50 — 시작 치즈 65로 살 수 있는 유일한 테크다
FIRST_BUY_COST = 50


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다(qa_v52와 같은 규약).
    if case:
        name = "[%s] %s" % (case, name)
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def cards_of(inst):
    """"테크:제목:살수있나" 목록 → [(테크, 제목, 살수있나)]. 안 떠 있으면 빈 목록."""
    raw = inst.invoke("TestGetShopPopupCards")
    if not raw or raw == "Missing":
        return []
    out = []
    for part in raw.split("|"):
        bits = part.split(":")
        if len(bits) < 3:
            continue
        out.append((bits[0], bits[1].strip(), bits[2].lower() == "true"))
    return out


def warp_into_shop(host, limit=12.0):
    host.invoke("TestWarpToNearestShop", _clientId=0)
    t0 = time.time()
    while time.time() - t0 < limit:
        if host.invoke("TestIsInShop", _clientId=0):
            return True
        time.sleep(0.5)
    return False


def main():
    host = WesPoco(instance=0)     # 쥐
    cat = WesPoco(instance=1)      # 고양이

    role = host.invoke("TestGetRole")
    ok_setup = check("⓪ 전제 — 호스트가 쥐이고 판이 돌고 있다",
                     role == "Mouse" and host.invoke("TestGetLocalHp") > 0,
                     "역할=%s · HP=%s" % (role, host.invoke("TestGetLocalHp")))
    if not ok_setup:
        print("!! `python setup_match.py 0 mouse 3`으로 세팅했는지 본다")
        return summary(aborted=True)

    # ---------- ① 상점 밖에서는 안 열린다 ----------
    host.invoke("TestTeleportLocalPlayer", _x=0.0, _z=0.0)
    time.sleep(1.2)
    outside_open = host.invoke("TestToggleShopPopup")
    check("① 상점 원 밖에서 E를 눌러도 안 열린다",
          not outside_open and not host.invoke("TestIsInShop", _clientId=0),
          "열림=%s" % outside_open, case="C-36")

    # ---------- ② 상점 안에서 열리고 열 장이 보인다 ----------
    in_shop = warp_into_shop(host)
    opened = host.invoke("TestToggleShopPopup") if in_shop else False
    cards = cards_of(host)
    check("② 상점 원 안에서 E를 누르면 파는 물건 %d종이 한 화면에 뜬다" % CARD_COUNT,
          opened and len(cards) == CARD_COUNT,
          "상점 안=%s · 열림=%s · 카드 %d장" % (in_shop, opened, len(cards)), case="C-36")
    if not opened:
        skip("③ 살 수 있는 카드와 없는 카드가 갈린다", "②가 안 열려 못 잰다")
        skip("④ 카드를 클릭하면 실제로 사진다", "②가 안 열려 못 잰다")
        skip("⑤ 산 기술이 핫바에 「쓰기」로 들어간다", "②가 안 열려 못 잰다")
        skip("⑥ 맞으면 창이 닫힌다", "②가 안 열려 못 잰다")
        return summary(aborted=True)

    # ---------- ③ 살 수 있는 것과 없는 것이 갈린다 ----------
    cheese = host.invoke("TestGetCheese")
    buyable = [c for c in cards if c[2]]
    too_poor = [c for c in cards if not c[2]]
    check("③ 잔액으로 살 수 있는 카드만 살 수 있다고 뜬다",
          len(buyable) >= 1 and len(too_poor) >= 1,
          "치즈 %s · 살 수 있음 %d / 못 삼 %d" % (cheese, len(buyable), len(too_poor)), case="C-33")

    # ---------- ④ 클릭하면 사진다 ----------
    before_level = host.invoke("TestGetTechLevel", _clientId=0, _techId=FIRST_BUY)
    clicked = host.invoke("TestClickShopCard", _tech=FIRST_BUY)
    time.sleep(1.5)
    after_level = host.invoke("TestGetTechLevel", _clientId=0, _techId=FIRST_BUY)
    after_cheese = host.invoke("TestGetCheese")
    check("④ 카드를 클릭하면 실제로 사진다 — 레벨이 오르고 치즈가 준다",
          clicked and after_level == before_level + 1 and after_cheese == cheese - FIRST_BUY_COST,
          "클릭=%s · 레벨 %s→%s · 치즈 %s→%s" % (clicked, before_level, after_level, cheese, after_cheese),
          case="C-36")

    # ---------- ⑤ 핫바는 「쓴다」 전용이다 ----------
    slots = host.invoke("TestGetHotbarSlots")
    first_slot = slots.split("|")[0] if slots else ""
    check("⑤ 산 기술이 핫바에 들어가고 칸이 「쓰기」로 보인다",
          "쓰기" in first_slot and "E로 산다" in slots,
          "1번 칸=%r" % first_slot[:60], case="C-35")

    # ---------- ⑥ 맞으면 닫힌다 ----------
    # 고양이를 덮치기 사거리 안으로 데려와 실제로 때린다 — 상점은 안전지대가 아니다.
    # **다른 쥐를 먼저 치운다** — `TestTeleportAtRangeFrom`은 가장 가까운 쥐를 고르므로,
    # 클론 쥐가 더 가까우면 고양이가 그쪽을 때려 호스트 HP가 안 준다(2026-09-19 실측).
    other = WesPoco(instance=2)
    other.invoke("TestTeleportLocalPlayer", _x=13.0, _z=13.0)
    time.sleep(1.5)
    cat.invoke("TestTeleportAtRangeFrom", _role="Mouse", _meters=1.5, _yawOffset=0.0, _botOnly=False)
    time.sleep(1.5)
    hp0 = host.invoke("TestGetLocalHp")
    for _ in range(8):
        cat.invoke("TestPounce")
        time.sleep(0.6)
        if host.invoke("TestGetLocalHp") < hp0:
            break
    hp1 = host.invoke("TestGetLocalHp")
    time.sleep(0.5)
    still_open = len(cards_of(host)) > 0
    check("⑥ 맞으면 창이 닫힌다 — 상점은 안전지대가 아니다",
          hp1 < hp0 and not still_open,
          "HP %s→%s · 닫힘=%s" % (hp0, hp1, not still_open), case="C-34")

    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다. 안 돈 항목은 통과가 아니다."""
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  | " + detail if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    unrun = EXPECTED - len(RESULTS)
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d"
          % (passed, failed, skipped, unrun, EXPECTED))
    if unrun > 0:
        print("!! 선언 %d · 실행 %d — 안 돈 항목은 통과가 아니다" % (EXPECTED, len(RESULTS)))
    if unrun < 0:
        print("!! 실행 %d > 선언 %d — EXPECTED를 고쳐라" % (len(RESULTS), EXPECTED))
    if aborted:
        print("!! 중단·열화된 실행이다 — 통과로 읽지 마라")
    return 0 if (failed == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
