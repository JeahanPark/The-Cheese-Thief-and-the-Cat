# -*- coding: utf-8 -*-
"""v3.9 고양이 전직 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 클론 둘=쥐** — 전직하는 쪽이 서버여야 조건을 직접 세울 수 있다.

한 판에 한 번만 돌린다(전직은 되돌릴 수 없다).
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
    cat, m1 = p[0], p[1]
    roles = [x.invoke("TestGetRole") for x in p]
    check("역할 확인 (호스트=고양이 · 쥐 2)", roles[0] == "Cat" and roles.count("Mouse") == 2, str(roles))
    if roles[0] != "Cat":
        return

    # ---------- 조건 ----------
    check("전직 — 처음에는 못 한다", not cat.invoke("TestGetCanAdvance"),
          f"강화 {cat.invoke('TestGetUpgradeStacks')}")
    check("전직 — 처음에는 직업이 없다", cat.invoke("TestGetCatClass") == "None")

    # 강화 하나만 올려도 안 열려야 한다 — 열리면 '전직이 곧 최적 구매'가 된다
    cat.invoke("TestGrantCheeseAll")
    time.sleep(0.5)
    cat.invoke("TestMaxOneUpgrade")
    time.sleep(1.0)
    check("전직 — 강화 하나만 올리면 안 열린다", not cat.invoke("TestGetCanAdvance"),
          f"강화 {cat.invoke('TestGetUpgradeStacks')}")

    cat.invoke("TestMaxAllUpgrades")
    time.sleep(1.0)
    check("전직 — 모든 강화를 올리면 열린다", cat.invoke("TestGetCanAdvance"),
          f"강화 {cat.invoke('TestGetUpgradeStacks')}")

    # ---------- 선택 ----------
    cat.invoke("TestChooseCatClass", _id="Fire")
    time.sleep(1.0)
    check("전직 — 고르면 반영된다", cat.invoke("TestGetCatClass") == "Fire",
          cat.invoke("TestGetCatClass"))
    check("전직 — 쥐 클라에도 보인다", m1.invoke("TestGetCatClassOf", _clientId=0) == "Fire",
          m1.invoke("TestGetCatClassOf", _clientId=0))
    check("전직 — 두 번은 못 한다", not cat.invoke("TestGetCanAdvance"))

    # ---------- 스킬 ----------
    check("스킬 — 쓰기 전에는 꺼져 있다", not cat.invoke("TestGetCatSkillActive"))

    cat.invoke("TestUseCatSkill")
    time.sleep(0.6)
    check("스킬 — 쓰면 켜진다", cat.invoke("TestGetCatSkillActive"))
    check("스킬 — 쥐 클라에도 켜져 보인다", m1.invoke("TestGetCatSkillActiveOf", _clientId=0),
          "복제됨")

    # 불: 켜진 동안 덮치기 피해가 오른다.
    # **한 방에 눕는지**로 잰다 — 피해량 차이로 재면 안 된다. 120이면 쥐(100)가 즉사하고,
    # 즉사한 쥐는 감옥으로 가면서 HP가 100으로 복구돼 "차이 0"으로 읽힌다(2026-08-10 실측).
    m1_id = int(m1.invoke("TestGetLocalClientId"))
    check("불 — 때리기 전에는 안 갇혀 있다", not m1.invoke("TestGetJailed"))

    cat.invoke("TestTeleportToOtherPlayer", _clientId=m1_id)
    time.sleep(0.5)
    cat.invoke("TestPounce")
    time.sleep(1.5)
    check("불 — 스킬 중 덮치기 한 방에 쥐가 눕는다", m1.invoke("TestGetJailed"),
          "기본 60이면 두 방이 필요하다")

    # 지속이 끝나면 꺼진다
    for _ in range(20):
        time.sleep(1.0)
        if not cat.invoke("TestGetCatSkillActive"):
            break
    check("스킬 — 지속이 끝나면 꺼진다", not cat.invoke("TestGetCatSkillActive"))

    # 쿨다운 중에는 다시 못 쓴다
    cat.invoke("TestUseCatSkill")
    time.sleep(0.6)
    check("스킬 — 쿨다운 중에는 다시 못 쓴다", not cat.invoke("TestGetCatSkillActive"),
          f"남은 쿨다운 {cat.invoke('TestGetCatSkillCooldown'):.0f}초")

    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + (f"  | {detail}" if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    print(f"{passed}/{len(RESULTS)}")


main()
