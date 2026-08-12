# -*- coding: utf-8 -*-
"""봇 판 하나를 라이브로 관찰한다 — 수감이 일어나는지, 일어나면 어떻게 풀리는지 (2026-08-12).

    python watch_match.py [--mice 8] [--cats 2] [--map 2] [--speed 5]

승률 표만 보면 "구출이 싸다"와 "애초에 못 잡는다"가 같은 숫자(수감 0)로 보인다.
2초마다 수감 수·행동 분포를 찍어 그 둘을 가른다.
"""
import argparse, io, re, sys, time

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

ROW = re.compile(r"#(\d+)/([MC])/(\w+)/")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--mice", type=int, default=8)
    p.add_argument("--cats", type=int, default=2)
    p.add_argument("--map", type=int, default=2)
    p.add_argument("--speed", type=float, default=5.0)
    args = p.parse_args()

    host = WesPoco(instance=0)

    if host.invoke("TestGetLocalClientId") >= 0:
        host.invoke("TestClearBots")
        host.invoke("TestRestartMatch"); time.sleep(3.0)
    else:
        host.invoke("TestStartHost"); time.sleep(4.0)
    host.invoke("TestSelectMap", _index=args.map); time.sleep(0.8)
    host.invoke("TestForceStartGame"); time.sleep(4.0)

    host.invoke("TestSpawnBots", _mice=args.mice, _cats=args.cats)
    time.sleep(2.0)
    host.invoke("TestSetTimeScale", _scale=args.speed)

    jail_events = 0
    prev_jailed = 0
    peak_jailed = 0
    started = time.time()
    try:
        while True:
            time.sleep(2.0)
            summary = host.invoke("TestGetMatchSummary").split(",")
            result, remaining = summary[0], summary[1]
            jailed = host.invoke("TestGetJailedCount")
            if jailed > prev_jailed:
                jail_events += jailed - prev_jailed
            peak_jailed = max(peak_jailed, jailed)
            prev_jailed = jailed

            acts = {}
            for _n, role, act in ROW.findall(host.invoke("TestGetBotDiagnostics")):
                acts[f"{role}:{act}"] = acts.get(f"{role}:{act}", 0) + 1
            cat_acts = " ".join(f"{k[2:]}x{v}" for k, v in sorted(acts.items()) if k.startswith("C"))
            mice_alive = sum(v for k, v in acts.items() if k.startswith("M"))

            print(f"[{time.time()-started:5.0f}s] 남은{remaining:>4s} 수감{jailed} "
                  f"(누적 {jail_events}) 쥐{mice_alive} | 고양이: {cat_acts}")
            sys.stdout.flush()

            if result != "None":
                print(f"\n결과 {result} · 수감 발생 누적 {jail_events} · 동시 최대 {peak_jailed}")
                break
            if time.time() - started > 400:
                print("\n시간 상한 — 관찰 종료")
                break
    finally:
        host.invoke("TestResetTimeScale")
        host.invoke("TestClearBots")


main()
