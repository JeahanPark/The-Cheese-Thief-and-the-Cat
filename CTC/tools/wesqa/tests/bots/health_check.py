# -*- coding: utf-8 -*-
"""봇이 **정상으로 도는가**를 먼저 통과시킨다 (2026-08-12).

    python health_check.py [--mice 8] [--cats 2] [--map 2] [--seconds 90]

전제: 메인 에디터가 **플레이모드**.

왜 필요한가
-----------
2026-08-12에 봇 승률을 세 번 쟀는데 셋 다 못 썼다.
- 고양이가 3.6×0.8m 폴리곤 조각에 갇혀 판 내내 못 나왔다
- 둘이 같은 목표만 쫓아 탐색 범위가 한 마리분이었다
- 길을 다시 짜는 카운터가 안 깎여 목표에 닿는 순간 그 자리에 섰다

**숫자는 셋 다 정상으로 보였다.** 승률만 보면 어느 쪽도 이상해 보이지 않는다.
그래서 승률을 읽기 전에 이걸 먼저 통과시킨다 — 하나라도 빨간불이면 그 판의 승률은 안 쓴다.
"""
import argparse, io, re, sys, time

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

# 레이아웃에서 잰 최장 직선 열린 구간(2026-08-12: 27.5m) + 여유.
# 이보다 멀리서 "봤다"면 시야 판정이 벽을 통과한 것이다.
MAX_HONEST_SIGHT = 35.0

SAMPLE_INTERVAL = 0.5

ROW = re.compile(
    r"#(\d+)/([MC])/(\w+)/(\d+)m/(\w+) corners=(\d+) idx=(\d+) goal=(-?\d+,-?\d+)")

passed, failed = 0, 0


def check(name, ok, detail=""):
    global passed, failed
    if ok:
        passed += 1
    else:
        failed += 1
    print(f"  [{'OK  ' if ok else 'FAIL'}] {name}{('  — ' + detail) if detail else ''}")
    sys.stdout.flush()


