# -*- coding: utf-8 -*-
"""상점 부스 철문 — 고양이가 오면 닫히고, 닫힌 동안 못 사고, 떠나면 열린다 (2026-09-19). MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, **새 판**이 시작된 직후.
**호스트=쥐** — 세팅: `python setup_match.py 0 mouse 3` (클론 1 = 고양이, 클론 2 = 쥐).

## 왜 이 스위트가 있는가

철문 규칙(반경·히스테리시스·살금살금)은 EditMode가 덮는다(C-39). 여기서 재는 것은 **전파와 배선**이다 —
철문은 서버가 판정하는 복제값이라 **쥐 화면에서도** 내려와야 하고(연출), 닫힌 동안 **실제 구매 경로**(서버 BuyRpc ·
상점 팝업)가 막혀야 한다. 하나라도 빠지면 EditMode는 초록인 채로 쥐 화면만 열려 있거나, 닫혀도 사진다.

기획: docs/superpowers/specs/2026-09-19-shop-booth-design.md
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 7     # ⓪ 전제 + ①~⑥

REOPEN = 1.5             # ShopShutterReopenSeconds
BUY = "Warehouse"        # 설치권 — 치즈만 있으면 누구나 산다
CLOSED_LABEL = "철문 닫힘"   # ShopHotbar.SHOP_CLOSED_LABEL — 닫히면 모드 줄의 "E = 상점" 자리에 뜬다


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


def fields(line):
    out = {}
    for part in (line or "").split():
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def shutter(inst, client_id):
    return fields(inst.invoke("TestGetNearestShutter", _clientId=client_id))


def wait_for(fn, timeout, interval=0.2):
    t0 = time.time()
    while time.time() - t0 < timeout:
        value = fn()
        if value:
            return value
        time.sleep(interval)
    return fn()


def main():
    host = WesPoco(instance=0)     # 쥐 — 손님
    cat = WesPoco(instance=1)      # 고양이
    other = WesPoco(instance=2)    # 다른 쥐 — 멀리 치운다

    host_id = host.invoke("TestGetLocalClientId")
    ok_setup = check("⓪ 전제 — 호스트가 쥐, 고양이 클론이 있다, 상점이 섰다",
                     host.invoke("TestGetRole") == "Mouse" and cat.invoke("TestGetRole") == "Cat"
                     and host.invoke("TestGetShopSpotCount") > 0,
                     "상점 %s곳" % host.invoke("TestGetShopSpotCount"))
    if not ok_setup:
        print("!! `python setup_match.py 0 mouse 3`으로 세팅했는지 본다")
        return summary(aborted=True)

    other.invoke("TestTeleportLocalPlayer", _x=13.0, _z=13.0)
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=14.0)
    host.invoke("TestGrantCheeseAll")
    host.invoke("TestWarpToNearestShop", _clientId=host_id)
    in_shop = wait_for(lambda: host.invoke("TestIsInShop", _clientId=host_id), 10.0, 0.4)
    time.sleep(REOPEN + 1.0)   # 워프 도중 고양이가 스쳐 닫혔을 수도 있다 — 열릴 시간을 준다

    # ---------- ① 처음엔 열려 있다 ----------
    s1 = shutter(host, host_id)
    mode1 = host.invoke("TestGetHotbarModeText") or ""
    check("① 고양이가 멀면 창구 철문이 열려 있고 점원이 서 있다 · 핫바가 「E = 상점」",
          in_shop and s1.get("closed") == "False" and float(s1.get("lowered", "1")) < 0.05
          and s1.get("cowering") == "False" and "E = 상점" in mode1,
          "상점 안=%s · %s · 모드 줄=%r" % (in_shop, s1, mode1), case="C-40")

    # ---------- ② 걷는 고양이가 3.5m까지 오면 — 쥐 화면에서도 쾅 닫히고 점원이 움츠린다 ----------
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=3.5)
    time.sleep(0.9)
    s2 = shutter(host, host_id)
    s2c = shutter(cat, host_id)
    check("② 고양이가 3.5m까지 오면 **쥐 화면에서도** 철문이 내려오고 점원이 움츠린다",
          s2.get("closed") == "True" and float(s2.get("lowered", "0")) > 0.95 and s2.get("cowering") == "True"
          and s2c.get("closed") == "True",
          "쥐 화면 %s · 고양이 화면 closed=%s" % (s2, s2c.get("closed")), case="C-40")

    # ---------- ③ 닫힌 동안 못 산다 — 서버 구매 경로 · 팝업 둘 다 ----------
    lv0 = host.invoke("TestGetTechLevel", _clientId=host_id, _techId=BUY)
    verdict = host.invoke("TestBuyTech", _clientId=host_id, _techId=BUY)
    time.sleep(0.8)
    lv1 = host.invoke("TestGetTechLevel", _clientId=host_id, _techId=BUY)
    opened = host.invoke("TestToggleShopPopup")
    mode = host.invoke("TestGetHotbarModeText") or ""
    check("③ 철문이 닫힌 상점에서는 못 산다 — 거절 사유 ShopClosed · 레벨 그대로 · 팝업도 안 열린다 · 핫바가 「철문 닫힘」",
          "ShopClosed" in (verdict or "") and lv1 == lv0 and not opened
          and CLOSED_LABEL in mode and "E = 상점" not in mode,
          "판정=%s · 레벨 %s→%s · 팝업 열림=%s · 모드 줄=%r" % (verdict, lv0, lv1, opened, mode), case="C-40")

    # ---------- ④ 고양이가 떠나면 1.5초 뒤 열리고 다시 산다 ----------
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=14.0)
    time.sleep(0.7)
    still = shutter(host, host_id).get("closed")
    reopened = wait_for(lambda: shutter(host, host_id).get("closed") == "False", 4.0)
    time.sleep(1.0)
    s4 = shutter(host, host_id)
    lv2 = host.invoke("TestGetTechLevel", _clientId=host_id, _techId=BUY)
    host.invoke("TestBuyTech", _clientId=host_id, _techId=BUY)
    time.sleep(0.8)
    lv3 = host.invoke("TestGetTechLevel", _clientId=host_id, _techId=BUY)
    check("④ 고양이가 떠나도 잠깐은 닫혀 있다가(1.5초) 열리고, 다시 산다",
          still == "True" and reopened and float(s4.get("lowered", "1")) < 0.05 and s4.get("cowering") == "False"
          and lv3 == lv2 + 1,
          "떠난 직후 closed=%s · 열림=%s · %s · 레벨 %s→%s" % (still, reopened, s4, lv2, lv3), case="C-40")

    # ---------- ⑤ 살금살금 고양이는 더 가까이 와야 닫힌다 ----------
    cat.invoke("TestSetSneak", _on=True)
    time.sleep(0.3)
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=4.0)
    time.sleep(0.9)
    sneak_far = shutter(host, host_id).get("closed")
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=2.0)
    time.sleep(0.9)
    sneak_near = shutter(host, host_id).get("closed")
    cat.invoke("TestSetSneak", _on=False)
    check("⑤ 살금살금 고양이는 4m에선 안 들키고 2m에서야 철문이 닫힌다",
          sneak_far == "False" and sneak_near == "True",
          "4m closed=%s · 2m closed=%s" % (sneak_far, sneak_near), case="C-40")

    # ---------- ⑥ 팝업을 열어 둔 채 고양이가 오면 팝업이 닫힌다 ----------
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=14.0)
    wait_for(lambda: shutter(host, host_id).get("closed") == "False", 5.0)
    time.sleep(0.9)
    was_open = host.invoke("TestToggleShopPopup")
    cards_before = host.invoke("TestGetShopPopupCards") or ""
    cat.invoke("TestTeleportNearShopOf", _clientId=host_id, _meters=3.5)
    time.sleep(0.9)
    cards_after = host.invoke("TestGetShopPopupCards") or ""
    check("⑥ 상점 팝업을 연 채 고양이가 오면 철문이 닫히며 팝업도 닫힌다",
          was_open and len(cards_before) > 0 and (cards_after == "" or cards_after == "Missing"),
          "열림=%s · 전 %d자 · 후 %r" % (was_open, len(cards_before), cards_after[:30]), case="C-40")

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
