# -*- coding: utf-8 -*-
"""v3.8 벽감 + 즉시 획득 치즈 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
호스트=고양이여야 '벽감을 때려도 장난기가 안 는다'를 볼 수 있다(장난기는 고양이 지갑).
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 14      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다
                   # 「치즈 — 쥐가 닿으면 먹는다」(벽감 위치를 못 받은 경우)와
                   # 「치즈 — 쥐가 닿으면 지갑이 는다」는 **둘 중 하나만** 돈다.
                   # 호출부 수(15)가 아니라 한 판에 실제로 서는 항목 수를 적는다.


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
    cat = roles.index("Cat") if "Cat" in roles else None
    mice = [i for i in range(3) if i != cat]
    check("역할 확인 (호스트=고양이)", cat == 0 and len(mice) == 2, str(roles))
    if cat is None:
        return summary(aborted=True)

    host = p[0]

    # ---------- 배치 ----------
    count = host.invoke("TestGetAlcoveCount")
    # 하한 4 — 강아지 1 + 직업권 2를 빼고도 치즈 벽이 하나는 남아야 한다(2026-08-11)
    # 개수는 맵마다 다르다(격자 맵 4·창고 6, 2026-09-07) — 하한 4·상한 8(AlcoveCountFor)만 본다
    check("벽감 — 매치 시작에 맵이 깔아 둔다", 4 <= count <= 8, f"{count}개 (기대 4~8)")

    # ---------- 총량 보존 ----------
    # 벽 몫은 총량 4900 '안에서' 뗀다 — 더하면 고갈이 늦어져 성장 기한이 무너진다.
    #
    # **치즈 벽감만 센다**(v3.8 5번). 벽감 하나는 강아지가 들어 있어 치즈를 안 준다 —
    # 그 몫까지 떼면 아무도 못 받는 치즈가 생겨 총량이 샌다.
    per_wall = host.invoke("TestGetAlcoveCheesePerWall")
    cheese_walls = host.invoke("TestGetCheeseAlcoveCount")
    # 벽마다 내용물이 다르다(직업권 벽은 치즈 0) — 개수 × 고정액이 아니라 합으로 잰다
    wall_share = host.invoke("TestGetAlcoveCheeseTotal")
    # 지금 남은 양이 아니라 **판 시작에 받은** 매장량을 쓴다 — 쥐가 캐는 동안 줄어든다
    reserve = host.invoke("TestGetDesignedSpotReserve")
    box_total = host.invoke("TestGetBoxCheeseTotal")
    # 총량은 2026-08-10부터 고정값(4900)이 아니라 쥐 수 비례다 — 서버에게 묻는다
    total = host.invoke("TestGetMapTotalCheese")
    check("총량 — 벽감 하나는 강아지라 상자가 없다",
          0 < cheese_walls < count, f"상자 벽감 {cheese_walls} / 전체 {count}")
    # 지점합 + 벽 몫 + 상자 몫이 총량을 넘지 않고, 나눈셈 나머지(지점 수 미만)만 비어야 한다
    taken = reserve + wall_share + box_total
    check("총량 — 지점 + 벽감 + 상자가 총량을 안 넘는다",
          taken <= total, f"{reserve} + {wall_share} + {box_total} = {taken} / {total}")
    check("총량 — 새는 치즈가 없다(나머지뿐)",
          total - taken < 20, f"빈 몫 {total - taken}")

    # ---------- 장난기 면제 ----------
    # Health만 붙이면 !IsPlayerObject라 자동으로 구조물 취급 → 장난기가 붙는다.
    # v3.5의 '벽 때리기가 최고 효율' 실패를 되살리지 않는지 본다.
    fun_before = host.invoke("TestGetCheese")
    dealt = host.invoke("TestHitFirstAlcove", _damage=40)
    time.sleep(0.6)
    fun_after = host.invoke("TestGetCheese")
    check("장난기 — 벽감을 때려도 고양이 수입이 늘지 않는다",
          dealt > 0 and fun_after == fun_before,
          f"입힌 피해 {dealt} · 장난기 {fun_before} → {fun_after}")

    # ---------- 벽을 부수면 상자가 드러난다 (2026-08-11) ----------
    # **벽은 보상을 품지 않는다.** 부수면 뒤에 있던 상자가 나오고, 그걸 또 부숴야 치즈가 나온다.
    # 한 번에 쏟아지면 부순 사람이 소리를 다 내고도 늦게 온 사람에게 뺏긴다.
    boxes_before = host.invoke("TestGetBoxCount")
    drops_before = host.invoke("TestGetCheeseDropCount")
    pos = host.invoke("TestBreakFirstAlcove")
    time.sleep(1.2)

    check("벽감 — 부수면 상자가 드러난다",
          host.invoke("TestGetBoxCount") == boxes_before + 1,
          f"상자 {boxes_before} → {host.invoke('TestGetBoxCount')}")
    check("벽감 — 벽을 열기만 해서는 치즈가 안 나온다",
          host.invoke("TestGetCheeseDropCount") == drops_before,
          f"덩이 {drops_before} → {host.invoke('TestGetCheeseDropCount')}")
    check("벽감 — 부서진 자리에서 사라진다",
          host.invoke("TestGetAlcoveCount") == count - 1,
          f"{host.invoke('TestGetAlcoveCount')}개 남음")

    host.invoke("TestBreakFirstBox")
    time.sleep(1.2)
    drops_after = host.invoke("TestGetCheeseDropCount")
    dropped = drops_after - drops_before
    check("상자 — 그걸 부숴야 치즈가 여러 덩이로 흩어진다", dropped == 3, f"{dropped}덩이 (기대 3)")

    total = host.invoke("TestGetCheeseDropTotal")
    check("치즈 — 덩이 합계가 상자가 품은 값과 같다(나머지를 안 버린다)",
          total == per_wall, f"합계 {total} (기대 {per_wall})")

    # ---------- 쥐가 먹는다 ----------
    if not pos:
        check("치즈 — 쥐가 닿으면 먹는다", False, "벽감 위치를 못 받음")
        return summary()

    dx, dz = [float(v) for v in pos.split(",")]
    mouse = p[mice[0]]
    before = mouse.invoke("TestGetCheese")
    # 흩어진 반경 1.5m — 한가운데로 가면 하나는 확실히 닿는다
    mouse.invoke("TestTeleportLocalPlayer", _x=dx + 1.5, _z=dz)
    time.sleep(1.0)
    mouse.invoke("TestWalkDirection", _x=-1, _z=0, _seconds=0.8)
    time.sleep(1.6)
    after = mouse.invoke("TestGetCheese")
    check("치즈 — 쥐가 닿으면 지갑이 는다", after > before, f"{before} → {after}")

    check("치즈 — 먹은 덩이는 사라진다",
          host.invoke("TestGetCheeseDropCount") < drops_after,
          f"{drops_after} → {host.invoke('TestGetCheeseDropCount')}")

    # ---------- 고양이는 못 먹는다 ----------
    # 고양이 지갑은 장난기다 — 여기에 치즈를 넣으면 '벽 부수기 = 고양이 수입'이 되어
    # 장난기 면제 결정과 정면으로 모순된다.
    remaining = host.invoke("TestGetCheeseDropCount")
    if remaining > 0:
        fun_before2 = host.invoke("TestGetCheese")
        host.invoke("TestTeleportLocalPlayer", _x=dx + 1.5, _z=dz)
        time.sleep(1.0)
        host.invoke("TestWalkDirection", _x=-1, _z=0, _seconds=0.8)
        time.sleep(1.6)
        check("치즈 — 고양이는 밟아도 못 먹는다",
              host.invoke("TestGetCheese") == fun_before2
              and host.invoke("TestGetCheeseDropCount") == remaining,
              f"장난기 {fun_before2} → {host.invoke('TestGetCheese')} · 덩이 {remaining} → {host.invoke('TestGetCheeseDropCount')}")
    else:
        # 남은 덩이가 없으면 「고양이가 밟아도 안 먹는다」를 못 잰다 — 결과에 남긴다(감사 E §2-4).
        skip("치즈 — 고양이는 밟아도 못 먹는다", "바닥에 남은 덩이가 0 — 앞의 쥐가 다 먹었다")

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
