# -*- coding: utf-8 -*-
"""로비를 몰아 매치를 연다 — 회귀 스위트(qa_v36~v44)의 전제를 스크립트로 만든다.

사용: python setup_match.py [맵인덱스] [호스트역할] [인원]
  맵인덱스  0=기본(30×30) · 1=확장(42×42) · 2=대형(120×120)
  호스트역할 cat(기본) · mouse(qa_v37용, 클론 하나가 고양이) · cat2(고양이 2 — qa_v44용)
  인원      3(기본) · 4(클론 3개 — 먼저 에디터 메뉴 'MPPM 4인 세팅'으로 클론을 켜야 한다)

클론은 MppmAutoConnect가 붙이고 쥐를 자동 선택한다 — 여기서는 호스트 역할과
맵만 정하고, 필요하면 클론 하나를 고양이로 돌린다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

map_index = int(sys.argv[1]) if len(sys.argv) > 1 else 0
host_role = sys.argv[2] if len(sys.argv) > 2 else "cat"
player_count = int(sys.argv[3]) if len(sys.argv) > 3 else 3

p = [WesPoco(instance=i) for i in range(player_count)]

if p[0].invoke("TestGetRole") in ("Cat", "Mouse"):
    print("이미 매치 중 — 그대로 쓴다")
else:
    p[0].invoke("TestStartHost")
    time.sleep(4.0)
    p[0].invoke("TestSelectMap", _index=map_index)
    time.sleep(0.8)
    # 고양이 자리만 정하고 나머지는 쥐 — 인원이 늘어도 조합이 안 불어난다
    cats = {"mouse": [1], "cat2": [0, 1]}.get(host_role, [0])
    for i in range(player_count):
        p[i].invoke("TestSelectRoleCat" if i in cats else "TestSelectRoleMouse")
    time.sleep(1.5)
    p[0].invoke("TestStartGame")
    time.sleep(4.0)

roles = [x.invoke("TestGetRole") for x in p]
print("역할:", roles)
print("맵:", p[0].invoke("TestGetMapShape"))
