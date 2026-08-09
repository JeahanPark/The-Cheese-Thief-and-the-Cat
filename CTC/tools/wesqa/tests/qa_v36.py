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
    # 벽에 처박히면 0.5초 만에 멈춰서 발소리가 꺼진다 → 시작 직후에 표본을 뜬다
    def footstep_while_walking(dx):
        cat.invoke("TestWalkDirection", _x=dx, _z=0, _seconds=1.2)
        time.sleep(0.25)
        v = m1.invoke("TestGetFootstepAudible", _clientId=cat_id)
        time.sleep(1.2)
        return v

    loud = footstep_while_walking(1)
    cat.invoke("TestSetSneak", _on=True)
    time.sleep(0.5)
    replicated = m1.invoke("TestGetSneaking", _clientId=cat_id)
    quiet = footstep_while_walking(-1)
    cat.invoke("TestSetSneak", _on=False)
    time.sleep(0.3)

    check("잠행 상태 복제(원격이 관측)", replicated is True, "sneaking=%s" % replicated)
    check("잠행 중 발소리 무음", loud is True and quiet is False,
          "평소=%s 잠행=%s" % (loud, quiet))

    # ---------- 2b. 쥐 잠행 (2026-08-10) ----------
    # 쥐에게도 같은 거래를 줬다 — 무음 대신 이속 절반. 고양이 쪽만 보고 있으면
    # "쥐 프리팹에 컴포넌트를 안 붙였다"를 못 잡는다(전에 실제로 고양이 전용이었다).
    m1_id = int(m1.invoke("TestGetLocalClientId"))

    def mouse_footstep_while_walking(dx):
        m1.invoke("TestWalkDirection", _x=dx, _z=0, _seconds=1.2)
        time.sleep(0.25)
        v = cat.invoke("TestGetFootstepAudible", _clientId=m1_id)
        time.sleep(1.2)
        return v

    m_loud = mouse_footstep_while_walking(1)
    m1.invoke("TestSetSneak", _on=True)
    time.sleep(0.5)
    m_replicated = cat.invoke("TestGetSneaking", _clientId=m1_id)
    m_quiet = mouse_footstep_while_walking(-1)
    m1.invoke("TestSetSneak", _on=False)
    time.sleep(0.3)

    check("잠행 — 쥐도 발소리를 죽인다", m_loud is True and m_quiet is False,
          "평소=%s 잠행=%s" % (m_loud, m_quiet))
    check("잠행 — 쥐 잠행도 복제된다", m_replicated is True, "sneaking=%s" % m_replicated)

    # ---------- 3. 미끼: 즉시 설치 ----------
    m2.invoke("TestBuyDecoy")
    time.sleep(1.2)
    dpos = m2.invoke("TestGetDecoyPos")
    check("미끼 즉시 설치(조준 없음)", dpos != "", "pos=%s" % dpos)

    # ---------- 4. 미끼 충돌 비대칭 ----------
    if dpos:
        dx, dz = [float(v) for v in dpos.split(",")]

        def push_through(p, label):
            """미끼 1.2m 앞에서 0.35초(≈2.2m)만 밀어붙인다.
            판정은 **이동 거리** — 미끼까지의 거리는 부호가 없어 '앞에서 막힘'과
            '지나쳐서 멀어짐'을 구분 못 한다. 길게 밀면 벽이 먼저 막아 계측이 오염된다."""
            p.invoke("TestTeleportLocalPlayer", _x=dx - 1.2, _z=dz)
            time.sleep(0.7)
            p.invoke("TestWalkDirection", _x=1, _z=0, _seconds=0.35)
            time.sleep(1.2)
            moved = p.invoke("TestGetDistanceTo", _x=dx - 1.2, _z=dz)
            to_decoy = p.invoke("TestGetDistanceTo", _x=dx, _z=dz)
            print("   %s: 이동 %.2f / 미끼까지 %.2f" % (label, moved, to_decoy))
            return moved, to_decoy

        mouse_moved, _ = push_through(m2, "쥐")
        cat_moved, cat_gap = push_through(cat, "고양이")
        check("미끼 — 쥐는 통과", mouse_moved > 1.6, "이동 %.2f (미끼 자리를 지나감)" % mouse_moved)
        check("미끼 — 고양이는 막힘", cat_moved < 1.2 and cat_gap < 0.9,
              "이동 %.2f, 미끼 앞 %.2f에서 정지" % (cat_moved, cat_gap))

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
    m1.invoke("TestTeleportLocalPlayer", _x=sx - 6.0, _z=sz)  # 덮치기 부채꼴에서 치운다
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
