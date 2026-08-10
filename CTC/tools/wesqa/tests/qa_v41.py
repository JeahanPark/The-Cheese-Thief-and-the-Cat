# -*- coding: utf-8 -*-
"""v3.9 상자 + 쥐끼리 공격 검증 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 클론 둘=쥐** — 상자를 부수고 총량을 읽는 쪽이 서버여야 한다.

한 판에 한 번만 돌린다. 다시 돌리려면 플레이모드를 껐다 켠다.
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

    # ---------- 배치 ----------
    boxes = cat.invoke("TestGetBoxCount")
    check("상자 — 매치 시작에 맵이 깔아 둔다", boxes > 0, f"{boxes}개")

    # ---------- 총량 보존 ----------
    # 상자가 품은 치즈도 4900 안에서 뗀다. 안 떼면 총량이 늘어 고갈이 늦어진다.
    box_total = cat.invoke("TestGetBoxCheeseTotal")
    alcove_walls = cat.invoke("TestGetCheeseAlcoveCount")
    per_wall = cat.invoke("TestGetAlcoveCheesePerWall")
    reserve = cat.invoke("TestGetTotalCheeseReserve")
    taken = reserve + per_wall * alcove_walls + box_total

    check("총량 — 상자도 치즈를 품는다", box_total > 0, f"상자 합계 {box_total}")
    check("총량 — 지점 + 벽감 + 상자가 4900을 넘지 않는다", taken <= 4900,
          f"{reserve} + {per_wall * alcove_walls} + {box_total} = {taken}")
    check("총량 — 새는 치즈가 없다(나눗셈 나머지뿐)", 4900 - taken < 10,
          f"빈 몫 {4900 - taken}")

    # ---------- 부수면 나온다 ----------
    before_drops = cat.invoke("TestGetCheeseDropCount")
    pos = cat.invoke("TestBreakFirstBox")
    time.sleep(1.2)
    after_drops = cat.invoke("TestGetCheeseDropCount")

    check("상자 — 부수면 자리에서 사라진다", cat.invoke("TestGetBoxCount") == boxes - 1,
          f"{cat.invoke('TestGetBoxCount')}개 남음")
    check("상자 — 부수면 치즈가 흩어져 나온다", after_drops > before_drops,
          f"덩이 {before_drops} → {after_drops} (자리 {pos})")

    # ---------- 장난기 면제 ----------
    # 상자도 맵에 원래 있던 물건이라 때려도 고양이 수입이 늘면 안 된다.
    fun_before = cat.invoke("TestGetCheese")
    cat.invoke("TestBreakFirstBox")
    time.sleep(0.8)
    check("장난기 — 상자를 부숴도 고양이 수입이 안 는다",
          cat.invoke("TestGetCheese") <= fun_before,
          f"{fun_before} → {cat.invoke('TestGetCheese')}")

    # ---------- 쥐끼리 공격 ----------
    m1_id = int(m1.invoke("TestGetLocalClientId"))
    m2.invoke("TestTeleportToOtherPlayer", _clientId=m1_id)
    time.sleep(0.8)
    hp_before = m1.invoke("TestGetLocalHp")
    for _ in range(6):
        m2.invoke("TestShootAtCat")   # 이제 '제 진영만 아니면' 쏘므로 다른 쥐도 표적이다
        time.sleep(0.9)
    hp_after = m1.invoke("TestGetLocalHp")
    check("쥐끼리 — 쥐가 다른 쥐를 맞힐 수 있다", hp_after < hp_before,
          f"{hp_before} → {hp_after}")

    # ---------- 제 구조물은 못 친다 ----------
    # 요새·창고가 RoleTag.Mouse라 안 가르면 쥐가 제 요새를 쏜다.
    # **서버 전용 훅이다.** 클론에서 부르면 조용히 무시된다(경고는 GameDebug라 에디터에서 안 뜬다).
    cat.invoke("TestGrantCheeseAll")
    time.sleep(0.5)
    # 자리는 치즈 지점 옆으로 잡는다 — 임의 좌표(0,0)는 벽 속일 수 있어 설치가 조용히 실패한다
    sx, sz = [float(v) for v in m1.invoke("TestGetBestCheeseSpotPos").split(",")]
    # 설치는 **제 몸 근처에만** 된다(설치 사거리) — 먼저 그 자리로 가야 한다
    m1.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(0.8)
    m1.invoke("TestPlaceWarehouseAt", _x=sx + 2.0, _z=sz)
    time.sleep(1.2)

    struct_before = cat.invoke("TestGetLowestStructureHp")
    if struct_before > 0:
        m2.invoke("TestTeleportLocalPlayer", _x=sx + 2.0, _z=sz + 2.0)
        time.sleep(0.6)
        for _ in range(5):
            m2.invoke("TestShootAtCat")
            time.sleep(0.9)
        struct_after = cat.invoke("TestGetLowestStructureHp")
        check("쥐끼리 — 쥐의 구조물은 못 친다", struct_after >= struct_before,
              f"구조물 최저 HP {struct_before} → {struct_after}")
    else:
        check("쥐끼리 — 쥐의 구조물은 못 친다", False, "구조물이 없어 검증 불가")


    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + (f"  | {detail}" if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    print(f"{passed}/{len(RESULTS)}")


main()
