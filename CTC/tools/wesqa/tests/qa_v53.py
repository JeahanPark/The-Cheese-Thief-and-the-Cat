# -*- coding: utf-8 -*-
"""원샷 애니메이션 6종이 **실제로 재생되는가** + 숨은 창고 Lv3이 **정말 무음인가**. MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이** — 세팅: `python setup_match.py 0 cat 3` (클론 둘 다 쥐).

## 왜 이 스위트가 따로 있는가

테크 v3 계획 2b가 원샷 여섯(대시 · 끈끈이 · 요새 · 광분 Spin · 구매 Clicked · 채굴 Eat)을
붙였는데(대시·끈끈이·요새는 2026-09-13부터 자작 클립 Dash·Sticky·Fortress), **자동 검사는 「컨트롤러에 그 이름이 있다」까지만**
봤다(EditMode `AnimationStateLogicTests`). `CrossFadeInFixedTime`은 없는 상태 이름을 받아도
에러도 경고도 안 낸다 — 배선이 끊겨도 화면에서 아무 일이 안 일어날 뿐이다.
2026-09-09 감사 B-04·C-23이 이걸 P0로 올렸고, `qa_v48` ⑦이 SKIP으로 남아 있던 이유다.

여기서는 `TestResetAnimObserved` → 동작 → `TestGetAnimObserved`로 **애니메이터가 실제로
들어갔던 상태 목록**을 읽는다. 한 번 물어보는 방식이 아닌 이유는 원샷이 0.35~0.6초만
살기 때문이다 — 왕복 지연에 놓치면 「안 걸렸다」와 구별이 안 된다.

**기대 이름은 파이썬에 그대로 적는다.** `AnimationStateLogic`의 상수를 훅으로 되읽으면
철자를 바꿔도 양쪽이 같이 움직여 항진명제가 된다(감사 T3 부류).

## 사람 손 경로

**액티브 사용은 전부 `TestPressSlotKey` / `TestPressActiveKey`다.** `TestUseTech`는
`ShopController.Use`를 직접 불러 핫바의 상점 모드 분기를 건너뛴다 — 그게 「고양이는 산
광분을 켤 수 없다」(P0-1)를 qa_v48에서 초록으로 통과시킨 자리다.
구매는 `TestBuyTechLocal`을 쓴다: 핫바의 `TryBuy`가 그대로 `ShopController.Buy`를
부르므로 같은 길이고, 숫자키로는 후보를 넘길 뿐 확정 구매는 Enter(EscapeRouter)라 못 탄다.

## 무음 검사

「0이어야 한다」는 계측기가 늘 0만 줘도 초록이 된다. 그래서 **대조군을 같이 잰다** —
숨은 창고 Lv3(무음)과 평범한 창고(소리 남)를 같은 훅·같은 창으로 세고, 틱 수도 함께 본다.
셋 중 하나라도 어긋나면 검사가 아무것도 안 본 것이다.

한 판에 한 번만 돌린다(칸을 채우고 구조물을 세운다). 다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 12      # ⓪ 전제 + ①~⑥ 원샷 6종 + ⑦ 무음 + ⑧ 대조 + ⑨ 틱 + ⑩ 창고 설치 + ⑪ 전직 스킬

# 기대하는 애니메이터 상태 이름 — **여기 글자가 곧 계약이다**(훅에서 되읽지 않는다).
# 대시·요새·끈끈이는 2026-09-13에 팩 클립(Roll·Jump·Bounce)을 걷고 자작 클립으로 바꿨다.
# **옛 이름이 그대로 남아 있으면 이 스위트가 빨개진다** — 그게 이 글자들이 지키는 것이다.
# 광분은 2026-09-13에 팩 Spin(발레 회전)을 걷고 자작 Frenzy로, 전직 스킬은 동작이 없다가 CatSkill이 생겼다.
DASH, FORTRESS, STICKY, FRENZY, CLICKED, EAT = "Dash", "Fortress", "Sticky", "Frenzy", "Clicked", "Eat"
CAT_SKILL_FIRE = "CatSkillFire"   # 전직마다 다른 동작(2026-09-13) — 여기서는 불로 전직한다
WAREHOUSE = "Warehouse"   # 창고 설치(2026-09-13) — 숨은 창고도 같은 클립이어야 한다
# 원샷에 딸린 표정(2026-09-13, FaceExpressionLogic) — 같은 이유로 글자를 여기 적는다.
FACE_EXCITED, FACE_RABID, FACE_HAPPY = "Eyes_Excited", "Eyes_Rabid", "Eyes_Happy"


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다. 붙이면 출력이 곧
    # 「무엇이 언제 돌았나」가 되고, 선언과 어긋나면 대장 드리프트 테스트가 깨진다.
    if case:
        name = "[%s] %s" % (case, name)
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


def warp_to_shop(host, client_id):
    """가장 가까운 상점 자리로 — qa_v36·v46·v47과 같은 규약(호스트에서 부른다)."""
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.2)
    return bool(host.invoke("TestIsInShop", _clientId=client_id))


def buy_slot(inst, host, client_id, tech):
    """한 레벨 사고 **그 테크가 몇 번째 칸에 앉았는지**를 돌려준다(못 샀으면 -1).

    칸 순서는 산 순서라, 파이썬이 순서를 가정하면 앞의 구매 하나가 실패하는 순간
    조용히 엉뚱한 칸을 누르게 된다 — 그러면 「원샷이 안 걸렸다」로 오독한다.
    """
    before = host.invoke("TestGetTechSlotCount", _clientId=client_id)
    inst.invoke("TestBuyTechLocal", _techId=tech)
    time.sleep(1.0)
    lv = host.invoke("TestGetTechLevel", _clientId=client_id, _techId=tech)
    after = host.invoke("TestGetTechSlotCount", _clientId=client_id)
    slot = before if (after == before + 1 and lv > 0) else -1
    print("    %s 구매 -> Lv%s · 칸 %s (보유 %s->%s)" % (tech, lv, slot, before, after))
    sys.stdout.flush()
    return slot


def press_and_watch(host, inst, client_id, slot, settle=1.2):
    """관측 창을 열고 → 숫자키를 누르고 → 애니메이터가 실제로 지나온 상태를 읽는다."""
    host.invoke("TestResetAnimObserved", _clientId=client_id)
    pressed = bool(inst.invoke("TestPressSlotKey", _slot=slot)) if slot >= 0 else False
    time.sleep(settle)
    seen = host.invoke("TestGetAnimObserved", _clientId=client_id)
    return pressed, (seen or "")


def sfx_window(host, seconds):
    """창을 열고 기다린 뒤 (WarehouseTick 재생 횟수, 그 창에서 난 소리 전부)."""
    host.invoke("TestResetSfxPlayCounts")
    time.sleep(seconds)
    return (host.invoke("TestGetSfxPlayCount", _id="WarehouseTick"),
            host.invoke("TestGetSfxPlayCounts"))


def try_place(inst, host, client_id, cx, cz):
    """창고를 한 채 세울 때까지 자리를 바꿔 시도한다 — qa_v47의 규약과 같다."""
    offsets = [(0, 0), (0, 3), (3, 0), (0, -3), (-3, 0), (2, 2), (-2, 2), (2, -2), (-2, -2)]
    for ox, oz in offsets:
        if host.invoke("TestGetWarehouseCountOf", _clientId=client_id) >= 1:
            break
        inst.invoke("TestPlaceWarehouseAt", _x=cx + ox, _z=cz + oz)
        time.sleep(1.4)
    got = host.invoke("TestGetWarehouseCountOf", _clientId=client_id)
    print("    창고 -> %s개" % got)
    sys.stdout.flush()
    return got


def main():
    host = WesPoco(instance=0)
    m1i = WesPoco(instance=1)
    m2i = WesPoco(instance=2)

    roles = [host.invoke("TestGetRole"), m1i.invoke("TestGetRole"), m2i.invoke("TestGetRole")]
    c = host.invoke("TestGetLocalClientId")
    m1 = m1i.invoke("TestGetLocalClientId")
    m2 = m2i.invoke("TestGetLocalClientId")
    ok = check("⓪ 전제 — 호스트=고양이 · 클론 둘=쥐",
               roles == ["Cat", "Mouse", "Mouse"],
               "역할=%s id=(고양이 %s · 쥐 %s · 쥐 %s)" % (roles, c, m1, m2))
    if not ok:
        print("전제가 안 서서 중단한다 — `python setup_match.py 0 cat 3`")
        return summary(aborted=True)

    host.invoke("TestClearSoldOut")      # 품절이 구매를 막으면 원샷을 아예 못 일으킨다
    for _ in range(5):                   # 한 번에 +1000 — 인자를 안 받는다
        host.invoke("TestGrantCheeseAll")
    time.sleep(1.0)

    # ================= 고양이(호스트) — 구매 Clicked · 광분 Spin =================

    host.invoke("TestResetAnimObserved", _clientId=c)
    frenzy_slot = buy_slot(host, host, c, "CatFrenzy")
    time.sleep(0.6)
    seen_buy = host.invoke("TestGetAnimObserved", _clientId=c) or ""
    face_buy = host.invoke("TestGetFaceObserved", _clientId=c) or ""
    check("① 구매 — 산 순간 Clicked가 실제로 돌고 얼굴이 Excited가 된다",
          frenzy_slot >= 0 and CLICKED in seen_buy.split(",") and FACE_EXCITED in face_buy.split(","),
          "관측=[%s] 표정=[%s] (기대 %s · %s)" % (seen_buy, face_buy, CLICKED, FACE_EXCITED), case="B-04 C-23")

    # 광분은 **Space(액티브 전용 키)**로 켠다 — 숫자키는 고양이에게 늘 「키운다」다(P0-1).
    host.invoke("TestResetAnimObserved", _clientId=c)
    used = bool(host.invoke("TestPressActiveKey"))
    time.sleep(1.2)
    seen_spin = host.invoke("TestGetAnimObserved", _clientId=c) or ""
    face_spin = host.invoke("TestGetFaceObserved", _clientId=c) or ""
    active = bool(host.invoke("TestGetCatFrenzyActive", _clientId=c))
    check("② 광분 — Space로 켜지고 Frenzy가 실제로 돌고 얼굴이 Rabid가 된다",
          used and active and FRENZY in seen_spin.split(",") and FACE_RABID in face_spin.split(","),
          "눌림=%s 켜짐=%s 관측=[%s] 표정=[%s] (기대 %s · %s)"
          % (used, active, seen_spin, face_spin, FRENZY, FACE_RABID), case="B-04 C-23")

    # 전직 스킬(Q) — 문턱만큼 벌고 불로 전직한 뒤 **사람 키 경로**로 쓴다(qa_v42와 같은 훅).
    host.invoke("TestGrantAdvanceMischief")
    time.sleep(1.0)
    host.invoke("TestChooseCatClass", _id="Fire")
    time.sleep(1.2)
    host.invoke("TestResetAnimObserved", _clientId=c)
    skill_pressed = bool(host.invoke("TestPressSkillKey"))
    time.sleep(1.2)
    seen_skill = host.invoke("TestGetAnimObserved", _clientId=c) or ""
    cls = host.invoke("TestGetCatClass")
    check("⑪ 전직 스킬 — 불로 전직해 Q를 쓰면 CatSkillFire가 실제로 돈다",
          skill_pressed and CAT_SKILL_FIRE in seen_skill.split(","),
          "전직=%s 눌림=%s 관측=[%s] (기대 %s 포함)" % (cls, skill_pressed, seen_skill, CAT_SKILL_FIRE),
          case="B-04 C-23")

    # ================= 클론 쥐 m1 — 대시 Dash · 요새 Fortress · 끈끈이 Sticky =================

    in_shop = warp_to_shop(host, m1)
    slots = {}
    for tech in ("Dash", "Fortress", "Sticky"):
        slots[tech] = buy_slot(m1i, host, m1, tech)
    note("m1 구매", "상점안=%s 칸=%s" % (in_shop, slots))

    # 상점 **밖**으로 나와야 숫자키가 「쓴다」로 간다(안에서는 「키운다」다 — ShopHotbar.PressSlot).
    spot = host.invoke("TestGetBestCheeseSpotPos")
    sx, sz = [float(v) for v in spot.split(",")]
    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 9.0, _z=sz + 9.0)
    time.sleep(1.5)
    out_of_shop = not bool(host.invoke("TestIsInShop", _clientId=m1))

    pressed, seen = press_and_watch(host, m1i, m1, slots["Dash"])
    check("③ 대시 — 숫자키로 쓰면 Dash가 실제로 돈다",
          out_of_shop and pressed and DASH in seen.split(","),
          "상점밖=%s 눌림=%s 관측=[%s] (기대 %s 포함)" % (out_of_shop, pressed, seen, DASH), case="B-04 C-23")

    time.sleep(2.0)
    pressed, seen = press_and_watch(host, m1i, m1, slots["Fortress"], settle=1.5)
    forts = host.invoke("TestGetFortressPos")
    check("④ 요새 — 숫자키로 세우면 Fortress가 실제로 돈다",
          pressed and FORTRESS in seen.split(","),
          "눌림=%s 관측=[%s] 요새=%s (기대 %s 포함)" % (pressed, seen, forts, FORTRESS), case="B-04 C-23")

    # 요새 안에 서 있으면 끈끈이가 InsideFortress로 거절된다 — 먼저 물러난다.
    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 16.0, _z=sz + 16.0)
    time.sleep(1.5)
    patches_before = host.invoke("TestGetStickyPatchCount")
    pressed, seen = press_and_watch(host, m1i, m1, slots["Sticky"], settle=1.5)
    patches_after = host.invoke("TestGetStickyPatchCount")
    deny = host.invoke("TestGetUseDeny", _clientId=m1, _techId="Sticky")
    check("⑤ 끈끈이 — 숫자키로 깔면 Sticky가 실제로 돈다",
          pressed and STICKY in seen.split(","),
          "눌림=%s 관측=[%s] 장판 %s->%s 사유=%s (기대 %s 포함)"
          % (pressed, seen, patches_before, patches_after, deny, STICKY), case="B-04 C-23")

    # ================= 클론 쥐 m2 — 채굴 Eat =================

    m2i.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(1.5)
    host.invoke("TestResetAnimObserved", _clientId=m2)
    time.sleep(2.5)                                   # 채굴 원샷은 0.8초마다 다시 걸린다
    seen_eat = host.invoke("TestGetAnimObserved", _clientId=m2) or ""
    face_eat = host.invoke("TestGetFaceObserved", _clientId=m2) or ""
    earner = host.invoke("TestGetCheeseEarnerNear", _x=sx, _z=sz)
    check("⑥ 채굴 — 지점에 서 있으면 Eat이 실제로 돌고 얼굴이 Happy가 된다",
          EAT in seen_eat.split(",") and FACE_HAPPY in face_eat.split(","),
          "선점자=%s 관측=[%s] 표정=[%s] (기대 %s · %s)" % (earner, seen_eat, face_eat, EAT, FACE_HAPPY),
          case="B-04 C-23")

    # ================= 숨은 창고 Lv3 무음 (qa_v47 ⑦-b SKIP 해소) =================
    #
    # 숨은 창고 레벨은 **세우는 순간 창고 몸에 박힌다**(ServerSetTech). 그래서 산 뒤에 세운다.

    warp_to_shop(host, m2)
    for _ in range(3):
        m2i.invoke("TestBuyTechLocal", _techId="HiddenWarehouse")
        time.sleep(0.9)
    hidden = host.invoke("TestGetTechLevel", _clientId=m2, _techId="HiddenWarehouse")
    buy_slot(m2i, host, m2, "Warehouse")

    m2i.invoke("TestTeleportLocalPlayer", _x=sx - 6.0, _z=sz - 6.0)
    time.sleep(1.5)
    host.invoke("TestResetAnimObserved", _clientId=m2)
    silent_count = try_place(m2i, host, m2, sx - 6.0, sz - 6.0)
    seen_place = host.invoke("TestGetAnimObserved", _clientId=m2) or ""
    check("⑩ 창고 설치 — 세우면 Warehouse가 실제로 돈다(숨은 창고도 같은 동작)",
          silent_count >= 1 and WAREHOUSE in seen_place.split(","),
          "창고=%s개 관측=[%s] (기대 %s 포함)" % (silent_count, seen_place, WAREHOUSE), case="B-04 C-23")

    ticks_before = host.invoke("TestGetWarehouseTicks")
    quiet_sfx, quiet_dump = sfx_window(host, 6.0)
    ticks_after = host.invoke("TestGetWarehouseTicks")
    ticked = ticks_after - ticks_before

    check("⑦ 숨은 창고 Lv3 — 생산음이 실제로 0회  <- qa_v47 ⑦-b SKIP 해소",
          hidden == 3 and silent_count >= 1 and quiet_sfx == 0,
          "숨은창고=Lv%s 창고=%s개 WarehouseTick 재생=%s회 (기대 0) · 그 창의 소리=[%s]"
          % (hidden, silent_count, quiet_sfx, quiet_dump), case="C-19 E-21")
    check("⑨ 그 창고는 **돌고 있었다** — 무음과 정지를 구별한다",
          ticked > 0, "그 6초에 생산 틱 %s회 (0이면 검사가 아무것도 안 봤다)" % ticked, case="C-19")

    # ---------- 대조군: 평범한 창고는 소리가 난다 ----------
    warp_to_shop(host, m1)
    buy_slot(m1i, host, m1, "Warehouse")
    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 12.0, _z=sz + 12.0)
    time.sleep(1.5)
    loud_count = try_place(m1i, host, m1, sx + 12.0, sz + 12.0)

    loud_sfx, loud_dump = sfx_window(host, 6.0)
    check("⑧ 대조 — 숨은 창고가 없는 창고는 생산음이 실제로 난다",
          loud_count >= 1 and loud_sfx > 0,
          "창고=%s개 WarehouseTick 재생=%s회 (기대 >0) · 그 창의 소리=[%s]"
          % (loud_count, loud_sfx, loud_dump), case="E-21")

    note("계측기 대조", "⑦(0회)과 ⑧(%s회)이 같은 훅·같은 창으로 재졌다 — "
                        "⑧이 0이면 ⑦의 초록은 계측기가 죽은 것이다" % loud_sfx)
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
