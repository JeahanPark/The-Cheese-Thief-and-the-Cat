# -*- coding: utf-8 -*-
"""무작위 3 + 전직 300 + 봇 상점 10항목 — 테크 v3 계획 3 Task 7 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이 · 맵=창고(인덱스 2)** — 세팅: `python setup_match.py 2 cat 3`

왜 호스트가 고양이인가:
  · ⑨(전직 300)는 `TestGrantAdvanceMischief`·`TestGetCanAdvance`가 **로컬 전용**이라
    고양이를 쥔 인스턴스에서 불러야 한다.
  · 판을 끝내고 다시 여는 것(재시작)이 이 스크립트의 뼈대인데, `TestExpireTimer`도
    `TestRestartMatch`도 **서버 전용**이다. 고양이가 호스트면 한 인스턴스에서 다 몬다.
  · 그리고 판이 **실제로 끝나야** 재시작이 듣는다(`MatchManager.RequestRestart`는
    `Result == None`이면 통째로 무시한다). 끝나려면 `ShouldArm`이 걸려야 하고 그건
    **살아 있는 고양이가 하나 이상**일 때다 — 호스트가 고양이면 그 조건이 저절로 선다.

왜 맵이 창고(인덱스 2)인가: 반경 60이라 「대형」이고(ShopSpotLargeHalfExtent 40),
상점 후보가 **8곳**이라 그중 넷을 뽑는다. 절차 맵(0·1)은 반경이 작아 상점이 2곳이고
후보도 방 수만큼뿐이라 「판마다 다른 넷」을 잴 수가 없다.

**왜 판을 세 번 여는가**: 무작위는 한 판으로는 못 잰다. 두 판도 부족하다 —
상점 조합은 C(8,4)=70, 직업권 자리는 벽감 6곳 중 둘이라, **우연히 같은 판**이
드물지만 실제로 나온다(각각 ~1.4%·~3%). 두 판만 보고 빨간 줄을 띄우면 그 줄은
버그가 아니라 주사위를 말하는 것이 된다. 세 판을 뽑아 **서로 다른 값이 둘 이상**인지 본다.
그래도 셋이 전부 같으면 그건 우연이 아니라 시드가 안 굴렀다는 뜻이다.

⑥은 **차감 0까지** 본다 — 값만 보는 QA가 배선 누락을 놓친 전례(2026-08-09)를 반복하지 않는다.

한 판에 한 번만 돌린다(칸을 채우고 봇을 세운다). 다시 돌리려면 플레이모드를 껐다 켠다.
"""
import sys, time, io, math

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

# 창고 맵 = MapDirector의 세 번째 레이아웃(Layout_Warehouse01, 반경 60 · 손 배치 · 상점 후보 8).
WAREHOUSE_MAP_INDEX = 2
WAREHOUSE_SHOP_COUNT = 4          # 반경 60 ≥ ShopSpotLargeHalfExtent(40) → ShopSpotCount
SOLD_OUT_COUNT = 2                # BalanceInfo.SoldOutCount
ADVANCE_MISCHIEF = 300            # BalanceInfo.CatAdvanceMischief

# 절대 품절되지 않는 테크(TechRandomLogic.NEVER_SOLD_OUT) — 생존과 째기의 바닥이다.
NEVER_SOLD_OUT = ("Fortress", "Dash", "Warehouse")

# 쥐 카탈로그(TechId 순). 품절 후보는 여기서 면제 셋을 뺀 7종이다.
MOUSE_CATALOG = ("Gun", "Pepper", "Fortress", "CheeseShield", "Warehouse",
                 "BigPouch", "HiddenWarehouse", "Sticky", "Dash", "FastFeet")

# ⑦ 대조군으로 쓸 수 있는 테크 — **설치권이 아니라 즉시 레벨이 오르는 것**만 고른다.
# 요새·창고는 사도 "설치권"이라 레벨은 오르지만 이 판정에 창고 상한 같은 다른 이유가 섞인다.
CONTROL_CANDIDATES = ("Gun", "Pepper", "CheeseShield", "BigPouch",
                      "HiddenWarehouse", "Sticky", "FastFeet")

ROUNDS = 3

RESULTS = []
DEGRADED = []       # 실행이 열화된 사유 — 하나라도 있으면 통과로 안 읽는다
EXTRA = []          # 분모에 안 들어가는 참고 기록(NOTE)
EXPECTED = 12      # 이 스위트가 선언한 항목 수 — summary가 실행 수와 대조한다


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


