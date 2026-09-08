# -*- coding: utf-8 -*-
"""v3.6 종합 검증 — MPPM 3인(메인=고양이, 클론2=쥐)을 wesqa로 조작한다.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 24      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


def balance_int(column):
    """BalanceInfo.csv에서 정수 한 칸 — 헤더는 '이름.타입' 형식."""
    import csv
    path = "c:/GitFork/The-Cheese-Thief-and-the-Cat/CTC/Assets/Resources/CSVInfo/BalanceInfo.csv"
    with open(path, encoding="utf-8-sig") as f:
        rows = list(csv.reader(f))
    names = [h.split(".")[0] for h in rows[0]]
    return int(float(rows[1][names.index(column)]))


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


def face_and_pounce(cat, tx, tz, cx, cz):
    """고양이를 (cx,cz)에 세우고 (tx,tz)를 바라보게 한 뒤 덮친다.
    몸 회전은 이동 입력으로만 바뀌므로 아주 짧게 밀었다가 제자리로 되돌린다."""
    cat.invoke("TestTeleportLocalPlayer", _x=cx, _z=cz)
    time.sleep(0.4)
    cat.invoke("TestWalkDirection", _x=tx - cx, _z=tz - cz, _seconds=0.08)
    time.sleep(0.4)
    cat.invoke("TestTeleportLocalPlayer", _x=cx, _z=cz)
    time.sleep(0.3)
    cat.invoke("TestPounce")
    time.sleep(0.6)


def warp_to_shop(host, player, client_id):
    """테크 v3(2026-09-07): 쥐는 **상점 자리 안에서만** 산다(기획 규칙 3).
    치즈 지점에 선 채로 사면 서버가 NotInShop으로 거절하므로, 사기 직전에 상점으로 옮긴다.

    TestWarpToNearestShop은 BodyPush.ServerPush 경유라 **서버(호스트) 인스턴스**로 불러야 하고,
    그 안에서 다시 소유자 RPC를 타므로 클론의 몸은 왕복이 끝나야 실제로 움직인다 — 그래서 쉬었다 확인한다.
    """
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.0)
    return bool(player.invoke("TestIsInShop", _clientId=client_id))


def main():
    cat = WesPoco(instance=0)
    m1 = WesPoco(instance=1)
    m2 = WesPoco(instance=2)

    cat_id = cat.invoke("TestGetLocalClientId")
    m1_id = m1.invoke("TestGetLocalClientId")
    m2_id = m2.invoke("TestGetLocalClientId")
    check("3인 접속", cat_id >= 0 and m1_id >= 0 and m2_id >= 0,
          "cat=%s m1=%s m2=%s" % (cat_id, m1_id, m2_id))

    for p in (cat, m1, m2):
        p.invoke("TestGrantCheeseAll")
    time.sleep(0.5)

    # ---------- 1. 호스트 치즈 라벨 실시간 갱신 (버그1) ----------
    sx, sz = [float(v) for v in m1.invoke("TestGetBestCheeseSpotPos").split(",")]
    before = cat.invoke("TestGetCheeseLabelNear", _x=sx, _z=sz)
    m1.invoke("TestTeleportLocalPlayer", _x=sx, _z=sz)
    time.sleep(4)
    after = cat.invoke("TestGetCheeseLabelNear", _x=sx, _z=sz)
    check("버그1 호스트 라벨 실시간 갱신", before != after and "점유" in after,
          "%s -> %s" % (before.replace("\n", "/"), after.replace("\n", "/")))

    # ---------- 2. 잠행 ----------
    # 2026-08-10부터 잠행은 **무음이 아니라 좁게 들린다** — 120×120 맵에서 완전 무음이면
    # 구석에 웅크린 쥐가 무적이 되어 "버티면 쥐 승" 규칙이 자동 승리가 된다.
    # 그래서 "소리가 나느냐"가 아니라 "몇 m까지 가느냐"를 잰다.
    # 벽에 처박히면 0.5초 만에 멈춰서 발소리가 꺼진다 → 걷는 동안 여러 번 떠서 최댓값을 쓴다.
    def footstep_range(walker, listener, walker_id, dx):
        best = -1.0
        for _ in range(2):
            walker.invoke("TestWalkDirection", _x=dx, _z=0, _seconds=1.2)
            for _ in range(4):
                time.sleep(0.25)
                best = max(best, float(listener.invoke("TestGetFootstepRange", _clientId=walker_id)))
            dx = -dx
        return best

    loud = footstep_range(cat, m1, cat_id, 1)
    cat.invoke("TestSetSneak", _on=True)
    time.sleep(0.5)
    replicated = m1.invoke("TestGetSneaking", _clientId=cat_id)
    quiet = footstep_range(cat, m1, cat_id, -1)
    cat.invoke("TestSetSneak", _on=False)
    time.sleep(0.3)

    check("잠행 상태 복제(원격이 관측)", replicated is True, "sneaking=%s" % replicated)
    check("잠행 중 발소리가 좁아진다(무음 아님)",
          0 < quiet < loud, "평소=%sm 잠행=%sm" % (loud, quiet))

    # ---------- 2b. 쥐 잠행 (2026-08-10) ----------
    # 쥐에게도 같은 거래를 줬다 — 무음 대신 이속 절반. 고양이 쪽만 보고 있으면
    # "쥐 프리팹에 컴포넌트를 안 붙였다"를 못 잡는다(전에 실제로 고양이 전용이었다).
    m1_id = int(m1.invoke("TestGetLocalClientId"))

    m_loud = footstep_range(m1, cat, m1_id, 1)
    m1.invoke("TestSetSneak", _on=True)
    time.sleep(0.5)
    m_replicated = cat.invoke("TestGetSneaking", _clientId=m1_id)
    m_quiet = footstep_range(m1, cat, m1_id, -1)
    m1.invoke("TestSetSneak", _on=False)
    time.sleep(0.3)

    check("잠행 — 쥐도 발소리가 좁아진다",
          0 < m_quiet < m_loud, "평소=%sm 잠행=%sm" % (m_loud, m_quiet))
    check("잠행 — 쥐 잠행도 복제된다", m_replicated is True, "sneaking=%s" % m_replicated)

    # ---------- 3. 요새: 즉발 방패 (2026-08-15, 미끼 흡수) ----------
    # 요새는 **내 위치 중심**에 즉시 선다. 그 자리에 있던 고양이는 밖으로 밀려나고,
    # 세운 쥐는 안에 남는다. 이걸 화면 밖 값이 아니라 실제 위치로 잰다.
    # 자리는 §8(sx+8 근처 덮치기)과 겹치지 않게 반대편 — 요새 벽이 남아 있으면 덮치기가 막힌다
    # 테크 v3: 값이 둘로 갈렸다 — 설치권(TechInfo Lv1)은 상점에서 한 번, 설치비는 세울 때마다.
    # 숫자를 여기 박지 않고 훅으로 읽는다(CSV가 원본).
    fort_right_cost = cat.invoke("TestGetTechCost", _techId="Fortress", _level=1)
    fort_place_cost = cat.invoke("TestGetPlaceCost", _techId="Fortress")

    fort_x = sx - 6.0
    # (1) 설치권은 상점에서만 살 수 있다 — 옮겨 사고 자리로 돌아온다
    in_shop = warp_to_shop(cat, m2, m2_id)
    check("상점 — 쥐가 상점 자리로 들어갔다", in_shop, "inShop=%s" % in_shop)
    right_before = m2.invoke("TestGetCheese")
    m2.invoke("TestBuyTechLocal", _techId="Fortress")
    time.sleep(1.2)
    right_spent = right_before - m2.invoke("TestGetCheese")
    check("요새 — 상점에서 설치권 구매",
          m2.invoke("TestGetTechLevel", _clientId=m2_id, _techId="Fortress") == 1
          and right_spent == fort_right_cost,
          "설치권 %s 나감(기대 %s)" % (right_spent, fort_right_cost))

    # (2) 자리로 돌아와 세운다
    m2.invoke("TestTeleportLocalPlayer", _x=fort_x, _z=sz)
    time.sleep(0.6)
    # 쥐 왼쪽 0.6m(풋프린트 안). 왼쪽인 이유: 밀려난 뒤 되돌아 밀어붙이는 방향이 +x가 되어
    # §6의 face_and_pounce(+x)와 바라보는 방향이 같다 — 0.08s 걷기론 180°를 못 돈다(실측).
    cat.invoke("TestTeleportLocalPlayer", _x=fort_x - 0.6, _z=sz)
    time.sleep(0.8)
    fort_cheese_before = m2.invoke("TestGetCheese")
    m2.invoke("TestUseTechLocal", _techId="Fortress")
    time.sleep(1.5)
    fpos = m2.invoke("TestGetFortressPos")
    check("요새 — 즉시 설치(조준 없음)", fpos != "", "pos=%s" % fpos)

    # ---------- 4. 요새 밀어내기 ----------
    if fpos:
        fx, fz = [float(v) for v in fpos.split(",")]
        mouse_gap = m2.invoke("TestGetDistanceTo", _x=fx, _z=fz)
        cat_gap = cat.invoke("TestGetDistanceTo", _x=fx, _z=fz)
        print("   요새 중심까지: 쥐 %.2f / 고양이 %.2f" % (mouse_gap, cat_gap))
        check("요새 — 세운 쥐 위치 중심에 선다(쥐는 안에)", mouse_gap < 0.7, "쥐 %.2f" % mouse_gap)
        check("요새 — 안에 있던 고양이는 밖으로 밀린다", cat_gap > 1.4, "고양이 %.2f" % cat_gap)

        # 밀려난 고양이가 되돌아 밀어붙여도 못 들어간다 — 좌우 통벽 쪽으로 0.35초
        cat.invoke("TestWalkDirection", _x=1, _z=0, _seconds=0.35)
        time.sleep(1.0)
        cat_gap2 = cat.invoke("TestGetDistanceTo", _x=fx, _z=fz)
        check("요새 — 고양이는 못 들어간다", cat_gap2 > 1.2, "밀어붙인 뒤 %.2f" % cat_gap2)
        # 첫 요새 = 설치권 + 설치비(=180). 부수고 다시 세우는 건 설치비(80)만 — 째기 응징이 살아 있다.
        place_spent = fort_cheese_before - m2.invoke("TestGetCheese")
        check("요새 — 설치마다 설치비가 나간다(재설치 값)", place_spent == fort_place_cost,
              "설치 %s (기대 %s)" % (place_spent, fort_place_cost))
        check("요새 — 첫 요새 총액 = 설치권 + 설치비",
              right_spent + place_spent == fort_right_cost + fort_place_cost,
              "총 %s (기대 %s)" % (right_spent + place_spent, fort_right_cost + fort_place_cost))

        # 요새 안에서 또 세우면 거부된다(2026-08-16) — 겹치면 벽끼리 서로를 가려 무적이 됐었다
        stack_before = m2.invoke("TestGetCheese")
        m2.invoke("TestUseTechLocal", _techId="Fortress")
        time.sleep(1.0)
        check("요새 — 요새 안에서는 겹쳐 못 세운다", m2.invoke("TestGetCheese") == stack_before,
              "치즈 %s -> %s" % (stack_before, m2.invoke("TestGetCheese")))
    else:
        # 요새가 안 섰으면 아래 6항목은 **못 돈다.** 조용히 건너뛰면 "N/N 통과"의 분모가
        # 실행마다 달라져 출력만 봐서는 구분이 안 된다(감사 E §2-4) — 결과에 남긴다.
        why = "요새가 안 서서 못 쟀다 (TestGetFortressPos가 빈 문자열)"
        skip("요새 — 세운 쥐 위치 중심에 선다(쥐는 안에)", why)
        skip("요새 — 안에 있던 고양이는 밖으로 밀린다", why)
        skip("요새 — 고양이는 못 들어간다", why)
        skip("요새 — 설치마다 설치비가 나간다(재설치 값)", why)
        skip("요새 — 첫 요새 총액 = 설치권 + 설치비", why)
        skip("요새 — 요새 안에서는 겹쳐 못 세운다", why)

    # ---------- 5. 설치 편의: 제 발밑에 창고 ----------
    # 요새와 같은 구조 — 설치권은 상점에서, 설치비는 놓을 때마다.
    wh_place_cost = cat.invoke("TestGetPlaceCost", _techId="Warehouse")
    wh_tick = balance_int("WarehouseTickCheese")   # CSV가 원본 — 코드에 수치를 박지 않는다
    wh_in_shop = warp_to_shop(cat, m1, m1_id)
    check("상점 — 창고 설치권을 사러 상점에 들어갔다", wh_in_shop, "inShop=%s" % wh_in_shop)
    m1.invoke("TestBuyTechLocal", _techId="Warehouse")
    time.sleep(1.2)
    check("창고 — 상점에서 설치권 구매",
          m1.invoke("TestGetTechLevel", _clientId=m1_id, _techId="Warehouse") == 1,
          "lv=%s" % m1.invoke("TestGetTechLevel", _clientId=m1_id, _techId="Warehouse"))

    m1.invoke("TestTeleportLocalPlayer", _x=sx + 2.0, _z=sz)
    time.sleep(0.6)
    before_ticks = m1.invoke("TestGetWarehouseTicks")
    cheese_before = m1.invoke("TestGetCheese")
    m1.invoke("TestPlaceWarehouseAt", _x=sx + 2.0, _z=sz)   # 설치권은 이미 있다 → 설치비만 나간다
    time.sleep(1.5)
    cheese_after = m1.invoke("TestGetCheese")
    # 설치 직후 1.5초 안에 첫 생산 틱(+tick)이 끼어들 수 있다 — 틱 수만큼 되돌려 설치비만 본다(2026-09-07 실측: 1081→991)
    ticks_in_window = m1.invoke("TestGetWarehouseTicks") - before_ticks
    # 창고 맵 실측(2026-09-07): 1080→991·1081→991 — 틱 외에 ±1 잔돈이 낀다. 설치비 근처(−틱−2 … +0)면 설치된 것.
    delta = cheese_before - cheese_after + ticks_in_window * wh_tick
    placed = wh_place_cost - wh_tick - 2 <= delta <= wh_place_cost
    check("설치 — 제 몸 위치에 창고 설치 성공(설치비 %s)" % wh_place_cost, placed,
          "치즈 %s -> %s" % (cheese_before, cheese_after))

    # ---------- 6. 장난기 구조물 x0.25 (방금 지은 창고를 때린다) ----------
    m1.invoke("TestTeleportLocalPlayer", _x=sx - 12.0, _z=sz)  # 덮치기 부채꼴에서 치운다(§3 요새 자리도 피한다)
    time.sleep(0.6)
    fun_before = cat.invoke("TestGetCheese")
    face_and_pounce(cat, sx + 2.0, sz, sx + 0.9, sz)  # 창고는 (sx+2, sz)
    time.sleep(0.8)
    fun_after = cat.invoke("TestGetCheese")
    gained = fun_after - fun_before
    check("장난기 — 구조물 60뎀 = 15pt(x0.25)", gained == 15,
          "획득 %s (기대 15)" % gained)

    # ---------- 7. 창고 수입 1/s ----------
    print("   창고 생산 21초 대기...")
    time.sleep(21)
    ticks = m1.invoke("TestGetWarehouseTicks") - before_ticks
    check("창고 — 2초마다 1틱(5/s)", 8 <= ticks <= 12, "21초 동안 %s틱 (기대 10)" % ticks)

    # ---------- 8. 따봉 ----------
    m2.invoke("TestTeleportLocalPlayer", _x=sx + 8.0, _z=sz)
    time.sleep(0.6)
    cat.invoke("TestTeleportLocalPlayer", _x=sx + 6.9, _z=sz)
    time.sleep(0.6)
    for _ in range(4):
        face_and_pounce(cat, sx + 8.0, sz, sx + 6.9, sz)
        time.sleep(2.2)
    time.sleep(1.0)
    # 잡히면 **즉시 탈락**(관전) — 감옥·구출(v3.8)은 2026-08-17에 뺐다.
    dead = m2.invoke("TestGetLocalHp") <= 0
    check("쥐가 잡히면 즉시 탈락(관전)", dead, "hp=%s" % m2.invoke("TestGetLocalHp"))

    if dead:
        time.sleep(1.5)
        # 따봉은 관전자의 것 — 탈락한 쥐가 살아 있는 쥐를 응원한다
        m2.invoke("TestCheer", _targetClientId=m1_id)
        time.sleep(1.0)
        on_mouse = m1.invoke("TestGetCheerMarkerCount")
        on_cat = cat.invoke("TestGetCheerMarkerCount")
        check("따봉 — 관전자가 살아 있는 쥐에게 보낸다", on_mouse >= 1, "마커 %s개" % on_mouse)
        check("따봉 — 고양이에겐 안 보임", on_cat == 0, "마커 %s개" % on_cat)
    else:
        # 쥐가 안 잡혔으면 관전자가 없어 따봉을 못 잰다 — 결과에 남긴다(감사 E §2-4).
        why = "쥐가 안 잡혀 관전자가 없다 — 앞의 덮치기가 실패했다"
        skip("따봉 — 관전자가 살아 있는 쥐에게 보낸다", why)
        skip("따봉 — 고양이에겐 안 보임", why)

    # ---------- 9. 카메라 거리 상수(충돌 제거) ----------
    # **정착 뒤에 잰다.** 0.9초에 재던 예전 판은 감쇠가 끝나기 전이라 카메라가 캐릭터를
    # 따라잡는 중이었고, 편차가 걷는 방향과 카메라 요각의 각도에 따라 널뛰었다
    # (2026-08-09 실측: 0.9초 0.44 · 1.5초 0.03 · 2.5초 0.00 — 같은 판에서 재현).
    # 잡으려는 건 "벽이 카메라를 밀어 넣는가"인데 그건 서 있어도 남는 정상상태 효과라,
    # 정착 후 측정이 오히려 더 민감하다. 이동 중 감쇠 지연은 정상 동작이고 관심사가 아니다.
    dists = []
    for i in range(4):
        cat.invoke("TestWalkDirection", _x=1 if i % 2 == 0 else -1, _z=0, _seconds=0.6)
        time.sleep(1.8)
        dists.append(cat.invoke("TestGetCameraDistance"))
    spread = max(dists) - min(dists)
    check("카메라 — 벽 근처에서도 거리 일정", spread < 0.35,
          "편차 %.2f (%s)" % (spread, ", ".join("%.2f" % d for d in dists)))

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
