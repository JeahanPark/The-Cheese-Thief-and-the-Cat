# -*- coding: utf-8 -*-
"""v3.6 종합 검증 — MPPM 3인(메인=고양이, 클론2=쥐)을 wesqa로 조작한다.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
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
    fort_x = sx - 6.0
    m2.invoke("TestTeleportLocalPlayer", _x=fort_x, _z=sz)
    time.sleep(0.6)
    # 쥐 왼쪽 0.6m(풋프린트 안). 왼쪽인 이유: 밀려난 뒤 되돌아 밀어붙이는 방향이 +x가 되어
    # §6의 face_and_pounce(+x)와 바라보는 방향이 같다 — 0.08s 걷기론 180°를 못 돈다(실측).
    cat.invoke("TestTeleportLocalPlayer", _x=fort_x - 0.6, _z=sz)
    time.sleep(0.8)
    fort_cheese_before = m2.invoke("TestGetCheese")
    m2.invoke("TestBuyFortress")
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
        check("요새 — 값 80이 나갔다", fort_cheese_before - m2.invoke("TestGetCheese") == 80,
              "치즈 %s -> %s" % (fort_cheese_before, m2.invoke("TestGetCheese")))

        # 요새 안에서 또 사면 거부된다(2026-08-16) — 겹치면 벽끼리 서로를 가려 무적이 됐었다
        stack_before = m2.invoke("TestGetCheese")
        m2.invoke("TestBuyFortress")
        time.sleep(1.0)
        check("요새 — 요새 안에서는 겹쳐 못 세운다", m2.invoke("TestGetCheese") == stack_before,
              "치즈 %s -> %s" % (stack_before, m2.invoke("TestGetCheese")))

    # ---------- 5. 설치 편의: 제 발밑에 창고 ----------
    m1.invoke("TestTeleportLocalPlayer", _x=sx + 2.0, _z=sz)
    time.sleep(0.6)
    before_ticks = m1.invoke("TestGetWarehouseTicks")
    cheese_before = m1.invoke("TestGetCheese")
    m1.invoke("TestPlaceWarehouseAt", _x=sx + 2.0, _z=sz)
    time.sleep(1.2)
    cheese_after = m1.invoke("TestGetCheese")
    placed = cheese_before - cheese_after >= 150
    check("설치 — 제 몸 위치에 창고 설치 성공", placed,
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
    # v3.8: 잡히면 죽지 않고 갇힌다. 예전엔 여기서 despawn되어 관전으로 넘어갔다 —
    # "3분에 죽으면 남은 7분을 관전만" 문제를 감옥이 대체했다.
    jailed = m2.invoke("TestGetJailed")
    check("쥐가 잡히면 갇힌다(탈락 아님)", jailed, "jailed=%s" % jailed)

    if jailed:
        # 따봉도 관전자에서 수감자로 옮겨 갔다 — 갇힌 쥐가 구하러 오는 동료를 응원한다
        m2.invoke("TestCheer", _targetClientId=m1_id)
        time.sleep(1.0)
        on_mouse = m1.invoke("TestGetCheerMarkerCount")
        on_cat = cat.invoke("TestGetCheerMarkerCount")
        check("따봉 — 갇힌 쥐가 자유로운 쥐에게 보낸다", on_mouse >= 1, "마커 %s개" % on_mouse)
        check("따봉 — 고양이에겐 안 보임", on_cat == 0, "마커 %s개" % on_cat)

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

    print("\n===== 요약 =====")
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + ("  | " + detail if detail else ""))
    print("%d/%d" % (passed, len(RESULTS)))
    return 0 if passed == len(RESULTS) else 1


if __name__ == "__main__":
    sys.exit(main())
