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
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 13      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


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


def warp_to_shop(host, player, client_id):
    """가장 가까운 상점 자리로 옮긴다 — qa_v36과 같은 규약(테크 v3, 2026-09-07).

    `TestWarpToNearestShop`은 `BodyPush.ServerPush` 경유라 **호스트 인스턴스**에서 불러야 하고,
    그 안에서 다시 소유자 RPC를 타므로 클론의 몸은 왕복이 끝나야 실제로 움직인다 — 쉬었다 확인한다.
    """
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.0)
    return bool(player.invoke("TestIsInShop", _clientId=client_id))


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    cat, m1, m2 = p[0], p[1], p[2]
    roles = [x.invoke("TestGetRole") for x in p]
    check("역할 확인 (호스트=고양이 · 쥐 2)", roles[0] == "Cat" and roles.count("Mouse") == 2, str(roles))
    if roles[0] != "Cat":
        return summary(aborted=True)

    # ---------- 배치 ----------
    # 상자는 **바닥에 없다**(2026-08-11). 전부 벽 뒤에 있고, 벽을 부숴야 드러난다 —
    # 직업권이 바닥에 굴러다니면 판을 뒤집는 물건이 아무 소리 없이 손에 들어간다.
    check("상자 — 바닥에는 하나도 없다", cat.invoke("TestGetBoxCount") == 0,
          f"{cat.invoke('TestGetBoxCount')}개")
    cat.invoke("TestBreakFirstAlcove")
    time.sleep(1.2)
    boxes = cat.invoke("TestGetBoxCount")
    check("상자 — 벽을 부숴야 나온다", boxes == 1, f"{boxes}개")

    # ---------- 총량 보존 ----------
    # 상자가 품은 치즈도 총량 안에서 뗀다. 안 떼면 총량이 늘어 고갈이 늦어진다.
    # 총량은 2026-08-10부터 고정값(4900)이 아니라 쥐 수 비례라 서버에게 묻는다.
    box_total = cat.invoke("TestGetBoxCheeseTotal")          # 바닥 상자 — 이제 0이다
    wall_share = cat.invoke("TestGetAlcoveCheeseTotal")      # 벽 뒤 상자들의 치즈 합
    # 판 시작에 받은 매장량(남은 양이 아니다) — 보존은 설계값으로 재는 게 맞다
    reserve = cat.invoke("TestGetDesignedSpotReserve")
    total = cat.invoke("TestGetMapTotalCheese")
    taken = reserve + wall_share + box_total
    check("총량 — 지점 + 벽 뒤 상자가 총량을 넘지 않는다", taken <= total,
          f"{reserve} + {wall_share} + {box_total} = {taken} / {total}")
    check("총량 — 새는 치즈가 없다(나눗셈 나머지뿐)", total - taken < 20,
          f"빈 몫 {total - taken}")

    # ---------- 부수면 나온다 ----------
    before_drops = cat.invoke("TestGetCheeseDropCount")
    pos = cat.invoke("TestBreakFirstBox")
    time.sleep(1.2)
    after_drops = cat.invoke("TestGetCheeseDropCount")

    check("상자 — 부수면 자리에서 사라진다", cat.invoke("TestGetBoxCount") == boxes - 1,
          f"{cat.invoke('TestGetBoxCount')}개 남음")
    # 2026-09-08: 여기 `or True`가 박혀 있어 **절대 실패할 수 없었다**(감사 E F-4). 뺐다.
    # 상자 안에 든 것이 치즈가 아니라 직업권일 수도 있으므로 덩이 수만으로는 못 잰다 —
    # 덩이가 늘거나(치즈) 상자 자리에 무언가 떨어졌으면 통과. 둘 다 아니면 빨갛다.
    check("상자 — 부수면 안의 것이 나온다", after_drops > before_drops,
          f"덩이 {before_drops} → {after_drops} (자리 {pos})")

    # ---------- 장난기 면제 ----------
    # 상자도 맵에 원래 있던 물건이라 때려도 고양이 수입이 늘면 안 된다.
    fun_before = cat.invoke("TestGetCheese")
    cat.invoke("TestBreakFirstAlcove")
    time.sleep(1.2)
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
    # 테크 v3(2026-09-07): 창고 값이 둘로 갈렸다 — 설치권은 **상점 자리 안에서** 한 번,
    # 설치비는 놓을 때마다. 치즈 지점에 선 채로 사면 서버가 NotInShop으로 거절한다.
    # 숫자는 훅으로 읽는다(CSV가 원본).
    wh_right_cost = cat.invoke("TestGetTechCost", _techId="Warehouse", _level=1)
    wh_place_cost = cat.invoke("TestGetPlaceCost", _techId="Warehouse")
    in_shop = warp_to_shop(cat, m1, m1_id)
    check("창고 — 설치권을 사러 상점 자리에 들어갔다", in_shop, f"inShop={in_shop}")
    m1.invoke("TestBuyTechLocal", _techId="Warehouse")
    time.sleep(1.2)
    right_lv = m1.invoke("TestGetTechLevel", _clientId=m1_id, _techId="Warehouse")
    check(f"창고 — 상점에서 설치권 구매(비용 {wh_right_cost})", right_lv == 1, f"lv={right_lv}")

    # 설치는 **제 몸 근처에만** 된다(설치 사거리) — 자리로 돌아가 놓는다. 설치비만 더 나간다
    m1.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(0.8)
    place_before = m1.invoke("TestGetCheese")
    m1.invoke("TestPlaceWarehouseAt", _x=sx + 2.0, _z=sz)
    time.sleep(1.5)
    place_spent = place_before - m1.invoke("TestGetCheese")
    # **정확한 액수는 여기서 못 잰다** — 쥐가 치즈 지점 위에 서 있어 같은 창에 채굴 수입이 들어온다
    # (실측 100 나가고 11 들어와 89로 읽혔다). 설치비 정확값은 qa_v46 ⑤가 깨끗한 자리에서 잰다.
    check(f"창고 — 설치비가 따로 나갔다(정가 {wh_place_cost}, 채굴 수입만큼 상쇄)",
          0 < place_spent <= wh_place_cost, f"{place_spent} 나감")

    # 항목은 **하나다.** 예전엔 if/else가 각각 check를 불러 정적 항목 수(14)와
    # 실제 실행 수(13)가 어긋났다 — 분모가 실행마다 달라지는 부류(감사 E §2-4).
    struct_before = cat.invoke("TestGetLowestStructureHp")
    struct_after = -1
    if struct_before > 0:
        m2.invoke("TestTeleportLocalPlayer", _x=sx + 2.0, _z=sz + 2.0)
        time.sleep(0.6)
        for _ in range(5):
            m2.invoke("TestShootAtCat")
            time.sleep(0.9)
        struct_after = cat.invoke("TestGetLowestStructureHp")
    check("쥐끼리 — 쥐의 구조물은 못 친다",
          struct_before > 0 and struct_after >= struct_before,
          f"구조물 최저 HP {struct_before} → {struct_after}"
          if struct_before > 0 else "구조물이 없어 검증 불가 — 앞의 창고 설치가 실패했다")


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
