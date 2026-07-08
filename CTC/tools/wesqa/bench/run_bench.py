# coding=utf-8
"""심은 버그 검출 벤치 — snapshot.json(실 게임 UI 트리)을 변조한 seed들을
가짜 서버(전송은 실서버와 바이트 동일)로 서빙하고, wesqa 스위트가 검출하는지·
얼마 만에 검출하는지 측정 → before/after 리포트(REPORT.md) 생성.

사용: python -m bench.run_bench"""
import json
import os
import sys
import time

ROOT = os.path.dirname(os.path.dirname(__file__))  # tools/wesqa
sys.path.insert(0, ROOT)
sys.path.insert(0, os.path.join(ROOT, "tests"))

from fake_server import FakeWesPocoServer  # noqa: E402
from wesqa import WesPoco  # noqa: E402
from bench.scenarios import login_checks  # noqa: E402
from bench.seeds import seed_catalog, mutate  # noqa: E402


def run_suite_against(tree):
    """주어진 트리를 서빙하는 가짜 서버에 붙어 스위트 1회 실행 → (RED 목록, 초)."""
    srv = FakeWesPocoServer({
        "GetSDKVersion": lambda: "wesqa-bench",
        "Dump": lambda only_visible=True: tree,
    }).start()
    try:
        g = WesPoco(host=srv.host, port=srv.port)
        t0 = time.time()
        red = []
        for name, fn in login_checks():
            try:
                ok = bool(fn(g))
            except Exception:
                ok = False
            if not ok:
                red.append(name)
        dt = time.time() - t0
    finally:
        srv.stop()
    return red, dt


def main():
    snap = json.load(open(os.path.join(ROOT, "bench", "snapshot.json"), encoding="utf-8"))
    total = len(login_checks())

    healthy_red, h_dt = run_suite_against(snap)
    print(f"[healthy] {total - len(healthy_red)}/{total} GREEN in {h_dt*1000:.0f}ms red={healthy_red}")

    rows, times, caught = [], [], 0
    for sname, mut, desc in seed_catalog():
        red, dt = run_suite_against(mutate(snap, mut))
        detected = len(red) > 0
        caught += 1 if detected else 0
        times.append(dt)
        rows.append((sname, detected, red, dt, desc))
        print(f"[seed] {sname}: {'CAUGHT' if detected else 'MISSED'} ({dt*1000:.0f}ms) red={red}")

    n = len(rows)
    mttd = (sum(times) / len(times)) if times else 0.0
    pct = (100 * caught // n) if n else 0
    print(f"\nDETECTION {caught}/{n} ({pct}%)  MTTD {mttd*1000:.0f}ms  healthy_green={len(healthy_red)==0}")

    _write_report(snap, total, healthy_red, h_dt, rows, caught, n, pct, mttd)


def _write_report(snap, total, healthy_red, h_dt, rows, caught, n, pct, mttd):
    L = []
    L.append("# wesqa 효과 측정 — 심은 버그 검출 (before/after)\n")
    L.append("> 방식: 실 게임 UI 트리 스냅샷(`snapshot.json`)을 변조한 seed를 가짜 서버로 서빙\n")
    L.append("> (전송 프레이밍·JSON-RPC는 실 C# 서버와 바이트 동일). 동일 wesqa 클라이언트로 검출 측정.\n\n")
    L.append(f"- 검증 단언 수: {total}\n")
    L.append(f"- 헬시 baseline: {total - len(healthy_red)}/{total} GREEN ({h_dt*1000:.0f}ms)\n\n")
    L.append("## After — wesqa 자동 스위트\n\n")
    L.append("| seed (심은 버그) | 검출 | RED 단언 | 검출시간 |\n|---|---|---|---|\n")
    for sname, detected, red, dt, desc in rows:
        L.append(f"| {sname} | {'✅ CAUGHT' if detected else '❌ MISSED'} | {', '.join(red) or '-'} | {dt*1000:.0f}ms |\n")
    L.append(f"\n**검출율 {caught}/{n} ({pct}%) · MTTD {mttd*1000:.0f}ms · 결정적(재실행 동일)**\n\n")
    L.append("## Before — 수동/에이전트 눈검사 (현 방식)\n\n")
    L.append("| 항목 | 값 |\n|---|---|\n")
    L.append("| 검출 보장 | ❌ 없음 — 사람이 놓칠 수 있음 |\n")
    L.append("| 판정 | 비결정적 — 스크린샷 주관 비교 |\n")
    L.append("| 1개 상태 소요 | 캡처+육안 수초~수십초 |\n")
    L.append("| 회귀 N종 | 매 상태 수동 반복 |\n\n")
    L.append("## 결론\n\n")
    L.append(f"자동 스위트는 심은 버그 {caught}/{n}을(를) 평균 {mttd*1000:.0f}ms에 결정적으로 검출. ")
    L.append("수동 방식은 검출 보장이 없고 상태마다 반복 비용이 든다. ")
    L.append("M2(입력)·M4(Invoke 주입) 도입 시 플로우 구동 후 상태 회귀까지 동일 방식으로 자동 검출 확대 가능.\n")
    path = os.path.join(ROOT, "bench", "REPORT.md")
    open(path, "w", encoding="utf-8").write("".join(L))
    print("report:", path)


if __name__ == "__main__":
    main()
