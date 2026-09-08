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
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 19      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


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


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    cat, m1, m2 = p[0], p[1], p[2]
    roles = [x.invoke("TestGetRole") for x in p]
    check("역할 확인 (호스트=고양이 · 쥐 2)", roles[0] == "Cat" and roles.count("Mouse") == 2, str(roles))
    if roles[0] != "Cat":
        return summary(aborted=True)

    m1_id = int(m1.invoke("TestGetLocalClientId"))
    m2_id = int(m2.invoke("TestGetLocalClientId"))

    # ---------- 능력 바 (2026-08-17: 좌하단 글자 칩 → 핫바 아래 줄 슬롯) ----------
    # 화면에 올라간 슬롯을 읽는다 — "키:이름:상태:남은초|..." (TestGetAbilityBar).
    mouse_bar = m1.invoke("TestGetAbilityBar")
    cat_bar = cat.invoke("TestGetAbilityBar")
    # 해금 안 된 칸은 아예 없다(2026-08-17) — 대시 미구매·평쥐·전직 전이면 바가 비어 있다
    check("능력 바 — 해금 전 쥐는 빈 바", mouse_bar == "", mouse_bar)
    check("능력 바 — 전직 전 고양이는 빈 바", cat_bar == "", cat_bar)

    # ---------- 직업 HUD ----------
    check("직업 HUD — 직업이 없으면 안 뜬다", m1.invoke("TestGetJobHudText") == "",
          m1.invoke("TestGetJobHudText"))

    cat.invoke("TestSetMouseJob", _clientId=m1_id, _job="Gang")
    time.sleep(1.0)
    boss_hud = m1.invoke("TestGetJobHudText")
    check("직업 HUD — 깡패가 되면 뜬다", "깡패" in boss_hud, boss_hud)
    check("직업 HUD — 뭘 해야 하는지 같이 적는다", "부하" in boss_hud or "쓰러" in boss_hud, boss_hud)
    check("능력 바 — 깡패가 되면 임명 칸이 생긴다", m1.invoke("TestGetAbilityBar") == "F:임명:Ready:0", m1.invoke("TestGetAbilityBar"))

    # 표식은 깡패팀에게만 보인다(2026-08-14) — 깡패가 아닌 쥐(m2, 아직 평쥐)는 보스 표식을 못 본다
    check("표식 — 깡패가 아닌 쥐에게는 안 보인다",
          m2.invoke("TestGetJobBadgeCount") == 0, f"{m2.invoke('TestGetJobBadgeCount')}개")

    cat.invoke("TestSetMouseJob", _clientId=m2_id, _job="Minion")
    time.sleep(1.0)
    minion_hud = m2.invoke("TestGetJobHudText")
    # 여기가 이 스위트의 핵심이다 — 거절을 못 하는 임명이라, 바뀐 걸 본인이 알아야 한다
    check("직업 HUD — 부하가 되면 본인 화면에 뜬다", "부하" in minion_hud, minion_hud)

    # ---------- 머리 위 표식 ----------
    # 규칙(2026-08-14): 깡패팀만 본다 · 보스는 첫 임명 전까지 · 부하는 임명 3초 팝업.
    # 여기서는 임명이 TestSetMouseJob(직접 세팅)이라 보스의 '첫 임명' 플래그는 안 선다 —
    # 보스 표식은 남고, 부하 팝업만 3초 뒤 꺼진다.
    time.sleep(0.8)
    mouse_badges = m1.invoke("TestGetJobBadgeCount")
    cat_badges = cat.invoke("TestGetJobBadgeCount")
    check("표식 — 깡패팀 화면에는 보인다(보스+부하 팝업)", mouse_badges == 2, f"{mouse_badges}개")
    check("표식 — 고양이 화면에는 안 보인다", cat_badges == 0, f"{cat_badges}개")

    time.sleep(3.0)
    check("표식 — 부하 팝업은 3초 뒤 꺼진다(보스만 남음)",
          m1.invoke("TestGetJobBadgeCount") == 1, f"{m1.invoke('TestGetJobBadgeCount')}개")

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
