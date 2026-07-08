# coding=utf-8
"""wesqa 전 기능 순차 검증(acceptance) — 로그인→로비→방→인게임→Invoke→시각→리포트.
플레이모드에서 실행. 각 스텝 PASS/FAIL + HTML 리포트(스크린샷 포함) 생성.
사용: python -m bench.verify_all"""
import os
import sys
import time

ROOT = os.path.dirname(os.path.dirname(__file__))
sys.path.insert(0, ROOT)
from wesqa import WesPoco          # noqa: E402
from wesqa.report import Report    # noqa: E402


def connect(retries=8):
    last = None
    for _ in range(retries):
        try:
            g = WesPoco(instance=0)
            g.sdk_version()
            return g
        except Exception as e:
            last = e
            time.sleep(1.0)
    raise RuntimeError("connect failed: %s" % last)


def node_names(g):
    root = g.agent.rpc.call("Dump", True).wait()[0]
    out = set()

    def w(n):
        out.add((n.get("payload") or {}).get("name"))
        for c in (n.get("children") or []):
            w(c)

    w(root)
    return out


def shot_b64(g, width=640):
    b64, _ = g.agent.screen.getScreen(width)
    return b64


def main():
    rep = Report("wesqa 순차 검증 (M1~M4 + 버그수정)")
    results = []

    def step(name, ok, note="", shot=None):
        results.append((name, ok))
        rep.step(name, ok, note=note, screenshot_b64=shot)
        print(("PASS" if ok else "FAIL"), name, ("| " + note) if note else "")

    g = connect()

    # M1: 핸드셰이크 + 로그인 화면 읽기
    step("M1 핸드셰이크", g.sdk_version() == "wesqa-0.1", g.sdk_version())
    login = node_names(g)
    step("M1 로그인 UI 읽기(StartButton)", "StartButton" in login,
         "nodes=%d" % len(login), shot=shot_b64(g))

    # M2: 입력 — 로그인→로비
    g("StartButton").click(); time.sleep(1.0)
    lobby = node_names(g)
    step("M2 클릭→로비 전환", "RoomCreateButton" in lobby and "StartButton" not in lobby,
         "RoomCreateButton 등장")

    # M2: 로비→방
    g("RoomCreateButton").click(); time.sleep(1.0)
    room = node_names(g)
    step("M2 방 생성 화면", "StartGameButton" in room, "StartGameButton 등장")

    # M2 + 버그수정: 방→인게임 (InGameColliderWorker NRE 레이스 구간)
    g("StartGameButton").click(); time.sleep(4.0)
    g = connect()  # 씬 전환 후 재연결
    ingame = node_names(g)
    in_ok = any(n in ingame for n in ("PlayerStatusHUD", "QuickSlotHUD", "InGameHUDWorker"))
    step("인게임 진입(버그수정 구간)", in_ok,
         "in-game HUD 노드=%s" % [n for n in ("PlayerStatusHUD", "QuickSlotHUD") if n in ingame],
         shot=shot_b64(g))

    # M4: Invoke 실효 — 인벤토리 토글
    before = node_names(g)
    g.invoke("SimulateInventoryToggle"); time.sleep(0.8)
    after = node_names(g)
    cells = sorted(n for n in (after - before) if n and "Cell" in n)
    step("M4 Invoke 실효(인벤토리 토글)", len(cells) > 0,
         "등장 Cell=%d개" % len(cells), shot=shot_b64(g))

    # M3: 인게임 시각 검증 — 자기 템플릿 매칭
    try:
        import cv2
        from wesqa import vision
        img = g.screenshot()
        h, w = img.shape[:2]
        crop = img[h // 2 - 30:h // 2 + 30, w // 2 - 60:w // 2 + 60]
        res = vision.find_template(img, crop, threshold=0.8)
        conf = None if res is None else res.get("confidence")
        step("M3 이미지 매칭(self-template)", res is not None and conf > 0.9,
             "confidence=%.4f" % (conf or 0))
    except Exception as e:
        step("M3 이미지 매칭(self-template)", False, "err: %s" % str(e)[:50])

    # geom_compare 자동 시각 게이트 — 골든 baseline 대비 결정론적 채점.
    # 최초 실행 시 bench/golden/ingame.png 를 baseline으로 자동 생성, 이후 회귀 비교.
    try:
        from wesqa.visual_compare import visual_gate
        ref = os.path.join(ROOT, "bench", "golden", "ingame.png")
        vg = visual_gate(g.screenshot(), ref, threshold=0.50)
        step("시각 게이트(geom_compare)", vg["passed"], vg.get("note", ""),
             shot=shot_b64(g))
    except Exception as e:
        step("시각 게이트(geom_compare)", False, "err: %s" % str(e)[:60])

    out = os.path.join(ROOT, "bench", "verify_report.html")
    rep.write(out)
    passed = sum(1 for _, ok in results if ok)
    print("\n=== %d/%d PASS ===" % (passed, len(results)))
    print("report:", out)


if __name__ == "__main__":
    main()