def parse(diag):
    """진단 한 줄 → {봇번호: (역할, 상태, 이동거리, 코너수, 인덱스, 목표)}.

    **번호로 잡는다.** 순서로 맞추면 중간에 탈락한 봇 때문에 줄이 밀려 엉뚱한 봇을 잰다
    (2026-08-12: 쥐가 전부 탈락한 판에서 두 마리 기준으로 접혀 오판했다).
    """
    out = {}
    for bot, role, act, dist, _mode, corners, idx, goal in ROW.findall(diag):
        out[int(bot)] = (role, act, int(dist), int(corners), int(idx), goal)
    return out


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--mice", type=int, default=8)
    p.add_argument("--cats", type=int, default=2)
    p.add_argument("--map", type=int, default=2)
    p.add_argument("--seconds", type=float, default=90.0)
    args = p.parse_args()

    host = WesPoco(instance=0)

    # **반드시 새 판에서 잰다.** 남은 판에 붙으면 이미 잡힌 쥐가 빠진 상태를 재게 되고,
    # "고양이가 안 쫓는다"가 아니라 "쫓을 쥐가 없었다"를 잡아낸다.
    if host.invoke("TestGetRole") not in ("Cat", "Mouse"):
        host.invoke("TestStartHost"); time.sleep(4.0)
        host.invoke("TestSelectMap", _index=args.map); time.sleep(1.5)
    else:
        host.invoke("TestClearBots")
        host.invoke("TestRestartMatch"); time.sleep(3.0)

    host.invoke("TestForceStartGame"); time.sleep(4.0)

    humans = host.invoke("TestGetMouseCount")
    if humans > 0:
        print(f"경고: 사람 쥐가 {humans}명 붙어 있다 — 클론을 끄고 에디터 하나로 돌려야 한다.\n")

    print(f"이동 폴리곤: {host.invoke('TestNavMeshIslands')}")

    host.invoke("TestSpawnBots", _mice=args.mice, _cats=args.cats)
    time.sleep(2.0)

    # ---------- 표본 모으기 ----------
    samples = []
    peak_cheese = peak_orders = 0
    first_sight = -1.0
    t0 = time.time()
    while time.time() - t0 < args.seconds:
        samples.append(parse(host.invoke("TestGetBotDiagnostics")))
        # **도는 동안 읽는다.** 끝나고 읽으면 탈락한 뒤라 0이 나온다.
        peak_cheese = max(peak_cheese, host.invoke("TestGetBotMouseCheese"))
        peak_orders = max(peak_orders, host.invoke("TestGetBotOrderCount"))
        if first_sight < 0:
            sight = host.invoke("TestGetBotFirstSightDistance")
            if sight >= 0:
                first_sight = sight
        time.sleep(SAMPLE_INTERVAL)

    # 봇마다 처음/마지막 본 값을 따로 모은다 — 도중에 탈락해도 그때까지의 값이 유효하다
    roles, acts, first_d, last_d, seen = {}, {}, {}, {}, {}
    for snapshot in samples:
        for bot, (role, act, dist, _c, _i, _g) in snapshot.items():
            roles[bot] = role
            acts.setdefault(bot, set()).add(act)
            first_d.setdefault(bot, dist)
            last_d[bot] = dist
            seen[bot] = seen.get(bot, 0) + 1

    mice = sum(1 for r in roles.values() if r == "M")
    cats = sum(1 for r in roles.values() if r == "C")
    print(f"\n{args.seconds:.0f}초 · {len(samples)}표본 · 봇 {len(roles)}마리 (쥐 {mice} · 고양이 {cats})\n")

    check("⓪ 부른 만큼 세워졌다", len(roles) == args.mice + args.cats,
          f"{len(roles)} / {args.mice + args.cats}")

    # ---------- ① 굳은 봇 ----------
    # 갇힌 쥐와 파밍 중인 쥐는 제자리에 있는 게 정상이라 뺀다.
    frozen = [f"{roles[b]}#{b} {last_d[b] - first_d[b]}m"
              for b in roles
              if not acts[b] <= {"Jailed", "Farm"}
              and seen[b] >= 10 and last_d[b] - first_d[b] < 15]
    check("① 판 내내 굳은 봇이 없다", not frozen, " · ".join(frozen))

    # ---------- ② 경로 소진 상태로 오래 있지 않는다 ----------
    # corners == idx 는 "경로를 다 썼는데 목표엔 못 갔다"는 뜻이다. 이어지면 그 봇은 서 있다.
    #
    # **제자리에 있는 게 정답인 상태는 뺀다** — 감옥에서 버티는 쥐(구출), 감옥 앞을 지키는
    # 고양이, 캐는 쥐, 덮치는 고양이는 서 있어야 한다. 그걸 막힘으로 세면 영원히 못 통과한다.
    STANDING_IS_CORRECT = {"Farm", "Jailed", "Rescue", "GuardJail", "Pounce"}

    run, worst = {}, {}
    for snapshot in samples:
        for bot, (_r, act, _d, corners, idx, _g) in snapshot.items():
            spent = corners > 0 and idx >= corners and act not in STANDING_IS_CORRECT
            run[bot] = run.get(bot, 0) + 1 if spent else 0
            worst[bot] = max(worst.get(bot, 0), run[bot])
    stalled = [f"{roles[b]}#{b} {w * SAMPLE_INTERVAL:.0f}s"
               for b, w in worst.items() if w * SAMPLE_INTERVAL >= 3.0]
    check("② 경로 소진 상태가 3초를 넘지 않는다", not stalled, " · ".join(stalled))

    # ---------- ③ 쥐가 실제로 캔다 ----------
    farmed = any("Farm" in acts[b] for b in roles if roles[b] == "M")
    check("③ 쥐가 치즈를 캔다", peak_cheese > 0 and farmed, f"최대 {peak_cheese}")

    # ---------- ④ 고양이가 실제로 친다 ----------
    hunted = set()
    for b in roles:
        if roles[b] == "C":
            hunted |= acts[b]
    check("④ 고양이가 쫓거나 덮친다", bool(hunted & {"Chase", "Pounce"}), " · ".join(sorted(hunted)))

    # ---------- ⑤ 시야가 정직하다 ----------
    check("⑤ 벽 너머로 보지 않는다", 0 <= first_sight <= MAX_HONEST_SIGHT,
          f"첫 발견 거리 {first_sight:.0f}m (최장 직선 {MAX_HONEST_SIGHT:.0f}m)")

    # ---------- ⑥ 고양이 둘이 안 겹친다 ----------
    same = total = 0
    for snapshot in samples:
        goals = [v[5] for v in snapshot.values() if v[0] == "C"]
        if len(goals) >= 2:
            total += 1
            same += len(set(goals)) < len(goals)
    ratio = same / total if total else 0.0
    check("⑥ 고양이 둘의 목표가 겹치지 않는다", ratio < 0.30, f"{same}/{total} = {ratio:.0%}")

    # ---------- ⑦ 움직일 때 제 속도로 움직인다 ----------
    # 최고속: 고양이 7.8 · 쥐 6.0 (PlayerInfo.csv). 상태를 오가느라 100%는 안 나오지만
    # 30%도 안 나오면 어딘가 막히고 있다는 뜻이다.
    for role, top, label in (("C", 7.8, "고양이"), ("M", 6.0, "쥐")):
        ids = [b for b in roles if roles[b] == role]
        if not ids:
            continue
        moved = sum(last_d[b] - first_d[b] for b in ids)
        span = sum(seen[b] for b in ids) * SAMPLE_INTERVAL / len(ids)
        speed = moved / len(ids) / max(span, 1e-3)
        check(f"⑦ {label}가 제 속도로 움직인다", speed >= top * 0.30, f"{speed:.1f} / {top} m/s")

    # ---------- ⑧ 지휘관이 돈다 ----------
    check("⑧ 지휘관이 명령을 낸다", peak_orders > 0, f"{peak_orders}개")

    print(f"\n===== {passed} 통과 / {failed} 실패 =====")
    print("승률을 재려면 여기가 전부 초록이어야 한다." if failed else "봇 정상 — 승률을 재도 된다.")
    host.invoke("TestClearBots")
    sys.exit(1 if failed else 0)


main()
