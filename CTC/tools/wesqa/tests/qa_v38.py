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


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def main():
    p = [WesPoco(instance=i) for i in range(3)]

    roles = [x.invoke("TestGetRole") for x in p]
    cat = roles.index("Cat") if "Cat" in roles else None
    mice = [i for i in range(3) if i != cat]
    check("역할 확인 (호스트=고양이)", cat == 0 and len(mice) == 2, str(roles))
    if cat is None:
        return summary()

    host = p[0]

    # ---------- 배치 ----------
    count = host.invoke("TestGetAlcoveCount")
    check("벽감 — 매치 시작에 맵이 깔아 둔다", count == 3, f"{count}개 (기대 3)")

    # ---------- 총량 보존 ----------
    # 벽 몫은 총량 4900 '안에서' 뗀다 — 더하면 고갈이 늦어져 성장 기한이 무너진다.
    #
    # **치즈 벽감만 센다**(v3.8 5번). 벽감 하나는 강아지가 들어 있어 치즈를 안 준다 —
    # 그 몫까지 떼면 아무도 못 받는 치즈가 생겨 총량이 샌다.
    per_wall = host.invoke("TestGetAlcoveCheesePerWall")
    cheese_walls = host.invoke("TestGetCheeseAlcoveCount")
    # 지금 남은 양이 아니라 **판 시작에 받은** 매장량을 쓴다 — 쥐가 캐는 동안 줄어든다
    reserve = host.invoke("TestGetDesignedSpotReserve")
    box_total = host.invoke("TestGetBoxCheeseTotal")
    # 총량은 2026-08-10부터 고정값(4900)이 아니라 쥐 수 비례다 — 서버에게 묻는다
    total = host.invoke("TestGetMapTotalCheese")
    check("총량 — 벽감 하나는 강아지 몫이라 치즈를 안 준다",
          0 < cheese_walls < count, f"치즈 벽감 {cheese_walls} / 전체 {count}")

    wall_share = per_wall * cheese_walls
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

    # ---------- 부수면 나온다 ----------
    drops_before = host.invoke("TestGetCheeseDropCount")
    pos = host.invoke("TestBreakFirstAlcove")
    time.sleep(1.2)
    drops_after = host.invoke("TestGetCheeseDropCount")
    dropped = drops_after - drops_before
    check("벽감 — 부수면 치즈가 여러 덩이로 흩어진다", dropped == 3, f"{dropped}덩이 (기대 3)")

    check("벽감 — 부서진 자리에서 사라진다",
          host.invoke("TestGetAlcoveCount") == count - 1,
          f"{host.invoke('TestGetAlcoveCount')}개 남음")

    total = host.invoke("TestGetCheeseDropTotal")
    check("치즈 — 덩이 합계가 벽감이 품은 값과 같다(나머지를 안 버린다)",
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