def poll(read, done, limit=6.0, step=0.3):
    """조건이 참이 될 때까지(상한 limit초) 다시 읽는다 → 마지막 값.

    구매·설치·재시작은 전부 서버 RPC 왕복이라 **클론에서는 「했다 → 바로 읽는다」가 늘 실패한다**
    (qa_v47~v49와 같은 이유). 한 번 읽고 판정하면 배선이 멀쩡한데도 빨간 줄이 뜬다.
    """
    until = time.time() + limit
    value = read()
    while not done(value) and time.time() < until:
        time.sleep(step)
        value = read()
    return value


def parse_spots(raw):
    """"x,z|x,z|..." → [(x, z), ...]. 빈 문자열은 빈 목록."""
    if not raw:
        return []
    out = []
    for part in raw.split("|"):
        if not part:
            continue
        x, z = part.split(",")
        out.append((float(x), float(z)))
    return out


def spot_key(spots):
    """자리 **집합**의 지문 — 명부 순서가 아니라 어디에 섰는지만 본다."""
    return tuple(sorted((round(x, 1), round(z, 1)) for x, z in spots))


def parse_sold_out(raw):
    return tuple(t for t in (raw or "").split(",") if t)


def snapshot(host):
    """판 하나의 무작위 지문 — 읽기만 한다(판 상태를 안 건드린다)."""
    return {
        "seed": host.invoke("TestGetMatchSeed"),
        "sub": tuple(host.invoke("TestGetMatchSubSeed", _use=u)
                     for u in ("shop", "soldout", "ticket")),
        "spots": parse_spots(host.invoke("TestGetShopSpotPositions")),
        "count": host.invoke("TestGetShopSpotCount"),
        "radius": float(host.invoke("TestGetShopSpotRadius")),
        "soldout": parse_sold_out(host.invoke("TestGetSoldOutTechs")),
        "tickets": host.invoke("TestGetJobTicketPositions"),
        "loot": host.invoke("TestGetMapLootCheese"),
    }


