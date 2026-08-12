# -*- coding: utf-8 -*-
"""레이아웃의 최장 직선 시야를 잰다 — 0.5m 격자 스캔 (2026-08-12 도구화).

    python tools/sightscan.py [Assets/Prefabs/Maps/Layout_Large01.asset] [--top 10]

**왜 재는가**: 고양이 이속 7.8m/s에서 직선 시야 L미터는 첫 발견까지 L/7.8초라는 뜻이다.
27.5m면 2초 — 발소리 18m·파밍 32m 같은 소리 값이 아무 일도 하기 전에 눈이 이긴다.
소리 밸런스를 재려면 최장 직선을 12~15m로 눌러야 한다.

**모델**: 눈높이 1.0m 기준 2D. 열린 칸 = 방 내부 ∪ 복도(중심선 ± 폭/2).
막는 것 = 방 벽(3m)·복도 벽(1.6m)·기둥(2.2m, m_Obstacles). 벽감·요새·창고는
**안 센다** — 판 중에 부서지는 것들이라, 시야는 최악(다 부서진 상태)으로 잰다.

축 정렬(가로·세로)만 잰다 — 격자 맵의 긴 줄은 전부 축 위에 있다.
"""
import argparse
import io
import math
import re
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

STEP = 0.5


def parse_layout(path):
    text = open(path, encoding="utf-8").read()

    def vec2_list(section, fields):
        """'m_Rooms:' 같은 섹션에서 각 항목의 필드를 뽑는다."""
        m = re.search(rf"{section}:\n(.*?)(?=\n  m_|\Z)", text, re.S)
        if not m:
            return []
        body = m.group(1)
        items = re.split(r"\n  - ", "\n" + body)
        out = []
        for item in items[1:]:
            row = {}
            for f in fields:
                fm = re.search(rf"{f}: \{{x: ([-\d.e]+), y: ([-\d.e]+)\}}", item)
                if fm:
                    row[f] = (float(fm.group(1)), float(fm.group(2)))
                else:
                    fm = re.search(rf"{f}: ([-\d.e]+)", item)
                    if fm:
                        row[f] = float(fm.group(1))
            out.append(row)
        return out

    half = float(re.search(r"m_HalfExtent: ([-\d.]+)", text).group(1))
    rooms = vec2_list("m_Rooms", ["Center", "Size"])
    corridors = vec2_list("m_Corridors", ["From", "To", "Width"])
    obstacles = vec2_list("m_Obstacles", ["Pos", "Size"])
    return half, rooms, corridors, obstacles


def build_grid(half, rooms, corridors, obstacles):
    n = int((half * 2) / STEP) + 1
    coord = lambda i: -half + i * STEP

    open_cell = [[False] * n for _ in range(n)]   # [iy][ix]

    for r in rooms:
        (cx, cy), (sx, sy) = r["Center"], r["Size"]
        x0, x1 = cx - sx / 2, cx + sx / 2
        y0, y1 = cy - sy / 2, cy + sy / 2
        for iy in range(max(0, int((y0 + half) / STEP)), min(n, int((y1 + half) / STEP) + 1)):
            y = coord(iy)
            if not (y0 < y < y1):
                continue
            for ix in range(max(0, int((x0 + half) / STEP)), min(n, int((x1 + half) / STEP) + 1)):
                if x0 < coord(ix) < x1:
                    open_cell[iy][ix] = True

    for c in corridors:
        (fx, fy), (tx, ty) = c["From"], c["To"]
        w = c.get("Width", 3.0) / 2
        dx, dy = tx - fx, ty - fy
        length = math.hypot(dx, dy)
        if length < 0.01:
            continue
        ux, uy = dx / length, dy / length
        x0, x1 = min(fx, tx) - w, max(fx, tx) + w
        y0, y1 = min(fy, ty) - w, max(fy, ty) + w
        for iy in range(max(0, int((y0 + half) / STEP)), min(n, int((y1 + half) / STEP) + 1)):
            for ix in range(max(0, int((x0 + half) / STEP)), min(n, int((x1 + half) / STEP) + 1)):
                px, py = coord(ix) - fx, coord(iy) - fy
                s = px * ux + py * uy
                if s < 0 or s > length:
                    continue
                d = abs(px * uy - py * ux)
                if d < w:
                    open_cell[iy][ix] = True

    for o in obstacles:
        (cx, cy), (sx, sy) = o["Pos"], o["Size"]
        x0, x1 = cx - sx / 2, cx + sx / 2
        y0, y1 = cy - sy / 2, cy + sy / 2
        for iy in range(max(0, int((y0 + half) / STEP)), min(n, int((y1 + half) / STEP) + 1)):
            y = coord(iy)
            if not (y0 <= y <= y1):
                continue
            for ix in range(max(0, int((x0 + half) / STEP)), min(n, int((x1 + half) / STEP) + 1)):
                if x0 <= coord(ix) <= x1:
                    open_cell[iy][ix] = False

    return open_cell, n, coord


def longest_runs(open_cell, n, coord):
    runs = []
    for iy in range(n):
        run = 0
        for ix in range(n + 1):
            if ix < n and open_cell[iy][ix]:
                run += 1
            else:
                if run > 1:
                    runs.append((run * STEP, "가로", coord(iy),
                                 coord(ix - run), coord(ix - 1)))
                run = 0
    for ix in range(n):
        run = 0
        for iy in range(n + 1):
            if iy < n and open_cell[iy][ix]:
                run += 1
            else:
                if run > 1:
                    runs.append((run * STEP, "세로", coord(ix),
                                 coord(iy - run), coord(iy - 1)))
                run = 0
    runs.sort(reverse=True)
    return runs


def main():
    p = argparse.ArgumentParser()
    p.add_argument("layout", nargs="?",
                   default="Assets/Prefabs/Maps/Layout_Large01.asset")
    p.add_argument("--top", type=int, default=10)
    args = p.parse_args()

    half, rooms, corridors, obstacles = parse_layout(args.layout)
    open_cell, n, coord = build_grid(half, rooms, corridors, obstacles)
    runs = longest_runs(open_cell, n, coord)

    print(f"{args.layout}")
    print(f"맵 {half * 2:.0f}×{half * 2:.0f} · 방 {len(rooms)} · 복도 {len(corridors)} · 기둥 {len(obstacles)}")
    print(f"\n최장 직선 {runs[0][0]:.1f}m — 상위 {args.top}:")
    for length, axis, fixed, a, b in runs[:args.top]:
        line = f"y={fixed:.1f}  x {a:.1f}→{b:.1f}" if axis == "가로" else f"x={fixed:.1f}  y {a:.1f}→{b:.1f}"
        print(f"  {length:5.1f}m  {axis}  {line}")


if __name__ == "__main__":
    main()
