# -*- coding: utf-8 -*-
"""2:8 기준 밸런스 검증 — 120×120 대형 맵 (MPPM 3인).

**로비부터 직접 몬다.** 고양이 정원이 2가 됐는지를 재려면 역할 선택 자체가 검증 대상이라,
"매치 시작 완료"를 전제로 두면 그 항목을 못 잰다.

구성: 호스트=고양이 · 클론1=고양이 · 클론2=쥐 (정원 2 확인 + 쥐 1로 총량 산수 확인)
전제: 메인 에디터가 플레이모드, 클론 2개 실행 중.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

LARGE_MAP_INDEX = 2          # MapLayoutPaths 순서: 기본 · 확장 · 대형
CHEESE_PER_MOUSE = 1400      # BalanceInfo.csv
RESULTS = []
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 19      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다
                   # 「매치 시작」(로비 진행 실패)은 성공 경로에서는 안 도는 중단 항목이라
                   # 분모에 넣지 않는다. 호출부 수(20)가 아니라 한 판에 서는 항목 수다.


def check(name, ok, detail=""):
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


def parse_shape(text):
    out = {}
    for part in (text or "").split(";"):
        if "=" not in part:
            continue
        k, v = part.split("=", 1)
        out[k] = float(v)
    return out


def walk_and_measure(walker, listener, walker_id, dx, dz):
    """걷는 동안 여러 번 재서 최댓값을 쓴다.

    한 번만 재면 벽에 막혀 멈춘 순간을 집어 -1(무음)이 나온다 —
    120×120 맵이라도 방 하나는 10m 남짓이라 1초면 벽에 닿는다(2026-08-10 실측).
    """
    best = -1.0
    for _ in range(3):
        walker.invoke("TestWalkDirection", _x=dx, _z=dz, _seconds=1.2)
        for _ in range(4):
            time.sleep(0.25)
            best = max(best, float(listener.invoke("TestGetFootstepRange", _clientId=walker_id)))
        dx, dz = -dx, -dz   # 막혔으면 반대로 — 어느 쪽이든 벽 없는 방향이 하나는 있다
    return best


def setup(p):
    """로비를 몰아 2:1 매치를 연다. 이미 시작돼 있으면 그대로 쓴다."""
    if p[0].invoke("TestGetRole") in ("Cat", "Mouse"):
        return True

    p[0].invoke("TestStartHost")
    time.sleep(4.0)

    # 클론은 MppmAutoConnect가 붙이고 쥐를 자동 선택한다 — 하나만 고양이로 돌린다
    p[0].invoke("TestSelectMap", _index=LARGE_MAP_INDEX)
    time.sleep(0.8)
    p[0].invoke("TestSelectRoleCat")
    p[1].invoke("TestSelectRoleCat")
    p[2].invoke("TestSelectRoleMouse")
    time.sleep(1.5)

    p[0].invoke("TestStartGame")
    time.sleep(4.0)
    return p[0].invoke("TestGetRole") in ("Cat", "Mouse")


def main():
    p = [WesPoco(instance=i) for i in range(3)]

    if not setup(p):
        check("매치 시작", False, "로비 진행 실패 — 클론 접속 확인")
        return summary(aborted=True)

    roles = [x.invoke("TestGetRole") for x in p]
    cat, cat2, mouse = p[0], p[1], p[2]

    # ---------- 인원 ----------
    check("정원 — 고양이가 둘이 될 수 있다", roles.count("Cat") == 2, str(roles))
    check("정원 — 코드 상수도 2다", cat.invoke("TestGetCatCapacity") == 2)
    check("인원 — 쥐가 하나다(총량 산수의 분모)", cat.invoke("TestGetMouseCount") == 1,
          str(cat.invoke("TestGetMouseCount")))

    # ---------- 맵 규모 ----------
    shape = parse_shape(cat.invoke("TestGetMapShape"))
    check("맵 — 120×120이다", shape.get("half") == 60, f"half={shape.get('half')}")
    # 대형 맵은 **손 배치**(Layout_Warehouse01)로 바뀌었다 — 절차 생성 방이 없어 rooms=0이다.
    # 예전 절차 맵의 49개를 그대로 두면 「방이 사라졌다」가 아니라 「맵이 바뀌었다」를 놓친다.
    check("맵 — 손 배치 창고라 절차 방이 없다", shape.get("rooms") == 0, f"rooms={shape.get('rooms')}")
    check("맵 — 치즈 지점 14개(등급 2벌)", shape.get("cheese") == 14, f"cheese={shape.get('cheese')}")
    check("맵 — 요새 8개(쥐 최대 8)", shape.get("fort") == 8, f"fort={shape.get('fort')}")

    # 레이아웃에만 있고 씬에 안 서면 배선 사고다 — 실제 오브젝트를 센다
    spots = cat.invoke("TestGetCheeseSpotObjectCount")
    check("맵 — 치즈 지점이 실제로 씬에 섰다", spots == 14, f"{spots}개")

    alcoves = cat.invoke("TestGetAlcoveCount")
    boxes = cat.invoke("TestGetBoxCount")
    check("맵 — 벽감이 방 수를 따라 늘었다(6개)", alcoves == 6, f"{alcoves}개")
    check("맵 — 상자는 바닥에 없다(전부 벽 뒤)", boxes == 0, f"{boxes}개")

    # ---------- 치즈 총량이 인원에 비례한다 ----------
    # 단, 맵의 권장 최소 인원이 하한이다 — 벽감·상자에 이미 깔린 치즈는 절대량이라
    # 인원이 적다고 총량을 줄이면 "깔린 치즈 > 총량"이 되어 지점이 통째로 0이 된다(2026-08-10).
    mice = int(cat.invoke("TestGetMouseCount"))
    floor = int(shape.get("micemin", 1))
    expected = CHEESE_PER_MOUSE * max(mice, floor)
    total = cat.invoke("TestGetMapTotalCheese")
    check(f"총량 — 쥐 1인당 1400 (하한 {floor}인)", total == expected, f"{total} (기대 {expected})")

    # 판 시작에 받은 매장량 — 남은 양으로 재면 파밍 속도에 따라 실패한다
    reserve = cat.invoke("TestGetDesignedSpotReserve")
    # 벽마다 내용물이 다르다(직업권 벽은 치즈 0) — 개수 × 고정액이 아니라 합으로 잰다
    wall = cat.invoke("TestGetAlcoveCheeseTotal")
    box_total = cat.invoke("TestGetBoxCheeseTotal")
    check("총량 — 지점 + 벽감 + 상자가 총량을 안 넘는다",
          reserve + wall + box_total <= total,
          f"{reserve} + {wall} + {box_total} = {reserve + wall + box_total} / {total}")
    check("총량 — 새는 치즈가 없다(지점 나눗셈 나머지뿐)",
          total - (reserve + wall + box_total) < 14,
          f"빈 몫 {total - (reserve + wall + box_total)}")

    # ---------- 발소리: 잠행은 무음이 아니라 '좁게' ----------
    mouse_id = int(mouse.invoke("TestGetLocalClientId"))
    cat_id = int(cat.invoke("TestGetLocalClientId"))

    mouse.invoke("TestSetSneak", _on=False)
    walk_range = walk_and_measure(mouse, cat, mouse_id, 1.0, 0.0)
    # 18 → 14 (2026-08-12): 뛰는 발소리가 고양이의 공짜 추적기였다 — 시야를 정직하게
    # 만들고 재니 들킨 쥐가 대시·미끼·시선 끊기로도 못 벗어났다(봇 실측 12판 전패).
    # 14 → 22 (`5e308d3` 대형 120×120 준선형 상향): 맵이 4배 넓어져 14m는 다시 무음에
    # 가까워졌다. 값의 주인은 `PlayerInfo.csv`(Mouse.FootstepRangeMeters)다.
    check("발소리 — 뛰면 22m까지 간다(CSV Mouse.FootstepRangeMeters)",
          abs(walk_range - 22.0) < 0.6, f"{walk_range}")

    mouse.invoke("TestSetSneak", _on=True)
    sneak_range = walk_and_measure(mouse, cat, mouse_id, -1.0, 0.0)
    # 여기가 이 판의 핵심이다 — -1(무음)이면 넓은 맵에서 고양이가 이길 길이 없다
    check("발소리 — 웅크려도 소리가 난다(무음 아님)", sneak_range > 0, f"{sneak_range}")
    # 4 → 6 (`5e308d3` 같은 상향). 값의 주인은 `PlayerInfo.csv`(Mouse.SneakFootstepRangeMeters)다.
    check("발소리 — 웅크리면 6m로 좁아진다(CSV Mouse.SneakFootstepRangeMeters)",
          abs(sneak_range - 6.0) < 0.6, f"{sneak_range}")
    mouse.invoke("TestSetSneak", _on=False)
    time.sleep(0.5)

    # ---------- 이속 ----------
    cat_speed = cat.invoke("TestGetMoveSpeed")
    mouse_speed = mouse.invoke("TestGetMoveSpeed")
    # 8.6 → 7.4 (`2f66cb5`): 쥐 승률 0%의 범인이 추격이었다. 1.30배에서 1.12배로 내리니
    # 분당 처치 2.76 → 1.44, 쥐 승률 0% → 20%로 게이트 하단에 닿았다. 기획서는 아직 8.6이라
    # 적혀 있지만 **지금 도는 값의 주인은 `PlayerInfo.csv`**이고, EditMode
    # `MoveSpeed_FastFeetLv3_ApproachesCat`도 7.4를 전제로 서 있다.
    check("이속 — 고양이 7.4 (CSV Cat.MoveSpeed)", abs(cat_speed - 7.4) < 0.05, f"{cat_speed}")
    check("이속 — 고양이가 쥐보다 1.12배 빠르다(1.30배는 쥐 승률 0%였다)",
          1.10 < cat_speed / mouse_speed < 1.15,
          f"{cat_speed} / {mouse_speed} = {cat_speed / mouse_speed:.2f}")

    # 고양이 발소리는 쥐보다 짧아야 한다 — '쥐가 항상 먼저 안다'를 뒤집은 부분
    cat_range = walk_and_measure(cat, mouse, cat_id, 1.0, 0.0)
    check("발소리 — 고양이(20m)가 쥐(22m)보다 짧다", 0 < cat_range < walk_range,
          f"고양이 {cat_range} vs 쥐 {walk_range}")

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