def restart_round(host):
    """판을 끝내고 다시 연다 → (성공?, 설명).

    순서를 지켜야 한다:
      1. `TestExpireTimer`로 시계를 3초로 — **끝나지 않은 판은 재시작이 무시된다.**
      2. 결과가 실제로 붙을 때까지 기다린다(`TestGetMatchResult` != None).
      3. `TestRestartMatch` → 로비 복귀. 역할은 유지되므로 다시 고르지 않는다.
      4. **한 박자 쉰 뒤** 시작한다 — 껐다 켜는 것을 같은 프레임에 하면 MatchManager가
         전환을 놓쳐 타이머를 안 채우고, 그러면 시작하자마자 시간 만료로 끝난다(CLAUDE.md 함정 7).
    """
    host.invoke("TestExpireTimer")
    result = poll(lambda: host.invoke("TestGetMatchResult"),
                  lambda v: v not in (None, "", "None"), limit=25.0, step=0.5)
    if result in (None, "", "None"):
        return False, "판이 안 끝났다 (result=%s remaining=%s)" % (
            result, host.invoke("TestGetMatchRemaining"))

    host.invoke("TestRestartMatch")
    lobby = poll(lambda: host.invoke("TestGetMatchResult"),
                 lambda v: v in ("None", "", None), limit=10.0, step=0.4)
    time.sleep(1.5)

    host.invoke("TestStartGame")
    seed = poll(lambda: host.invoke("TestGetMatchSeed"),
                lambda v: v not in (None, 0), limit=25.0, step=0.5)
    spots = poll(lambda: host.invoke("TestGetShopSpotCount"),
                 lambda v: v is not None and v > 0, limit=15.0, step=0.5)
    role = host.invoke("TestGetRole")
    ok = seed not in (None, 0) and spots and spots > 0 and role == "Cat"
    return ok, "직전결과=%s → 로비=%s · 새 시드=%s · 상점 %s곳 · 역할 %s" % (
        result, lobby, seed, spots, role)


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [int(x.invoke("TestGetLocalClientId")) for x in p]
    roles = [x.invoke("TestGetRole") for x in p]
    selected = p[0].invoke("TestGetSelectedMap") or ""
    map_ok = selected.startswith("%d:" % WAREHOUSE_MAP_INDEX)

    ok_setup = check("⓪ 3인 접속 · 호스트=고양이 · 쥐 2 · 창고 맵",
                     all(i >= 0 for i in ids) and roles[0] == "Cat"
                     and roles.count("Cat") == 1 and roles.count("Mouse") == 2 and map_ok,
                     "roles=%s ids=%s map=%s shape=%s"
                     % (roles, ids, selected, p[0].invoke("TestGetMapShape")))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 2 cat 3`으로 판을 다시 연다")
        return summary(aborted=True)

    host = p[0]                                     # 호스트 = 고양이
    cat_id = ids[0]
    mouse_idx = [i for i in range(3) if roles[i] == "Mouse"]
    m1i, m1 = p[mouse_idx[0]], ids[mouse_idx[0]]
    print("고양이=%s(호스트) · 쥐A=%s(inst %s) · 쥐B=%s"
          % (cat_id, m1, mouse_idx[0], ids[mouse_idx[1]]))

    # ---------- 판을 세 번 열어 지문을 모은다 ----------
    shots = []
    for r in range(ROUNDS):
        if r > 0:
            ok, why = restart_round(host)
            if not ok:
                # 판 재시작 실패는 **결과에 남는다** — 뒤 항목이 덜 도는데 통과로 읽히면 안 된다
                note("판 %d 재시작 실패" % (r + 1), why)
                DEGRADED.append("판 %d 재시작 실패: %s" % (r + 1, why))
                print("!! 판을 다시 못 열었다 — 뒤 항목을 잴 수 없다")
                break
            note("판 %d 재시작" % (r + 1), why)
        s = snapshot(host)
        shots.append(s)
        print("  판%d  seed=%s sub=%s 상점%s곳 품절=%s 직업권=%s 치즈총량=%s"
              % (r + 1, s["seed"], s["sub"], s["count"],
                 ",".join(s["soldout"]) or "-", s["tickets"], s["loot"]))
        sys.stdout.flush()

    if len(shots) < 2:
        print("!! 지문이 한 판뿐이라 비교 항목을 못 잰다")
        return summary(aborted=True)

    last = shots[-1]

    # ---------- ① 매치 시드가 판마다 다르다 ----------
    seeds = [s["seed"] for s in shots]
    check("① 매치 시드가 판마다 다르다",
          len(set(seeds)) >= 2 and 0 not in seeds,
          "시드 %s (서로 다른 값 %d개)" % (seeds, len(set(seeds))))

    # 하위 시드가 실제로 갈렸는지 — 셋이 같으면 "상점이 왼쪽인 판은 늘 후추 품절"이 된다.
    subs_split = all(len(set(s["sub"])) == 3 for s in shots)
    check("①' 하위 시드 셋(shop·soldout·ticket)이 서로 다르다",
          subs_split, " · ".join(str(s["sub"]) for s in shots))

    # ---------- ② 상점 자리가 판마다 다르다 ----------
    keys = [spot_key(s["spots"]) for s in shots]
    moved = len(set(keys)) >= 2
    check("② 상점 자리가 판마다 다르다 (최소 1곳 이동)",
          moved,
          " / ".join("판%d %s" % (i + 1, [(x, z) for x, z in k]) for i, k in enumerate(keys)))

    # ---------- ③ 상점 개수가 맵 규칙과 맞다 ----------
    counts = [s["count"] for s in shots]
    lens = [len(s["spots"]) for s in shots]
    check("③ 창고 맵(반경 60)은 상점 4곳",
          all(c == WAREHOUSE_SHOP_COUNT for c in counts) and counts == lens,
          "개수 %s · 좌표 수 %s (기대 %d)" % (counts, lens, WAREHOUSE_SHOP_COUNT))

    # ---------- ④ 상점끼리 붙어 서지 않는다 ----------
    # 기준은 **원반 지름**이다. 목표 간격은 25 m지만 좁은 맵에서는 반으로 줄여 재시도하므로
    # (ShopSpotLogic.GAP_RELAX_STEPS) 25를 기대값으로 박으면 재시도가 걸린 판에서 거짓 실패가 난다.
    # 원반이 겹치면 그건 "다른 곳"이 아니다 — 그 바닥만 못 박는다.
    floor = last["radius"] * 2.0
    worst, worst_round = float("inf"), 0
    for i, s in enumerate(shots):
        for a in range(len(s["spots"])):
            for b in range(a + 1, len(s["spots"])):
                (ax, az), (bx, bz) = s["spots"][a], s["spots"][b]
                d = math.hypot(ax - bx, az - bz)
                if d < worst:
                    worst, worst_round = d, i + 1
    check("④ 상점끼리 원반이 안 겹친다",
          worst >= floor,
          "가장 가까운 쌍 %.1f m (판%d) · 바닥 %.1f m · 목표 간격 25 m" % (worst, worst_round, floor))

    # ---------- ⑤ 품절 2개 · 면제 3종은 안 뜬다 ----------
    bad_exempt = [t for s in shots for t in s["soldout"] if t in NEVER_SOLD_OUT]
    bad_unknown = [t for s in shots for t in s["soldout"] if t not in MOUSE_CATALOG]
    sizes = [len(s["soldout"]) for s in shots]
    # 클론도 **같은 답**을 계산해야 한다 — 목록을 복제하지 않고 시드만 복제하기 때문이다.
    clone_sold = parse_sold_out(m1i.invoke("TestGetSoldOutTechs"))
    check("⑤ 오늘 품절 2개 · 면제 3종 없음 · 호스트=클론 동일",
          all(n == SOLD_OUT_COUNT for n in sizes) and not bad_exempt and not bad_unknown
          and clone_sold == last["soldout"],
          "판별 %s · 면제혼입 %s · 미상 %s · 호스트 %s vs 클론 %s"
          % (sizes, bad_exempt or "없음", bad_unknown or "없음",
             ",".join(last["soldout"]), ",".join(clone_sold)))
    if len(set(s["soldout"] for s in shots)) < 2:
        note("⑤' 품절 조합이 판마다 안 바뀌었다",
             "%s — 7 중 2라 조합이 21개뿐이다(우연일 수 있다). 시드는 %s로 갈렸다"
             % ([",".join(s["soldout"]) for s in shots], seeds))

    # ---------- ⑨(먼저) 전직이 아직 안 열려 있다 ----------
    # **치즈를 뿌리기 전에** 재야 한다 — TestGrantCheeseAll이 고양이 지갑도 1000 올려
    # 누적을 문턱 위로 넘겨 버린다. 순서를 바꾸면 "닫혀 있었다"를 영영 못 본다.
    earned0 = host.invoke("TestGetEarnedOf", _clientId=cat_id)
    can0 = bool(host.invoke("TestGetCanAdvance"))
    class0 = host.invoke("TestGetCatClass")

    # ---------- ⑥⑦ 상점 안에서의 구매 ----------
    for _ in range(3):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)

    host.invoke("TestWarpToNearestShop", _clientId=m1)
    in_shop = poll(lambda: bool(host.invoke("TestIsInShop", _clientId=m1)), lambda v: v, limit=8.0)

    sold = last["soldout"][0] if last["soldout"] else ""
    control = next((t for t in CONTROL_CANDIDATES if t not in last["soldout"]), "")

    # ⑥ 품절 테크 — 레벨도 치즈도 **안 움직여야** 한다.
    lv_before = host.invoke("TestGetTechLevel", _clientId=m1, _techId=sold)
    cheese_before = host.invoke("TestGetCheeseOf", _clientId=m1)
    m1i.invoke("TestBuyTechLocal", _techId=sold)
    time.sleep(2.0)                       # 거절은 "아무 일도 안 일어난다"라 기다릴 조건이 없다
    lv_after = host.invoke("TestGetTechLevel", _clientId=m1, _techId=sold)
    cheese_after = host.invoke("TestGetCheeseOf", _clientId=m1)
    check("⑥ 품절 테크는 상점 안에서도 못 산다 (레벨 0 유지 · 차감 0)",
          bool(sold) and in_shop and lv_before == 0 and lv_after == 0
          and cheese_after == cheese_before,
          "%s Lv%s→%s · 치즈 %s→%s (차감 %s) · 상점 안=%s"
          % (sold or "?", lv_before, lv_after, cheese_before, cheese_after,
             cheese_before - cheese_after if cheese_before is not None else "?", in_shop))

    # ⑦ 대조군 — 같은 자리·같은 지갑으로 품절 아닌 것은 사진다.
    cost = host.invoke("TestGetTechCost", _techId=control, _level=1)
    c_before = host.invoke("TestGetCheeseOf", _clientId=m1)
    m1i.invoke("TestBuyTechLocal", _techId=control)
    c_lv = poll(lambda: host.invoke("TestGetTechLevel", _clientId=m1, _techId=control),
                lambda v: v is not None and v >= 1, limit=6.0)
    c_after = host.invoke("TestGetCheeseOf", _clientId=m1)
    check("⑦ 품절이 아닌 테크는 같은 자리에서 사진다 (대조군)",
          bool(control) and c_lv == 1 and c_before - c_after == cost,
          "%s Lv%s · 가격 %s · 치즈 %s→%s (차감 %s)"
          % (control or "?", c_lv, cost, c_before, c_after,
             c_before - c_after if c_before is not None else "?"))

    # ---------- ⑧ 직업권 자리가 판마다 다르다 ----------
    tickets = [s["tickets"] for s in shots]
    loots = [s["loot"] for s in shots]
    check("⑧ 직업권 자리가 판마다 다르다 · 맵 치즈 총량은 불변",
          len(set(tickets)) >= 2 and len(set(loots)) == 1 and all(":|" not in t for t in tickets),
          "자리 %s · 치즈 총량 %s" % (tickets, loots))

    # ---------- ⑨ 고양이가 300 벌면 전직이 열린다 ----------
    earned1 = poll(lambda: host.invoke("TestGetEarnedOf", _clientId=cat_id),
                   lambda v: v is not None and v >= ADVANCE_MISCHIEF, limit=6.0)
    can1 = poll(lambda: bool(host.invoke("TestGetCanAdvance")), lambda v: v, limit=6.0)
    host.invoke("TestChooseCatClass", _id="Fire")
    class1 = poll(lambda: host.invoke("TestGetCatClass"), lambda v: v not in ("None", "", None), limit=6.0)
    check("⑨ 고양이는 누적 %d 미만이면 전직이 닫히고, 넘기면 열린다" % ADVANCE_MISCHIEF,
          earned0 < ADVANCE_MISCHIEF and not can0 and class0 == "None"
          and earned1 >= ADVANCE_MISCHIEF and can1 and class1 == "Fire",
          "누적 %s(가능=%s 전직=%s) → %s(가능=%s 전직=%s) · 문턱=%s"
          % (earned0, can0, class0, earned1, can1, class1,
             host.invoke("TestGetAdvanceMischief")))

    # ---------- ⑩ 봇 쥐가 상점 반경 안에 들어간다 ----------
    # 봇이 굶으면 상점에 갈 이유가 없다(MouseBotLogic.WantsShopping은 돈이 있을 때만 참).
    # 그래서 파밍을 기다리지 않고 지갑을 채운 뒤 **걸어가는 것만** 잰다.
    host.invoke("TestSpawnBots", _mice=3, _cats=0)
    bots = poll(lambda: host.invoke("TestGetBotCount"), lambda v: v is not None and v > 0, limit=10.0)
    for _ in range(2):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.4)

    def reached():
        raw = host.invoke("TestGetBotShopStats") or ""
        got = dict(kv.split("=") for kv in raw.split() if "=" in kv)
        return raw, int(float(got.get("reached", 0))), int(float(got.get("seekers", 0)))

    stats = poll(lambda: reached(), lambda v: v[1] >= 1, limit=120.0, step=2.0)
    raw, n_reached, n_seekers = stats
    check("⑩ 봇 쥐가 상점 반경 안까지 걸어 들어간다",
          bots and bots > 0 and n_reached >= 1,
          "봇 %s마리 · %s · 진단=%s" % (bots, raw, host.invoke("TestGetBotDiagnostics")))
    if n_seekers > n_reached:
        note("⑩' 상점을 향했지만 못 닿은 봇이 있다",
             "seekers=%s reached=%s — 그 판의 봇 수치는 밸런스가 아니라 길찾기를 재고 있다"
             % (n_seekers, n_reached))

    # ---------- 곁가지: 미니맵이 바뀐 상점 자리를 따라왔나 ----------
    # 계획이 "확인만 한다"고 적어 둔 것 — MinimapMarkers가 ShopSpot.All을 주기로 읽으므로
    # 자리가 바뀌어도 저절로 따라와야 한다.
    mini = poll(lambda: host.invoke("TestGetMinimapMarkerCount", _kind="shop"),
                lambda v: v == last["count"], limit=5.0)
    note("곁가지: 미니맵 상점 마커 = 씬의 상점 수",
         "미니맵 %s · 씬 %s" % (mini, last["count"]))

    host.invoke("TestClearBots")

    return summary(aborted=bool(DEGRADED))


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
