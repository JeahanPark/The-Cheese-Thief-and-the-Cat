# -*- coding: utf-8 -*-
"""봇 판을 N번 돌려 승률 표를 뽑는다 (2026-08-11).

    python run_matches.py --matches 20 --mice 8 --cats 2 --map 2 --speed 5

전제: 메인 에디터가 **플레이모드**. 클론도 빌드도 필요 없다 — 봇은 서버에서 도니까
에디터 하나로 2:8 판이 성립한다.

**나오는 건 봇 승률이지 사람 승률이 아니다.** 봇은 속임수(미끼)와 협동(몰이)을 못 한다.
구조적으로 불가능한 구간을 걸러내는 도구지 5:5를 확정해 주지 않는다.
"""
import argparse, csv, os, sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

OUT_DIR = r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\docs\balance"
FIELDS = ["match", "result", "remaining", "mice_left", "cats_left",
          "first_sight", "jailed", "phase", "warning"]


def parse():
    p = argparse.ArgumentParser()
    p.add_argument("--matches", type=int, default=10)
    p.add_argument("--mice", type=int, default=8)
    p.add_argument("--cats", type=int, default=2)
    p.add_argument("--map", type=int, default=2)      # 0 기본 · 1 확장 · 2 대형
    p.add_argument("--speed", type=float, default=5.0)
    p.add_argument("--timeout", type=float, default=900.0)   # 실시간 상한(초)
    return p.parse_args()


def open_lobby(host, map_index):
    """로비를 몰아 매치를 연다. 봇만 쓸 거라도 **사람 역할이 하나는 있어야** 시작된다."""
    if host.invoke("TestGetRole") in ("Cat", "Mouse"):
        return True

    # 이 러너의 호스트는 역할을 안 잡으므로 TestGetRole로는 호스팅 여부를 모른다.
    # 이미 호스팅 중일 때 TestStartHost를 다시 부르면 로비 상태가 어긋나
    # 1판이 80~100초에 cats=0으로 끝난다(2026-08-12 실측) — 판을 다시 여는 걸로 대체.
    if host.invoke("TestGetLocalClientId") >= 0:
        host.invoke("TestClearBots")
        host.invoke("TestRestartMatch")
        time.sleep(3.0)
    else:
        host.invoke("TestStartHost")
        time.sleep(4.0)
    host.invoke("TestSelectMap", _index=map_index)
    time.sleep(0.8)
    # 호스트는 고양이로 두되 **가만히 있는다** — 사람이 조작하지 않으므로 판에 개입하지 않는다.
    # 봇 고양이는 여기에 더해진다(그래서 --cats 2면 실제 고양이는 3이 아니라 2+1이다. 아래 주석 참조)
    # **역할을 고르지 않는다.** 사람이 판에 서면 가만히 있어도 승패가 뒤집힌다 —
    # 쥐면 시간 만료로 쥐가 이기고, 고양이면 사냥 안 하는 고양이가 하나 더 있는 셈이다.
    host.invoke("TestForceStartGame")
    time.sleep(4.0)
    return True


def run_one(host, args, index):
    # 재시작은 **로비로 돌려보낼 뿐**이다 — 다시 시작을 눌러야 판이 선다(2026-08-11 실측:
    # 안 누르면 남은 시간 -1로 영원히 안 끝난다). 역할은 그대로 남아 있다.
    if index > 1:
        host.invoke("TestRestartMatch")
        time.sleep(3.0)
        host.invoke("TestForceStartGame")
        time.sleep(4.0)

    host.invoke("TestSpawnBots", _mice=args.mice, _cats=args.cats)
    time.sleep(2.0)
    host.invoke("TestSetTimeScale", _scale=args.speed)

    started = time.time()
    while True:
        time.sleep(2.0)
        summary = host.invoke("TestGetMatchSummary")
        result = summary.split(",")[0]
        if result != "None":
            break
        if time.time() - started > args.timeout:
            summary = summary.replace("None", "Timeout", 1)
            break

    host.invoke("TestResetTimeScale")

    # 이상 징후 — 숫자만 보면 안 보이는 것들. 벽에 낀 봇이 있으면 그 판의 승률은 못 믿는다.
    diag = host.invoke("TestGetBotDiagnostics")
    warning = ""
    stuck = [c for c in diag.split(" ") if c.endswith("/0m")]
    if stuck:
        warning = f"제자리봇 {len(stuck)}"
    if ",-1.0," in summary or summary.endswith(",-1.0"):
        warning = (warning + " 고양이가 한 번도 못 봄").strip()

    host.invoke("TestClearBots")
    return summary.split(",") + [warning], diag


def main():
    args = parse()
    host = WesPoco(instance=0)

    if not open_lobby(host, args.map):
        print("로비 진행 실패 — 플레이모드인지 확인")
        return

    # **사람이 남아 있으면 측정이 오염된다**(2026-08-11 실측). MPPM 클론이 붙어 있으면
    # 조작하지 않아도 쥐 역할을 자동으로 잡고 스폰 지점에 서 있는다 —
    # 고양이 봇이 그 자리를 즉시 보고("첫 발견 0.5초"), 시간이 다 되면 그 클론 덕에 쥐가 이긴다.
    humans = host.invoke("TestGetMouseCount")
    if humans > 0:
        print(f"경고: 사람 쥐가 {humans}명 붙어 있다. 클론을 끄고 에디터 하나로 돌려야 한다.")
        print("      (이대로 돌리면 승률·첫 발견 수치를 믿을 수 없다)")

    os.makedirs(OUT_DIR, exist_ok=True)
    stamp = time.strftime("%Y-%m-%d_%H%M")
    path = os.path.join(OUT_DIR, f"bots_{args.cats}v{args.mice}_{stamp}.csv")

    rows = []
    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(FIELDS)

        for i in range(1, args.matches + 1):
            row, diag = run_one(host, args, i)
            writer.writerow([i] + row)
            f.flush()
            rows.append(row)
            print(f"[{i:2d}/{args.matches}] {row[0]:8s} 남은{row[1]:>4s}s "
                  f"쥐{row[2]} 고양이{row[3]} 첫발견{row[4]}s 갇힘{row[5]} {row[6]} {row[7]}")
            sys.stdout.flush()

    print(f"\n→ {path}")
    summarize(rows)


def summarize(rows):
    if not rows:
        return

    total = len(rows)
    wins = {}
    for r in rows:
        wins[r[0]] = wins.get(r[0], 0) + 1

    print("\n===== 요약 =====")
    for name, count in sorted(wins.items(), key=lambda kv: -kv[1]):
        print(f"  {name:10s} {count:3d} / {total}  ({count * 100 // total}%)")

    sights = [float(r[4]) for r in rows if float(r[4]) >= 0]
    if sights:
        print(f"  첫 발견까지 평균 {sum(sights) / len(sights):.1f}초 "
              f"(최소 {min(sights):.0f} · 최대 {max(sights):.0f})")
    print(f"  고양이가 한 번도 못 본 판: {total - len(sights)} / {total}")


main()
