# -*- coding: utf-8 -*-
"""v3.9 쥐 직업 + 2단계 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 클론 둘=쥐** — 직업을 새기고 고양이를 죽이는 쪽이 서버여야 한다.

한 판에 한 번만 돌린다(고양이를 죽이고 판을 2단계로 넘긴다).
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
        return

    m1_id = int(m1.invoke("TestGetLocalClientId"))
    m2_id = int(m2.invoke("TestGetLocalClientId"))

    check("직업 — 처음에는 없다", m1.invoke("TestGetMouseJob") == "None")
    check("단계 — 처음에는 1단계다", cat.invoke("TestGetPhase") == "Hunt")

    # ---------- 상자에서 직업권이 나온다 ----------
    # 직업권은 **벽 뒤에만** 있다(2026-08-11) — 벽을 부숴 상자를 꺼내고, 그 상자를 또 부숴야 한다
    check("상자 — 바닥에는 없다", cat.invoke("TestGetBoxCount") == 0)
    cat.invoke("TestBreakFirstAlcove")
    time.sleep(1.2)
    check("벽감 — 부수면 상자가 드러난다", cat.invoke("TestGetBoxCount") == 1,
          f"{cat.invoke('TestGetBoxCount')}개")
    cat.invoke("TestBreakFirstBox")
    time.sleep(1.2)
    check("상자 — 부수면 자리에서 사라진다", cat.invoke("TestGetBoxCount") == 0,
          "부순 뒤 자리에서 사라짐")

    # ---------- 임명 ----------
    cat.invoke("TestSetMouseJob", _clientId=m1_id, _job="Gang")
    time.sleep(0.8)
    check("직업 — 깡패권이 반영된다", m1.invoke("TestGetMouseJob") == "Gang",
          m1.invoke("TestGetMouseJob"))
    check("직업 — 다른 클라에도 보인다", cat.invoke("TestGetMouseJobOf", _clientId=m1_id) == "Gang")

    # 멀리 있으면 임명이 안 된다 — 거리 판정은 서버가 한다.
    # **둘 다 자리를 지정한다.** 한쪽만 옮기면 하필 상대 스폰 옆에 떨어질 수 있다
    # (2026-08-10 실측: 요새 스폰이 13,13 바로 옆이라 2.4m로 붙어 임명이 성립했다).
    m1.invoke("TestTeleportLocalPlayer", _x=-12.0, _z=-12.0)
    m2.invoke("TestTeleportLocalPlayer", _x=12.0, _z=12.0)
    time.sleep(1.2)
    gap = m2.invoke("TestGetDistanceTo", _x=-12.0, _z=-12.0)
    m1.invoke("TestRecruit")
    time.sleep(0.8)
    check("임명 — 멀면 안 된다", m2.invoke("TestGetMouseJob") == "None",
          f"{m2.invoke('TestGetMouseJob')} · 사이 {gap:.1f}m")

    # ---------- 2단계 진입 ----------
    # **부하는 아직 임명하지 않는다.** 쥐가 둘뿐이라 둘 다 깡패 편이면 2단계가 열리자마자
    # 끝나 버려서 "판이 안 끝난다"를 못 잰다(2026-08-10 실측). 임명은 2단계 안에서 한다 —
    # 그게 실제 시나리오이기도 하다.
    check("단계 — 고양이가 살아 있으면 1단계다", cat.invoke("TestGetPhase") == "Hunt")

    cat.invoke("TestKillCat")
    time.sleep(3.0)
    check("단계 — 고양이가 죽고 깡패가 있으면 2단계로 간다", cat.invoke("TestGetPhase") == "Gang",
          cat.invoke("TestGetPhase"))
    check("단계 — 판이 안 끝났다(보스 1 vs 나머지 1)", cat.invoke("TestGetMatchResult") == "None",
          cat.invoke("TestGetMatchResult"))

    remaining = cat.invoke("TestGetPhaseRemaining")
    check("단계 — 새 시계 4분을 받는다", 230 < remaining <= 240, f"{remaining:.0f}초")

    # ---------- 2단계 안에서 임명 ----------
    # 다가가면 된다. **거절은 없다.**
    m2.invoke("TestTeleportToOtherPlayer", _clientId=m1_id)
    time.sleep(1.0)
    m1.invoke("TestRecruit")
    time.sleep(1.0)
    check("임명 — 다가가면 부하가 된다(거절 없음)", m2.invoke("TestGetMouseJob") == "Minion",
          m2.invoke("TestGetMouseJob"))

    # 나머지 쥐가 0명이 됐다 — 깡패 팀 승리
    time.sleep(2.5)
    check("2단계 — 나머지 쥐가 없으면 깡패 승리", cat.invoke("TestGetMatchResult") == "GangWin",
          cat.invoke("TestGetMatchResult"))


    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + (f"  | {detail}" if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    print(f"{passed}/{len(RESULTS)}")


main()
