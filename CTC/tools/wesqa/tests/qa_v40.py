# -*- coding: utf-8 -*-
"""v3.8 5번 제3세력 강아지 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 클론 둘=쥐** — 벽감을 부수고 강아지를 때리는 쪽이 서버여야 한다.

한 판에 한 번만 돌린다(강아지를 풀고 죽인다). 다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 16      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


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
    roles = [x.invoke("TestGetRole") for x in p]
    cat = p[0]
    check("역할 확인 (호스트=고양이 · 쥐 2)", roles[0] == "Cat" and roles.count("Mouse") == 2, str(roles))
    if roles[0] != "Cat":
        return summary(aborted=True)

    mouse = p[1]

    # ---------- 봉인 ----------
    alcove_hp = cat.invoke("TestGetDogAlcoveHp")
    check("강아지 벽감이 맵에 하나 서 있다", alcove_hp > 0, f"hp={alcove_hp}")
    # 기회비용이 시간 게이트를 대신한다 — 치즈 벽(120)보다 확실히 단단해야 한다
    check("강아지 벽감이 치즈 벽감보다 단단하다", alcove_hp > 240, f"hp={alcove_hp} (치즈 벽감 120)")

    check("풀기 전에는 강아지가 없다", cat.invoke("TestGetDogCount") == 0)

    # ---------- 개방 ----------
    cat.invoke("TestBreakDogAlcove")
    time.sleep(2.0)
    spawned = cat.invoke("TestGetDogCount")
    check("벽을 부수면 강아지가 나온다", spawned == 1, f"{spawned}마리")
    if spawned != 1:
        return summary(aborted=True)

    dog_hp_full = cat.invoke("TestGetDogHp")
    check("강아지가 HP를 갖고 시작한다", dog_hp_full > 0, f"hp={dog_hp_full}")

    # 클론(쥐)도 같은 강아지를 본다 — 복제가 됐다는 뜻
    check("쥐 클라에도 강아지가 보인다", mouse.invoke("TestGetDogCount") == 1,
          f"{mouse.invoke('TestGetDogCount')}마리")

    # ---------- 길찾기 ----------
    # 길찾기는 구운 폴리곤이 한다(2026-08-12). 0이면 통째로 죽은 것이고,
    # 그러면 강아지가 코앞 목표를 두고 한 발도 못 뗀다.
    polys = cat.invoke("TestGetNavMeshPolygons")
    check("길찾기 — 이동 폴리곤이 구워졌다", polys > 0, f"{polys}개")

    # 재는 동안 고양이는 멀리 빼 둔다. 옆에 두면 12초 사이에 물려 죽고, 그러면 판이 끝나면서
    # 강아지까지 사라져 "0m 이동"으로 읽힌다(2026-08-10에 실제로 그랬다).
    cat.invoke("TestTeleportLocalPlayer", _x=-13.0, _z=-13.0)
    time.sleep(1.0)

    # 실제로 맵을 가로지르는가. Model 값이 아니라 **좌표가 얼마나 움직였나**를 센다 —
    # 상태만 보면 'Chase'인데 제자리인 경우를 못 잡는다.
    def dog_xz():
        raw = cat.invoke("TestGetDogPos")
        if not raw:
            return None
        x, z = raw.split(",")
        return float(x), float(z)

    start = dog_xz()
    travelled = 0.0
    prev = start
    for _ in range(12):
        time.sleep(1.0)
        now = dog_xz()
        if prev and now:
            travelled += ((now[0] - prev[0]) ** 2 + (now[1] - prev[1]) ** 2) ** 0.5
        prev = now
    check("길찾기 — 강아지가 맵을 실제로 가로지른다", travelled > 12.0,
          f"12초 동안 {travelled:.1f}m 이동")

    # ---------- 추격 ----------
    # 고양이를 강아지 근처로 데려간 뒤 거리가 줄어드는지 본다.
    cat.invoke("TestTeleportToDog")
    time.sleep(0.6)
    first = cat.invoke("TestGetDogDistance")
    time.sleep(2.5)
    later = cat.invoke("TestGetDogDistance")
    # 물기 사거리(2m) 안까지 붙으면 더 안 줄어든다 — 둘 중 하나면 통과
    check("강아지가 다가온다(또는 이미 붙어 있다)", later <= first + 0.5 or later <= 2.5,
          f"{first:.1f}m → {later:.1f}m")

    # ---------- 물기 ----------
    # **쥐 둘을 멀리 치운다.** 옆에 있으면 강아지는 쥐를 물고 있고(어그로는 보이는 동안 안 놓는다),
    # 덮치기도 쥐에게 맞아 장난기가 오른다 — 그러면 아래 두 항목이 강아지와 무관해진다.
    p[1].invoke("TestTeleportLocalPlayer", _x=-13.5, _z=13.5)
    p[2].invoke("TestTeleportLocalPlayer", _x=-12.0, _z=13.5)
    time.sleep(6.0)   # 어그로 해제(DogAggroDropSeconds=4)보다 넉넉히

    hp_before = cat.invoke("TestGetLocalHp")
    for _ in range(10):
        cat.invoke("TestTeleportToDog")
        time.sleep(1.5)
        if cat.invoke("TestGetLocalHp") < hp_before:
            break
    hp_after = cat.invoke("TestGetLocalHp")
    check("강아지가 고양이를 문다", hp_after < hp_before, f"{hp_before} → {hp_after}")

    # **푼 쪽도 문다**가 이 기능의 전부다 — 고양이가 부쉈는데 고양이가 맞았다.
    check("푼 쪽도 예외가 아니다", hp_after < hp_before, "부순 고양이가 물렸다")

    # ---------- 장난기 면제 ----------
    fun_before = cat.invoke("TestGetCheese")
    cat.invoke("TestTeleportToDog")
    time.sleep(0.4)
    for _ in range(3):
        cat.invoke("TestPounce")
        time.sleep(2.6)
    fun_after = cat.invoke("TestGetCheese")
    check("강아지를 때려도 장난기가 안 오른다", fun_after <= fun_before,
          f"{fun_before} → {fun_after}")

    # 덮치기가 실제로 닿았는지 확인 — 안 닿았으면 위 항목이 공허하다
    dog_hp_now = cat.invoke("TestGetDogHp")
    check("덮치기가 강아지에게 닿았다", dog_hp_now < dog_hp_full, f"{dog_hp_full} → {dog_hp_now}")

    # ---------- 퇴장 ----------
    cat.invoke("TestDamageDog", _damage=99999)
    time.sleep(1.5)
    check("쓰러뜨리면 사라진다", cat.invoke("TestGetDogCount") == 0,
          f"{cat.invoke('TestGetDogCount')}마리")
    check("쥐 클라에서도 사라진다", mouse.invoke("TestGetDogCount") == 0)

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
