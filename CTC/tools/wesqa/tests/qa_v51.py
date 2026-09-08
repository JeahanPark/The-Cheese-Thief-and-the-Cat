# -*- coding: utf-8 -*-
"""사람 손 입력 경로 — 고양이 광분(P0-1)과 죽은 고양이 덮치기(P0-7). MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이** — 세팅: `python setup_match.py 0 cat 3` (클론 둘 다 쥐).

## 왜 이 스위트가 따로 있는가

qa_v46~v48은 액티브를 전부 `TestUseTech`(= `ShopController.Use` 직접 호출)로 켰다.
그 훅은 **핫바의 상점 모드 분기를 통째로 건너뛴다.** 그래서 「고양이는 산 광분을
숫자키로 켤 수 없다」가 qa_v48 ③에서 초록으로 통과했다(2026-09-09 감사 G-1·G-2).

여기서는 `TestPressSlotKey` / `TestPressActiveKey`만 쓴다 — 둘 다 `ShopHotbar`가
사람의 키 입력에 대해 실제로 타는 분기(`PressSlot`·`PressActiveKey`)로 들어간다.
**`TestUseTech`를 부르지 않는다.** 그게 이 스위트의 존재 이유다.

## 무엇을 재는가

  ② 고양이 핫바는 늘 구매 모드다(상점 면제 역할이라 「밖」이 없다)
  ③ 광분을 사면 핫바·능력 바에 전용 키 안내가 뜬다
  ④ **숫자키**는 여전히 「키운다」다 — 광분이 켜지지 않는다(모드가 안 뒤집혔다는 증거)
  ⑤ **Space**로 광분이 켜진다 ← P0-1. 고치기 전에는 어떤 키로도 못 켰다
  ⑥ 쿨다운이 화면에 보인다(핫바 힌트 · 능력 바 상태)
  ⑦ 쿨 중에는 `CanUse`가 Cooldown이라 말한다
  ⑧ 죽은 고양이의 덮치기는 피해 0 · 장난기 0 ← P0-7

한 판에 한 번만 돌린다(⑧이 고양이를 죽여 판을 끝낸다). 다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 9      # ⓪ 전제 + ②~⑧(⑥은 a·b 둘)


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def note(name, detail):
    EXTRA.append((name, detail))
    print("[NOTE] " + name + "  " + detail)
    sys.stdout.flush()


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def frenzy_slot(host, client_id):
    """광분이 몇 번째 칸인가(0-based, 없으면 -1) — 숫자키를 손으로 세지 않는다.

    칸 순서는 `TechSlots.Owned`의 산 순서다. 광분 하나만 사면 0번이지만,
    스위트가 순서를 가정하면 앞에 뭘 하나 더 사는 순간 조용히 엉뚱한 칸을 누른다.
    """
    count = host.invoke("TestGetTechSlotCount", _clientId=client_id)
    if count is None or count < 0:
        return -1
    return 0 if host.invoke("TestGetTechLevel", _clientId=client_id, _techId="CatFrenzy") > 0 else -1


def pounce_mouse(cat, host, cat_id, target_id, attempts=3):
    """고양이를 쥐 앞에 세우고 실제로 깎일 때까지 덮친다 → (쥐 HP 손실, 장난기 적립, 시도 수).

    qa_v48과 같은 규약 — 덮치기는 차폐(치즈 지점 콜라이더 등)에 막히면 그 대상만 건너뛴다.
    """
    for i in range(1, attempts + 1):
        cat.invoke("TestTeleportToOtherPlayer", _clientId=target_id)
        time.sleep(1.5)
        hp0 = host.invoke("TestGetHpOf", _clientId=target_id)
        fun0 = host.invoke("TestGetCheeseOf", _clientId=cat_id)
        cat.invoke("TestPounce")
        time.sleep(1.5)
        loss = hp0 - host.invoke("TestGetHpOf", _clientId=target_id)
        fun = host.invoke("TestGetCheeseOf", _clientId=cat_id) - fun0
        if loss > 0:
            return loss, fun, i
        time.sleep(2.5)   # 덮치기 쿨(PlayerInfo Cat.PounceCooldown)
    return 0, 0, attempts


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
    ok_setup = check("⓪ 3인 접속 · 호스트=고양이 · 쥐 2",
                     all(i >= 0 for i in ids) and roles[0] == "Cat"
                     and roles.count("Cat") == 1 and roles.count("Mouse") == 2,
                     "roles=%s ids=%s" % (roles, ids))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 0 cat 3`으로 판을 다시 연다")
        return summary(aborted=True)

    host = p[0]
    c = ids[0]
    m1_idx, m2_idx = [i for i in range(3) if i != 0]
    m1i, m1 = p[m1_idx], ids[m1_idx]
    m2 = ids[m2_idx]
    print("고양이=%s(호스트) · 쥐A=%s · 쥐B=%s" % (c, m1, m2))

    # 장난기를 미리 채운다 — 광분 Lv1~2를 사고도 남아야 ④의 「키운다」가 돈을 이유로 막히지 않는다.
    for _ in range(6):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)
    time.sleep(0.6)

    # 쥐를 치즈 지점에서 떼어 놓는다 — 지점 콜라이더가 덮치기를 가린다(qa_v47 ④와 같은 이유).
    spot = host.invoke("TestGetBestCheeseSpotPos")
    sx, sz = [float(v) for v in spot.split(",")]
    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 9.0, _z=sz + 9.0)
    time.sleep(1.5)

    # ---------- ② 고양이 핫바는 늘 구매 모드다 ----------
    # 상점 면제 역할이라 위치와 무관하게 「산다/키운다」다. 이게 P0-1의 원인이자 전제다.
    # 「상점 밖」은 **세워야 하는 전제**다 — 상점 자리가 판마다 바뀌어(계획 3) 고양이 스폰이
    # 원반 안에 들어앉는 판이 있다(실측: 상점 (0.0, 1.5) 반경 3 m · 고양이 스폰 (0.0, -0.25)).
    in_shop = not move_out_of_shops(host, host, c)
    mode_before = host.invoke("TestGetHotbarModeText")
    check("② 고양이 핫바는 상점 자리 밖에서도 구매 모드다",
          "상점" in mode_before and "숫자키 = 산다" in mode_before and not in_shop,
          "상점 자리 안=%s · 모드 줄=%r" % (in_shop, mode_before))

    # ---------- ③ 광분을 사면 전용 키 안내가 뜬다 ----------
    host.invoke("TestBuyTechLocal", _techId="CatFrenzy")
    time.sleep(1.2)
    lv1 = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatFrenzy")
    mode_owned = host.invoke("TestGetHotbarModeText")
    bar = host.invoke("TestGetAbilityBar")
    check("③ 광분 Lv1 구매 — 핫바 모드 줄과 능력 바에 Space 안내가 생긴다",
          lv1 == 1 and "Space = 쓴다" in mode_owned and "Space:광분" in bar,
          "Lv=%s · 모드 줄=%r · 능력 바=%r" % (lv1, mode_owned, bar))

    slot = frenzy_slot(host, c)

    # ---------- ④ 숫자키는 「키운다」다 — 광분이 켜지지 않는다 ----------
    # P0-1을 고쳤다고 숫자키의 뜻을 바꾸면 고양이가 광분을 **키울** 길이 사라진다.
    # 여기서 그 경계를 못 박는다: 숫자키 = 레벨이 오른다 · 켜지지 않는다.
    pressed_num = bool(host.invoke("TestPressSlotKey", _slot=slot)) if slot >= 0 else False
    time.sleep(1.4)
    lv_after_num = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatFrenzy")
    state_after_num = host.invoke("TestGetCatFrenzyState", _clientId=c)
    check("④ 숫자키(사람 손) = 키운다 — Lv1→Lv2 · 광분은 안 켜진다",
          slot >= 0 and pressed_num and lv_after_num == 2 and state_after_num == "Off",
          "칸=%s 눌림=%s Lv %s→%s · 광분=%s"
          % (slot, pressed_num, lv1, lv_after_num, state_after_num))

    # ---------- ⑤ Space(사람 손) → 광분이 켜진다  ← P0-1 ----------
    # 고치기 전에는 이 줄이 존재할 수 없었다: `ShopHotbar`에 Space 분기가 없었고
    # `CatFrenzy`에도 입력이 없어, **사람이 켤 수 있는 키가 하나도 없었다.**
    used = bool(host.invoke("TestPressActiveKey"))
    time.sleep(0.8)
    state_on = host.invoke("TestGetCatFrenzyState", _clientId=c)
    cd_now = host.invoke("TestGetCatFrenzyCooldown", _clientId=c)
    check("⑤ Space(사람 손) → 광분이 켜지고 쿨이 돈다  ← P0-1",
          used and state_on == "On" and cd_now > 0.5,
          "PressActiveKey=%s · 광분=%s · 남은 쿨 %.1f초" % (used, state_on, cd_now))

    # ---------- ⑥ 쿨다운이 화면에 보인다 ----------
    slots_text = host.invoke("TestGetHotbarSlots")
    bar_on = host.invoke("TestGetAbilityBar")
    check("⑥-a 핫바 칸 힌트에 Space 쿨이 초로 보인다",
          "Space 쿨" in slots_text,
          "핫바 칸=%r" % slots_text)
    check("⑥-b 능력 바의 Space 칸이 Ready가 아니다(지속 중이거나 쿨)",
          "Space:광분:Ready" not in bar_on and "Space:광분" in bar_on,
          "능력 바=%r" % bar_on)

    # ---------- ⑦ 쿨 중 다시 Space → CanUse가 Cooldown이라 말한다 ----------
    deny = host.invoke("TestGetUseDeny", _clientId=c, _techId="CatFrenzy")
    check("⑦ 쿨 중에는 거절 사유가 Cooldown이다(뭉뚱그리지 않는다)",
          deny == "Cooldown",
          "CanUse=%r (기대 Cooldown)" % deny)

    # ---------- ⑧ 죽은 고양이는 못 덮치고 못 번다  ← P0-7 ----------
    # 먼저 **살아 있을 때** 실제로 깎이는지 잰다 — 기준선이 없으면 "0"이 아무것도 증명하지 못한다.
    alive_loss, alive_fun, tries = pounce_mouse(host, host, c, m1)
    time.sleep(2.6)   # 덮치기 쿨 — 죽은 뒤 시도가 쿨에 막혀 0이 나오면 안 된다

    killed = host.invoke("TestKillCat")
    time.sleep(1.0)
    hp_before = host.invoke("TestGetHpOf", _clientId=m1)
    fun_before = host.invoke("TestGetCheeseOf", _clientId=c)
    host.invoke("TestTeleportToOtherPlayer", _clientId=m1)
    time.sleep(1.2)
    host.invoke("TestPounce")
    time.sleep(1.5)
    dead_loss = hp_before - host.invoke("TestGetHpOf", _clientId=m1)
    dead_fun = host.invoke("TestGetCheeseOf", _clientId=c) - fun_before
    check("⑧ 죽은 고양이의 덮치기 — 피해 0 · 장난기 0  ← P0-7",
          killed >= 1 and alive_loss > 0 and dead_loss == 0 and dead_fun == 0,
          "산 고양이 피해 %s(장난기 +%s, 시도 %s) → 죽은 뒤 피해 %s(장난기 +%s) · 처치 %s"
          % (alive_loss, alive_fun, tries, dead_loss, dead_fun, killed))

    note("판 상태", "결과=%s (⑧이 고양이를 죽여 판이 끝난다 — 다시 돌리려면 플레이를 껐다 켠다)"
         % host.invoke("TestGetMatchResult"))
    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다. 안 돈 항목은 통과가 아니다."""
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
