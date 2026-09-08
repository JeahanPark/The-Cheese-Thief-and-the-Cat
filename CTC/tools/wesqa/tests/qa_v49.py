# -*- coding: utf-8 -*-
"""미니맵 6항목 — 미니맵 계획 Task 5 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이** — 세팅: `python setup_match.py 0 cat 3` (클론 둘 다 쥐).

이 스크립트가 지키는 계약은 기획서 §2의 비대칭 표다.

    지형·내 위치   쥐 ○ · 고양이 ○
    상점 자리      쥐 ○ · 고양이 ○   ← 여기서 만난다
    치즈 매장지    쥐 발견분만 · 고양이 ✕
    내 창고·요새   쥐 ○(내 것만) · 고양이 ✕
    **남**         어느 쪽에도 ✕      ← 이게 핵심이다

왜 호스트가 고양이인가: 「고양이 화면에 무엇이 있나」를 세는 항목이 셋(③⑥)이고,
미니맵 값은 **전부 그 인스턴스의 화면**이라 복제되지 않는다. 고양이를 호스트에 두면
지갑 주기·좌표 조회 같은 서버 훅과 같은 인스턴스에서 읽힌다.

세는 값은 **Model 목록이 아니라 View가 실제로 켠 아이콘**이다(`TestGetMinimapMarkerCount`).
목록 길이를 세면 View가 하나도 안 켜도 통과한다 — 볼륨은 바뀌는데 소리는 그대로였던
사고(2026-08-09)와 같은 눈속임이라, 훅부터 화면을 보게 만들었다.

②의 기대값은 **하드코딩하지 않는다.** 지금 기본 맵은 `ShopSpotMinGap` 탓에 상점이 1곳만
서지만 그건 미니맵의 잘못이 아니다 — 「미니맵이 씬과 같은 수를 그리는가」만 본다.

한 판에 한 번만 돌린다(창고를 세우고 쥐를 옮긴다). 다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io, math

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

# 「그 자리에 점이 없다」의 기준(m).
#
# 재려는 것은 **거리가 아니라 추적**이다. 상대를 따라다니는 점이 있으면 상대를 어디로 옮기든
# 그 자리에서 0 m가 나온다 — 픽셀 반올림 오차(30 m를 200 px에 담으므로 한 칸 ≈0.15 m)뿐이다.
# 그래서 기준은 그 오차보다 넉넉히 크되(≈13배), **상점·치즈 마커와 우연히 가까워지는 거리보다는
# 작게** 잡는다. 크게 잡으면 남을 안 그린다는 것이 아니라 「마커가 드문가」를 재게 된다
# (기본 맵은 30×30에 치즈 지점이 7곳이라 실제로 4~5 m 안에 남의 마커가 흔하다).
# 대신 상대를 **두 자리로 옮겨** 두 번 잰다 — 우연히 가까운 것과 따라다니는 것은 그래야 갈린다.
ENEMY_CLEAR_METERS = 2.0

RESULTS = []
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 7      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


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


def poll(read, done, limit=6.0, step=0.3):
    """조건이 참이 될 때까지(상한 limit초) 다시 읽는다 → 마지막 값.

    미니맵은 0.2초 주기로 다시 그리고, 구매·설치는 서버 RPC 왕복이라 **클론에서는
    「했다 → 바로 읽는다」가 늘 실패한다**(qa_v47·v48과 같은 이유). 한 번 읽고 판정하면
    배선이 멀쩡한데도 빨간 줄이 뜬다.
    """
    until = time.time() + limit
    value = read()
    while not done(value) and time.time() < until:
        time.sleep(step)
        value = read()
    return value


def markers(inst, kind=""):
    return inst.invoke("TestGetMinimapMarkerCount", _kind=kind)


def kinds_of(inst):
    """(상점, 치즈, 구조물, 전체) — 전체가 셋의 합과 어긋나면 넷째 무언가가 그려지고 있다."""
    return (markers(inst, "shop"), markers(inst, "cheese"),
            markers(inst, "structure"), markers(inst, "all"))


def pos_of(host, client_id):
    raw = host.invoke("TestGetPlayerPos", _clientId=client_id)
    if not raw:
        return None
    x, z = raw.split(",")
    return float(x), float(z)


def nearest_to(inst, xz):
    """그 자리에 가장 가까운 **켜진 마커**까지의 거리(m). 못 잰 경우 `None`.

    훅은 **미니맵이 통째로 없어도** -1을 준다 — 「마커가 하나도 없다」와 구분이 안 된다.
    예전엔 그 -1을 `float("inf")`로 바꿔 「그 자리에 점이 없다」를 통과시켰다(감사 E F-5):
    **미니맵이 안 떠도 ⑥이 초록이었다.** 이제 -1은 실패 신호로 그대로 올린다 —
    「미니맵이 실제로 마커를 그리고 있다」는 호출부가 따로 확인한다.
    """
    d = inst.invoke("TestGetMinimapNearestMarkerMeters", _x=xz[0], _z=xz[1])
    if d is None or d < 0:
        return None
    return float(d)


def teleport(inst, x, z, settle=1.2):
    inst.invoke("TestTeleportLocalPlayer", _x=x, _z=z)
    time.sleep(settle)


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [int(x.invoke("TestGetLocalClientId")) for x in p]
    roles = [x.invoke("TestGetRole") for x in p]
    ok_setup = check("⓪ 3인 접속 · 호스트=고양이 · 쥐 2",
                     all(i >= 0 for i in ids) and roles[0] == "Cat"
                     and roles.count("Cat") == 1 and roles.count("Mouse") == 2,
                     "roles=%s ids=%s" % (roles, ids))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 0 cat 3`으로 판을 다시 연다")
        return summary(aborted=True)

    host = p[0]          # 호스트 = 고양이
    cat_id = ids[0]
    m1_idx = [i for i in range(3) if i != 0][0]
    m1i, m1 = p[m1_idx], ids[m1_idx]
    print("고양이=%s(호스트) · 쥐A=%s(inst %s)" % (cat_id, m1, m1_idx))

    # ---------- ① 쥐·고양이 둘 다 미니맵이 떠 있다 ----------
    cat_shown = poll(lambda: bool(host.invoke("TestIsMinimapVisible")), lambda v: v)
    mouse_shown = poll(lambda: bool(m1i.invoke("TestIsMinimapVisible")), lambda v: v)
    check("① 미니맵이 양쪽 화면에 떠 있다", cat_shown and mouse_shown,
          "고양이=%s 쥐=%s" % (cat_shown, mouse_shown))

    # ---------- ② 상점 마커 수 = 실제 상점 자리 수 ----------
    # 기대값은 훅이 준다. 지금 기본 맵은 1곳만 서지만(ShopSpotMinGap) 그건 맵 문제라
    # 여기서 상수로 박으면 맵이 고쳐지는 날 이 테스트가 거짓말을 한다.
    spots = host.invoke("TestGetShopSpotCount")
    cat_shop = poll(lambda: markers(host, "shop"), lambda v: v == spots)
    mouse_shop = poll(lambda: markers(m1i, "shop"), lambda v: v == spots)
    check("② 상점 마커 수 = 씬의 상점 자리 수 (양쪽 다)",
          spots > 0 and cat_shop == spots and mouse_shop == spots,
          "씬 %s개 · 고양이 %s · 쥐 %s" % (spots, cat_shop, mouse_shop))

    # ---------- ③ 고양이 치즈 마커 0 ----------
    # 고양이를 치즈 매장지 **위에** 세우고도 0이어야 한다 — 「발견」이 역할을 이기면 안 된다.
    spot = host.invoke("TestGetBestCheeseSpotPos")
    sx, sz = [float(v) for v in spot.split(",")]
    teleport(host, sx, sz, settle=2.0)
    cat_cheese = poll(lambda: markers(host, "cheese"), lambda v: v != 0, limit=3.0)
    cat_struct = markers(host, "structure")
    cat_disc = host.invoke("TestGetMinimapDiscoveredCheese")
    check("③ 고양이 화면에 치즈 마커 0 (매장지 위에 서 있어도)",
          cat_cheese == 0 and cat_struct == 0,
          "매장지(%.1f, %.1f) 위 · 치즈 %s · 구조물 %s · 발견기억 %s"
          % (sx, sz, cat_cheese, cat_struct, cat_disc))

    # ---------- ④ 쥐 치즈 마커 — 처음엔 없고, 매장지 옆으로 가면 는다 ----------
    mouse_cheese0 = markers(m1i, "cheese")
    disc0 = m1i.invoke("TestGetMinimapDiscoveredCheese")

    # 매장지 바로 옆(발견 거리 12 m 안). 지점 위가 아니라 옆에 세운다 — 콜라이더에 끼지 않게.
    teleport(m1i, sx + 2.0, sz + 2.0, settle=2.0)
    mouse_cheese1 = poll(lambda: markers(m1i, "cheese"), lambda v: v > mouse_cheese0)
    disc1 = m1i.invoke("TestGetMinimapDiscoveredCheese")
    check("④ 쥐 치즈 마커 — 매장지 옆으로 가면 는다",
          mouse_cheese1 > mouse_cheese0 and disc1 > disc0 and mouse_cheese1 >= 1,
          "마커 %s→%s · 발견기억 %s→%s" % (mouse_cheese0, mouse_cheese1, disc0, disc1))
    if mouse_cheese0 != 0:
        note("④' 시작 치즈 마커가 0이 아니었다",
             "%s개 — 쥐 스폰 자리가 이미 매장지 12 m 안이었다는 뜻이다(기획상 문제는 아니다)"
             % mouse_cheese0)

    # ---------- ⑤ 쥐가 창고를 세우면 내 구조물 마커 +1 ----------
    for _ in range(3):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)

    struct0 = markers(m1i, "structure")
    warehouses0 = host.invoke("TestGetWarehouseCountOf", _clientId=m1)

    # 설치권은 **상점 안에서만** 산다(TechLogic.NeedsShopSpot — 쥐만). 사는 것과 세우는 것을
    # 나눠 부른다: TestPlaceWarehouseAt에 맡기면 "못 샀나 못 세웠나"가 한 덩어리로 실패한다.
    host.invoke("TestWarpToNearestShop", _clientId=m1)
    in_shop = poll(lambda: bool(host.invoke("TestIsInShop", _clientId=m1)), lambda v: v)
    m1i.invoke("TestBuyTechLocal", _techId="Warehouse")
    permit = poll(lambda: host.invoke("TestGetTechLevel", _clientId=m1, _techId="Warehouse"),
                  lambda v: v >= 1)

    # 세우는 자리는 **상점 자리 안**이다 - 「상점에서 설치권을 사고 그 자리에 세운다」가
    # 테크 v3의 동선이라, 여기서 막히면 기능이 없는 것과 같다.
    #
    # 2026-09-08에 실제로 한 번 막혔었다: 상점을 방 크기와 무관하게 방 중심에서 뽑는 바람에
    # 기본 맵의 상점이 4x6 m 방(R2)에 앉았고, 반경 3 m 원반의 대부분이 벽 속이었다.
    # 지금은 원반이 들어가는 방만 고른다(ShopSpotLogic.RoomHoldsShop). 그래서 이 항목은
    # **그 회귀를 잡는 그물**이다 - 실패하면 TestDebugPlacementBlockers가 무엇이 막았는지
    # 콜라이더 이름으로 뱉는다.
    shx, shz = [float(v) for v in host.invoke("TestGetShopSpotPos", _index=0).split(",")]
    ring = float(host.invoke("TestGetShopSpotRadius")) * 0.6

    placed, placed_at, blockers = warehouses0, None, []
    for k in range(9):
        if placed > warehouses0:
            break
        # 발치 -> 원반 안쪽 여덟 방향. 쥐는 이미 상점 한가운데 서 있어 걷지 않는다.
        if k == 0:
            px, pz = shx, shz
        else:
            ang = (k - 1) * math.pi / 4.0
            px, pz = shx + math.cos(ang) * ring, shz + math.sin(ang) * ring
        m1i.invoke("TestPlaceWarehouseAt", _x=px, _z=pz)
        placed = poll(lambda: host.invoke("TestGetWarehouseCountOf", _clientId=m1),
                      lambda v: v > warehouses0, limit=3.0)
        if placed > warehouses0:
            placed_at = (px, pz)
        else:
            blockers.append(host.invoke("TestDebugPlacementBlockers",
                                        _x=px, _z=pz, _techId="Warehouse"))
    if blockers:
        note("5' 상점 안에서 거절된 자리", " ;; ".join(blockers[:3]))

    struct1 = poll(lambda: markers(m1i, "structure"), lambda v: v > struct0)
    cat_struct1 = markers(host, "structure")
    check("⑤ 쥐가 창고를 세우면 내 구조물 마커 +1 (고양이 화면엔 여전히 0)",
          placed == warehouses0 + 1 and struct1 == struct0 + 1 and cat_struct1 == 0,
          "상점 안=%s 설치권 Lv%s · 창고 %s→%s @%s · 쥐 구조물 마커 %s→%s · 고양이 구조물 마커 %s"
          % (in_shop, permit, warehouses0, placed, placed_at, struct0, struct1, cat_struct1))

    # ---------- ⑥ 어느 쪽 화면에도 상대 마커가 0 ----------
    # 두 가지를 같이 본다.
    #  ㉠ 전체 = 상점+치즈+구조물 — 아는 세 종류 말고 넷째가 그려지고 있지 않다.
    #  ㉡ 상대가 선 자리에 점이 없다 — 적 마커가 상점인 척 섞여 들어와도 ㉠은 못 잡는다.
    #     상대를 **두 자리로 옮겨** 두 번 잰다: 마커가 따라다니면 두 번 다 0 m가 나온다.
    cat_kinds = kinds_of(host)
    mouse_kinds = kinds_of(m1i)
    sums_ok = (sum(cat_kinds[:3]) == cat_kinds[3]) and (sum(mouse_kinds[:3]) == mouse_kinds[3])

    dists = []
    for ox, oz in [(6.0, 6.0), (-7.0, 5.0)]:
        teleport(host, sx + ox, sz + oz, settle=1.6)       # 고양이를 옮긴다
        time.sleep(0.5)
        cat_at = pos_of(host, cat_id)
        dists.append(("쥐 화면 ← 고양이 자리", cat_at, nearest_to(m1i, cat_at)))

    for ox, oz in [(4.0, -6.0), (7.0, 2.0)]:
        teleport(m1i, sx + ox, sz + oz, settle=1.6)        # 쥐를 옮긴다
        time.sleep(0.5)
        mouse_at = pos_of(host, m1)
        dists.append(("고양이 화면 ← 쥐 자리", mouse_at, nearest_to(host, mouse_at)))

    # **먼저 미니맵이 살아 있는지 본다.** 마커가 0개면 「상대 자리에 점이 없다」는 공허하다 —
    # 미니맵이 통째로 안 떠도 같은 결과가 나오기 때문이다(감사 E F-5).
    drawing = cat_kinds[3] > 0 and mouse_kinds[3] > 0
    measured = all(d is not None for _, _, d in dists)
    clear = measured and all(d >= ENEMY_CLEAR_METERS for _, _, d in dists)
    check("⑥ 어느 쪽 화면에도 상대 마커가 0",
          drawing and sums_ok and clear,
          "미니맵이 마커를 그린다=%s(전체 고양이%s 쥐%s) · 종류합 고양이%s 쥐%s(전체와 일치=%s)"
          " · 전부 측정됨=%s · 상대 자리까지 최단거리 %s (기준 ≥%.1f m)"
          % (drawing, cat_kinds[3], mouse_kinds[3],
             cat_kinds[:3], mouse_kinds[:3], sums_ok, measured,
             ["%s %s %s" % (n, tuple(round(v, 1) for v in at),
                            "못 잼(-1)" if d is None else "%.1fm" % d)
              for n, at, d in dists],
             ENEMY_CLEAR_METERS))

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
