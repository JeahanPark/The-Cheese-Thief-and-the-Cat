# -*- coding: utf-8 -*-
"""사람 1명 + 봇 — 로비에서 역할을 고른 뒤 실행하면 반대편을 봇으로 채워 판을 연다.

    사용법 (에디터 플레이모드에서):
      1. 게임 뷰에서 호스트 시작 → 맵 선택 → 역할 선택 (고양이든 쥐든)
      2. python tools/wesqa/tests/bots/play_vs_bots.py
      3. 판이 열리고 봇이 선다 — 배속 없음(사람 판은 1배속이어야 한다)

    구성: 내가 고양이면 → 봇 고양이 1 + 봇 쥐 8 (2:8)
          내가 쥐면   → 봇 고양이 2 + 봇 쥐 7 (2:8)
"""
import io
import sys
import time

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

host = WesPoco(instance=0)

role = host.invoke("TestGetRole")
if role not in ("Cat", "Mouse"):
    print("로비에서 역할(고양이/쥐)을 먼저 고르고 다시 실행하세요.")
    print("  (호스트 시작 → 맵 선택 → 역할 선택)")
    sys.exit(1)

host.invoke("TestForceStartGame")
time.sleep(4.0)

# 사람을 포함해 고양이 2 : 쥐 8을 맞춘다
if role == "Cat":
    mice, cats = 8, 1
else:
    mice, cats = 7, 2

host.invoke("TestSpawnBots", _mice=mice, _cats=cats)
time.sleep(1.5)
print(f"판 시작 — 나={role} · 봇 쥐 {mice} · 봇 고양이 {cats} (배속 1x)")
print("끝나면 결과 화면에서 다시 시작을 누르거나, 이 스크립트를 다시 실행하면 된다.")
