# -*- coding: utf-8 -*-
"""v3.9 고양이 전직 검증 (MPPM 3인). 문턱은 누적 획득 장난기다(2026-09-09 계획 3 Task 5).

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

    # 문턱은 **누적 획득 장난기**다(계획 3 Task 5). 강화를 아무리 올려도 그것만으로는 안 열린다 —
    # 옛 조건("강화 둘 다 만렙")은 정해진 두 테크를 사라는 강제였다.
    cat.invoke("TestMaxAllUpgrades")
    time.sleep(1.0)
    check("전직 — 강화를 다 올려도 그것만으론 안 열린다", not cat.invoke("TestGetCanAdvance"),
          f"강화 {cat.invoke('TestGetUpgradeStacks')} · 누적 {cat.invoke('TestGetAdvanceMischief')}")

    # **아무것도 사지 않고** 벌기만 해서 문턱을 넘긴다 — 강제가 사라졌다는 증거
    cat.invoke("TestGrantAdvanceMischief")
    time.sleep(1.0)
    check("전직 — 누적 획득이 문턱에 닿으면 열린다", cat.invoke("TestGetCanAdvance"),
          f"누적 {cat.invoke('TestGetAdvanceMischief')}")

    # ---------- 선택 ----------
    cat.invoke("TestChooseCatClass", _id="Fire")
    time.sleep(1.0)
    check("전직 — 고르면 반영된다", cat.invoke("TestGetCatClass") == "Fire",
          cat.invoke("TestGetCatClass"))
    check("전직 — 쥐 클라에도 보인다", m1.invoke("TestGetCatClassOf", _clientId=0) == "Fire",
          m1.invoke("TestGetCatClassOf", _clientId=0))
    check("전직 — 두 번은 못 한다", not cat.invoke("TestGetCanAdvance"),
          f"누적 {cat.invoke('TestGetAdvanceMischief')}")
    check("능력 바 — 전직하면 Q 칸이 전직명으로 생긴다", cat.invoke("TestGetAbilityBar") == "Q:불:Ready:0",
          cat.invoke("TestGetAbilityBar"))

    # ---------- 스킬 ----------
    check("스킬 — 쓰기 전에는 꺼져 있다", not cat.invoke("TestGetCatSkillActive"))

    cat.invoke("TestUseCatSkill")
    time.sleep(0.6)
    check("스킬 — 쓰면 켜진다", cat.invoke("TestGetCatSkillActive"))
    check("스킬 — 쥐 클라에도 켜져 보인다", m1.invoke("TestGetCatSkillActiveOf", _clientId=0),
          "복제됨")
    check("능력 바 — 쓰면 Q 칸이 Active", ":Active:" in cat.invoke("TestGetAbilityBar"), cat.invoke("TestGetAbilityBar"))
    # 쿨다운은 서버 시각 NetworkVariable(2026-08-17) — 다른 클라(쥐)가 읽어도 0이 아니어야 복제된 것
    remote_cd = m1.invoke("TestGetCatSkillCooldownOf", _clientId=0)
    check("스킬 — 쿨다운이 다른 클라에도 복제된다", remote_cd > 20, f"쥐가 읽은 고양이 쿨 {remote_cd:.1f}초")

    # 불: 켜진 동안 덮치기 피해가 오른다.
    # **한 방에 눕는지**로 잰다 — 피해량 차이로 재면 안 된다. 120이면 쥐(100)가 즉사한다.
    m1_id = int(m1.invoke("TestGetLocalClientId"))
    check("불 — 때리기 전에는 살아 있다", m1.invoke("TestGetLocalHp") > 0)

    cat.invoke("TestTeleportToOtherPlayer", _clientId=m1_id)
    time.sleep(0.5)
    cat.invoke("TestPounce")
    time.sleep(1.5)
    check("불 — 스킬 중 덮치기 한 방에 쥐가 눕는다", m1.invoke("TestGetLocalHp") <= 0,
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
    bar = cat.invoke("TestGetAbilityBar")
    check("능력 바 — 쿨다운 중엔 Cooldown + 남은 초", ":Cooldown:" in bar and int(bar.split(":")[-1]) > 0, bar)

    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + (f"  | {detail}" if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    print(f"{passed}/{len(RESULTS)}")


main()
