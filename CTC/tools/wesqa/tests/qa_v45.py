# -*- coding: utf-8 -*-
"""직업·2단계 UI 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 클론 둘=쥐** — 직업을 새기는 쪽이 서버여야 한다.

**화면에 올라간 글자를 읽는다.** Model 값(MouseJob.Value)만 보면 "값은 바뀌는데 화면은
그대로"를 못 잡는다 — 실제로 임명당한 쥐가 자기가 편이 바뀐 걸 모르는 상태였다.

한 판에 한 번만 돌린다(고양이를 죽이고 2단계로 넘긴다).
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


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    cat, m1, m2 = p[0], p[1], p[2]
    roles = [x.invoke("TestGetRole") for x in p]
    check("역할 확인 (호스트=고양이 · 쥐 2)", roles[0] == "Cat" and roles.count("Mouse") == 2, str(roles))
    if roles[0] != "Cat":
        return summary()

    m1_id = int(m1.invoke("TestGetLocalClientId"))
    m2_id = int(m2.invoke("TestGetLocalClientId"))

    # ---------- 조작 안내 ----------
    # 잠행은 2026-08-10부터 무음이 아니다. 안내가 거짓이면 안 들킬 줄 알고 서 있다가 잡힌다.
    mouse_controls = m1.invoke("TestGetControlsText")
    cat_controls = cat.invoke("TestGetControlsText")
    check("안내 — 쥐 조작이 화면에 있다", "살금살금" in mouse_controls, mouse_controls[:40])
    check("안내 — '무음'이라고 쓰지 않는다(쥐)", "무음" not in mouse_controls)
    check("안내 — '무음'이라고 쓰지 않는다(고양이)", "무음" not in cat_controls)
    check("안내 — 잠행 발소리 거리를 적는다", "발소리" in mouse_controls and "발소리" in cat_controls)
    check("안내 — 깡패가 아니면 F 안내가 없다", "임명" not in mouse_controls)

    # ---------- 직업 HUD ----------
    check("직업 HUD — 직업이 없으면 안 뜬다", m1.invoke("TestGetJobHudText") == "",
          m1.invoke("TestGetJobHudText"))

    cat.invoke("TestSetMouseJob", _clientId=m1_id, _job="Gang")
    time.sleep(1.0)
    boss_hud = m1.invoke("TestGetJobHudText")
    check("직업 HUD — 깡패가 되면 뜬다", "깡패" in boss_hud, boss_hud)
    check("직업 HUD — 뭘 해야 하는지 같이 적는다", "부하" in boss_hud or "쓰러" in boss_hud, boss_hud)
    check("안내 — 깡패가 되면 F 안내가 붙는다", "임명" in m1.invoke("TestGetControlsText"))

    cat.invoke("TestSetMouseJob", _clientId=m2_id, _job="Minion")
    time.sleep(1.0)
    minion_hud = m2.invoke("TestGetJobHudText")
    # 여기가 이 스위트의 핵심이다 — 거절을 못 하는 임명이라, 바뀐 걸 본인이 알아야 한다
    check("직업 HUD — 부하가 되면 본인 화면에 뜬다", "부하" in minion_hud, minion_hud)

    # ---------- 머리 위 표식 ----------
    time.sleep(0.8)
    mouse_badges = m1.invoke("TestGetJobBadgeCount")
    cat_badges = cat.invoke("TestGetJobBadgeCount")
    check("표식 — 쥐 화면에는 깡패 편이 보인다(보스+부하 2)", mouse_badges == 2, f"{mouse_badges}개")
    check("표식 — 고양이 화면에는 안 보인다", cat_badges == 0, f"{cat_badges}개")

    # ---------- 2단계 타이머 ----------
    before = cat.invoke("TestGetTimerText")
    check("타이머 — 1단계에는 시계만 나온다", "깡패전" not in before, before)

    cat.invoke("TestKillCat")
    time.sleep(3.0)
    check("단계 — 2단계로 갔다", cat.invoke("TestGetPhase") == "Gang", cat.invoke("TestGetPhase"))

    after = m1.invoke("TestGetTimerText")
    check("타이머 — 2단계라고 알린다", "깡패전" in after, after)
    # 1단계 시계가 그대로 흐르면 4분을 준 게 화면에 안 나온다
    check("타이머 — 새 4분 시계를 보여 준다", "4:" in after or "3:5" in after, after)

    # ---------- 결과 문구 ----------
    # 쥐 둘이 다 깡패 편이라 나머지 쥐가 0 → 깡패 팀 승리로 끝난다.
    # 예전엔 "쥐 승이 아니면 고양이 승"이라 여기서 CAT WINS!가 떴다(2026-08-10 실측).
    time.sleep(2.5)
    result = cat.invoke("TestGetMatchResult")
    check("결과 — 깡패 팀이 이겼다", result == "GangWin", result)
    label = cat.invoke("TestGetResultText")
    check("결과 문구 — 고양이 승이라고 하지 않는다", "CAT" not in label, label)
    check("결과 문구 — 깡패 승리라고 적는다", "GANG" in label, label)

    return summary()


def summary():
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + (f"  | {detail}" if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    print(f"{passed}/{len(RESULTS)}")


main()
