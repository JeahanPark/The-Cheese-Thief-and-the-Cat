# -*- coding: utf-8 -*-
"""고양이 고정 6칸 핫바 · 만렙 표시 · 전직(Q) — 사람 손 입력 경로. MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이** — 세팅: `python setup_match.py 0 cat 3` (클론 둘 다 쥐).

## 왜 이 스위트가 있는가

2026-09-11 실플레이에서 **같은 테크가 두 칸에 떴다**(발톱이 1·2번, 광분이 3·5번).
뿌리는 「쥐용 UI를 고양이에 그대로 씌운 것」이었다 — 쥐는 10종을 5칸으로 훑어야 해서
「같은 숫자키 반복 = 다음 후보」가 필요하지만, 고양이는 6종뿐이고 상점에도 안 가서
그 장치가 값을 못 하고 부작용만 냈다.

그래서 고양이 핫바는 **칸이 곧 테크**가 됐다(카탈로그 순서로 1키 = 1테크, 넘기기 없음).
여기서 재는 것은 그 계약이다 — 그리고 **훅으로 우회하지 않는다**:
`TestPressSlotKey` · `TestPressActiveKey` · `TestPressSkillKey` · `TestClickCatClassButton`만 쓴다.
`TestBuyTechLocal` · `TestUseTech` · `TestChooseCatClass`는 이 스위트에 없다 — 그것들이
사람 손 분기를 건너뛰어 「고양이는 산 광분을 켤 수 없다」를 초록으로 통과시킨 적이 있다
(2026-09-09 감사 G-1).

## 무엇을 재는가

  ①~④ 6칸이 서로 다른 6종으로, 카탈로그 순서로 뜬다 · 모드 줄에 Enter 안내가 없다
  ⑤~⑦ 전직: 전에는 능력 바에 Q칸이 없다 → 조건을 채우면 창이 뜬다 → 눌러 고르면 Q칸이 생긴다
  ⑧~⑫ 숫자키: 안 샀으면 산다 · 샀으면 키운다 · 칸이 안 넘어간다 · 6종을 다 살 수 있다 · 만렙이 보인다
  ⑬~⑭ Q로 전직 스킬이 켜지고, 그 위에 Space로 광분이 겹쳐도 서로 끄지 않는다

한 판에 한 번만 돌린다(전직은 되돌릴 수 없다). 다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 15     # ⓪ 전제 + ①~⑭

# 고양이 카탈로그 순서(TechInfo.csv 등장 순서) = 칸 순서. 이게 곧 「1키가 무엇인가」다.
CAT_LABELS = ["발톱", "발 구르기", "두꺼운 가죽", "배고픔", "광분", "예민한 귀"]
SLOT_HIDE = 2      # 두꺼운 가죽 — 사고 키우는 데 쓴다
SLOT_STOMP = 1     # 발 구르기 — 만렙까지 올린다(80+120+160 = 360)
SLOT_FRENZY = 4    # 광분 — 숫자키로 사고 Space로 켠다


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


def slot_labels(host):
    """핫바가 **실제로 그린** 칸 이름들 — "3  두꺼운 가죽 Lv1#200#..." → "두꺼운 가죽 Lv1".

    번호 접두를 떼고 이름만 남긴다. 꺼진 칸은 훅이 이미 건너뛰므로 길이가 곧 보이는 칸 수다.
    """
    raw = host.invoke("TestGetHotbarSlots") or ""
    out = []
    for chunk in raw.split("|"):
        if not chunk:
            continue
        label = chunk.split("#")[0]
        parts = label.split("  ", 1)
        out.append(parts[1].strip() if len(parts) > 1 else label.strip())
    return out


def tech_name(label):
    """"두꺼운 가죽 Lv2 · 만렙" → "두꺼운 가죽" — 레벨·만렙 꼬리를 떼고 테크 이름만."""
    return label.split(" Lv")[0].strip()


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
    note("커서 잠금", "%s (풀려 있으면 Q 가드에 막힌다 — 게임 뷰에 포커스를 줘라)"
         % host.invoke("TestGetCursorLocked"))

    # ---------- ①~④ 고정 6칸 ----------
    labels = slot_labels(host)
    names = [tech_name(x) for x in labels]
    check("① 고양이 핫바는 6칸이다(테크 종류 수만큼)",
          len(labels) == len(CAT_LABELS),
          "칸 %d개 = %s" % (len(labels), labels))
    check("② 6칸이 서로 다른 6종이다  ← 2026-09-11 신고(발톱이 1·2번, 광분이 3·5번)",
          len(set(names)) == len(names) and len(names) == len(CAT_LABELS),
          "이름=%s" % names)
    check("③ 칸 순서가 카탈로그 순서다 — 1키 = 1테크가 판마다 고정이다",
          names == CAT_LABELS,
          "본 것=%s / 기대=%s" % (names, CAT_LABELS))
    mode = host.invoke("TestGetHotbarModeText") or ""
    check("④ 모드 줄이 「숫자키 = 산다 / 키운다」이고 Enter 안내가 없다(넘기기가 없으니까)",
          "숫자키 = 산다 / 키운다" in mode and "Enter" not in mode,
          "모드 줄=%r" % mode)

    # ---------- ⑤ 전직 전 ----------
    bar_before = host.invoke("TestGetAbilityBar") or ""
    popup_before = bool(host.invoke("TestGetCatClassPopupOpen"))
    earned_before = host.invoke("TestGetAdvanceMischief")
    check("⑤ 전직 전 — 능력 바에 Q칸이 없고 선택 창도 안 떠 있다",
          "Q:" not in bar_before and not popup_before,
          "능력 바=%r · 창=%s · 누적 %s" % (bar_before, popup_before, earned_before))

    # ---------- ⑥ 조건을 채우면 창이 뜬다 ----------
    # 문턱은 **누적 획득** 장난기(CatAdvanceMischief) — 잔액이 아니다. 아무것도 안 사도 열린다.
    host.invoke("TestGrantAdvanceMischief")
    time.sleep(1.5)
    can = bool(host.invoke("TestGetCanAdvance"))
    popup_open = bool(host.invoke("TestGetCatClassPopupOpen"))
    check("⑥ 누적 장난기가 문턱을 넘으면 전직 선택 창이 저절로 뜬다",
          can and popup_open,
          "CanAdvance=%s · 창=%s · 누적 %s"
          % (can, popup_open, host.invoke("TestGetAdvanceMischief")))

    # ---------- ⑦ 창의 버튼을 눌러 고른다(사람 손) ----------
    clicked = bool(host.invoke("TestClickCatClassButton", _index=0))   # 0 = 불
    time.sleep(1.5)
    cls = host.invoke("TestGetCatClass")
    bar_after = host.invoke("TestGetAbilityBar") or ""
    popup_closed = not bool(host.invoke("TestGetCatClassPopupOpen"))
    check("⑦ 「불」 버튼을 눌러 전직 — 창이 닫히고 능력 바에 Q칸이 생긴다(이름=불)",
          clicked and cls == "Fire" and popup_closed and "Q:불" in bar_after,
          "클릭=%s · 전직=%s · 창닫힘=%s · 능력 바=%r"
          % (clicked, cls, popup_closed, bar_after))

    # 이제부터는 사고 키운다 — 장난기를 넉넉히 준다(6종 + 만렙까지 1000 남짓이면 된다).
    for _ in range(3):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)
    time.sleep(0.8)

    # ---------- ⑧ 안 산 칸의 숫자키 = 산다 ----------
    pressed = bool(host.invoke("TestPressSlotKey", _slot=SLOT_HIDE))
    time.sleep(1.3)
    hide_lv1 = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatHide")
    labels_after = slot_labels(host)
    check("⑧ 안 산 칸의 숫자키 = 산다 — 3번이 두꺼운 가죽 Lv1이 된다",
          pressed and hide_lv1 == 1
          and tech_name(labels_after[SLOT_HIDE]) == CAT_LABELS[SLOT_HIDE],
          "눌림=%s Lv=%s 칸=%r" % (pressed, hide_lv1, labels_after[SLOT_HIDE]))

    # ---------- ⑨ 같은 칸을 다시 = 키운다(넘어가지 않는다) ----------
    host.invoke("TestPressSlotKey", _slot=SLOT_HIDE)
    time.sleep(1.3)
    hide_lv2 = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatHide")
    labels_lv2 = slot_labels(host)
    check("⑨ 같은 숫자키를 다시 = 키운다 — Lv2가 되고 칸은 여전히 그 테크다(후보가 안 넘어간다)",
          hide_lv2 == 2 and tech_name(labels_lv2[SLOT_HIDE]) == CAT_LABELS[SLOT_HIDE],
          "Lv %s→%s · 칸=%r" % (hide_lv1, hide_lv2, labels_lv2[SLOT_HIDE]))

    # ---------- ⑩ 광분 칸: 숫자키는 「산다」이지 「쓴다」가 아니다 ----------
    host.invoke("TestPressSlotKey", _slot=SLOT_FRENZY)
    time.sleep(1.3)
    frenzy_lv = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatFrenzy")
    frenzy_state = host.invoke("TestGetCatFrenzyState", _clientId=c)
    check("⑩ 광분 칸의 숫자키 = 산다 — Lv1이 되고 광분은 **안 켜진다**",
          frenzy_lv == 1 and frenzy_state == "Off",
          "Lv=%s · 광분=%s" % (frenzy_lv, frenzy_state))

    # ---------- ⑪ 6종을 다 산다 — 칸 상한이 역할별이라는 증거 ----------
    # 전역 상한 5가 그대로였다면 여섯째에서 SlotsFull로 막혔다.
    for slot in (0, 1, 3, 5):
        host.invoke("TestPressSlotKey", _slot=slot)
        time.sleep(1.1)
    owned_count = host.invoke("TestGetTechSlotCount", _clientId=c)
    ear_lv = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatEar")
    check("⑪ 6종을 전부 산다 — 여섯째가 「칸이 다 찼다」로 막히지 않는다",
          owned_count == len(CAT_LABELS) and ear_lv >= 1,
          "보유 %s칸 · 예민한 귀 Lv%s · 칸=%s" % (owned_count, ear_lv, slot_labels(host)))

    # ---------- ⑫ 만렙 칸이 흐리게 보인다 ----------
    for _ in range(2):
        host.invoke("TestPressSlotKey", _slot=SLOT_STOMP)
        time.sleep(1.1)
    stomp_max = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatStomp")
    host.invoke("TestPressSlotKey", _slot=SLOT_STOMP)    # 만렙인데 한 번 더
    time.sleep(1.2)
    stomp_after = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatStomp")
    labels_max = slot_labels(host)
    check("⑫ 다 키운 칸은 「만렙」이라 적히고 더 눌러도 안 오른다(칸은 지우지 않는다 — 곧 스탯 창이다)",
          stomp_max == 3 and stomp_after == 3 and "만렙" in labels_max[SLOT_STOMP],
          "Lv %s→%s · 칸=%r" % (stomp_max, stomp_after, labels_max[SLOT_STOMP]))

    # ---------- ⑬ Q(사람 손) → 전직 스킬이 켜진다 ----------
    q_pressed = bool(host.invoke("TestPressSkillKey"))
    time.sleep(1.0)
    skill_state = host.invoke("TestGetCatSkillState")
    skill_cd = host.invoke("TestGetCatSkillCooldown")
    bar_skill = host.invoke("TestGetAbilityBar") or ""
    check("⑬ Q(사람 손) → 전직 스킬이 켜지고 능력 바의 Q칸이 Ready가 아니다",
          q_pressed and skill_state == "On" and skill_cd > 0.5
          and "Q:불:Ready" not in bar_skill and "Q:불" in bar_skill,
          "Q=%s · 스킬=%s · 쿨 %.1f초 · 능력 바=%r"
          % (q_pressed, skill_state, skill_cd if skill_cd is not None else -1, bar_skill))

    # ---------- ⑭ 그 위에 Space → 둘이 서로 끄지 않는다 ----------
    space_used = bool(host.invoke("TestPressActiveKey"))
    time.sleep(0.8)
    frenzy_on = host.invoke("TestGetCatFrenzyState", _clientId=c)
    skill_still = host.invoke("TestGetCatSkillState")
    check("⑭ 전직 스킬(Q)과 광분(Space)은 서로 간섭하지 않는다 — 둘 다 켜진 채로 남는다",
          space_used and frenzy_on == "On" and skill_still == "On",
          "Space=%s · 광분=%s · 스킬=%s" % (space_used, frenzy_on, skill_still))

    note("덮치기 배율", "둘 다 켜도 곱하지 않는다(CatClassLogic.PounceMultiplier = 큰 쪽) — "
                     "EditMode PounceMultiplier_BothOn_TakesMaxNotProduct가 지킨다")
    note("핫바 칸", "%s" % slot_labels(host))
    note("능력 바", "%r" % host.invoke("TestGetAbilityBar"))
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
