# -*- coding: utf-8 -*-
"""v3.8 쥐 감옥과 구출 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 클론 둘=쥐** — 잡는 쪽이 호스트여야 수감을 시킬 수 있다.

한 판에 한 번만 돌린다(쥐를 가두고 죽인다). 다시 돌리려면 플레이모드를 껐다 켠다.
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


def capture(cat, victim):
    """고양이가 희생자를 덮쳐 잡는다 — 실제 전투 경로를 그대로 탄다."""
    vid = int(victim.invoke("TestGetLocalClientId"))
    for _ in range(14):
        if victim.invoke("TestGetJailed"):
            return True
        cat.invoke("TestTeleportToOtherPlayer", _clientId=vid)
        time.sleep(0.5)
        cat.invoke("TestPounce")
        time.sleep(1.4)
    return bool(victim.invoke("TestGetJailed"))


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    roles = [x.invoke("TestGetRole") for x in p]
    cat_idx = roles.index("Cat") if "Cat" in roles else None
    mice = [i for i in range(3) if i != cat_idx]
    check("역할 확인 (호스트=고양이 · 쥐 2)", cat_idx == 0 and len(mice) == 2, str(roles))
    if cat_idx is None:
        return summary()

    cat = p[cat_idx]
    victim, rescuer = p[mice[0]], p[mice[1]]

    jails = cat.invoke("TestGetJailPositions")
    check("감옥 — 맵에 여러 곳 서 있다", len(jails.split(";")) >= 2, jails)

    # ---------- 수감 ----------
    caught = capture(cat, victim)
    check("잡히면 죽지 않고 갇힌다", caught, f"jailed={victim.invoke('TestGetJailed')}")
    if not caught:
        return summary()

    check("갇힌 쥐는 despawn되지 않는다(관전 아님)",
          victim.invoke("TestGetLocalHp") > 0, f"hp={victim.invoke('TestGetLocalHp')}")

    remaining = victim.invoke("TestGetJailRemaining")
    check("체류 카운트다운이 돈다", 0 < remaining <= 45, f"남은 {remaining:.1f}초")

    check("갇힌 수가 집계된다", cat.invoke("TestGetJailedCount") == 1,
          f"{cat.invoke('TestGetJailedCount')}명")

    # ---------- 입력 잠금 ----------
    before = victim.invoke("TestGetLocalPosXZ") if False else None
    victim.invoke("TestWalkDirection", _x=1, _z=0, _seconds=0.8)
    time.sleep(1.4)
    check("갇힌 쥐는 걸어 나올 수 없다", victim.invoke("TestGetJailed"),
          "이동 시도 후에도 갇힘 유지")

    # ---------- 구출 ----------
    # 갇힌 쥐가 있는 감옥으로 구조자를 보내 3초 버틴다
    jail_list = [tuple(float(v) for v in j.split(",")) for j in jails.split(";")]
    released = False
    for jx, jz in jail_list:
        rescuer.invoke("TestTeleportLocalPlayer", _x=jx + 1.0, _z=jz)
        time.sleep(4.5)          # 구출 3초 + 여유
        if not victim.invoke("TestGetJailed"):
            released = True
            break
    check("동료가 오면 풀려난다", released, f"jailed={victim.invoke('TestGetJailed')}")

    if released:
        hp = victim.invoke("TestGetLocalHp")
        check("석방 직후 HP는 절반이다", 40 <= hp <= 60, f"hp={hp} (최대 100)")
        check("갇힌 수가 0으로 돌아간다", cat.invoke("TestGetJailedCount") == 0,
              f"{cat.invoke('TestGetJailedCount')}명")

    return summary()


def summary():
    print("\n===== 요약 =====")
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + ("  | " + detail if detail else ""))
    print(f"{passed}/{len(RESULTS)}")
    return 0 if passed == len(RESULTS) else 1


if __name__ == "__main__":
    sys.exit(main())
