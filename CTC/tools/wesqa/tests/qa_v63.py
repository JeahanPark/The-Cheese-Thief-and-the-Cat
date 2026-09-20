# -*- coding: utf-8 -*-
"""2026-09-20 테스트 피드백 — 카메라 가림 · 미니맵 방향 · 고양이 접근 추격곡.

**혼자 돈다.** 대기실에서 봇 1을 채워 판을 연다(개발 빌드 또는 에디터 플레이). 내가 고양이로 뽑히면
끝내고 대기실에서 다시 시작한다 — 후보가 둘뿐이라 직전 고양이도 다시 걸릴 수 있어 몇 번 돌린다.
전제: 메인(Intro)이나 대기실(Room)에서 시작한다. 판이 열려 있으면 안 된다.

## 무엇을 재는가

  ⓪ 판이 열렸고 내가 쥐다(전제)
  ① 미니맵 내 삼각형은 **몸** 방향을 따라 돈다(끝이 빨강 — 사용자 요청)
  ② 부채꼴은 **카메라** 방향을 따라 돈다 — 카메라만 돌리면 삼각형은 그대로다
  ③ 선반 너머에서 나를 보면 **선반 판벽(맵 셰이더)**이 반투명해진다 — 예전엔 절대 안 흐려졌다
  ④ 같은 자리에서 **선반 가구(URP Lit)**도 반투명해진다
  ⑤ 산 고양이가 반경 밖(40m)이면 추격곡이 아니다
  ⑥ 살금살금 고양이는 14m에선 안 걸리고 8m에선 걸린다(반경 20m의 절반)
  ⑦ 벗어나면 바로 끝나지 않고 3초를 버틴 뒤 돌아온다

추격곡 음원(bgm_chase)이 아직 없어도 ⑤~⑦은 돈다 — 판정만 재고 소리는 안 본다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 8      # ⓪ 전제 + ①~⑦


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다.
    if case:
        name = "[%s] %s" % (case, name)
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def fields(text):
    """`a=1 b=x` 한 줄 → 사전."""
    out = {}
    for part in (text or "").split():
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def angle_gap(a, b):
    d = abs(float(a) - float(b)) % 360.0
    return min(d, 360.0 - d)


def wait_scene(host, name, limit=40.0):
    t0 = time.time()
    while time.time() - t0 < limit:
        if host.invoke("TestGetActiveSceneName") == name:
            return True
        time.sleep(0.5)
    return False


def open_as_mouse(host, tries=4):
    """대기실에서 봇 1로 판을 연다 — 쥐가 될 때까지(→ 역할)."""
    if host.invoke("TestGetActiveSceneName") != "Room":
        host.invoke("TestFrontDoorHost", _password="")
        wait_scene(host, "Room", 25)
        time.sleep(3.0)
    host.invoke("TestSetRoomBots", _count=1)
    time.sleep(1.0)
    role = ""
    for _ in range(tries):
        host.invoke("TestRoomClickStart")
        wait_scene(host, "Game", 40)
        t0 = time.time()
        while time.time() - t0 < 40 and host.invoke("TestGetPhase") != "Hunt":
            time.sleep(0.5)
        time.sleep(2.0)
        role = host.invoke("TestGetRole")
        if role == "Mouse":
            return role
        host.invoke("TestExpireTimer")
        wait_scene(host, "Room", 40)
        time.sleep(3.0)
    return role


def chase(host):
    return fields(host.invoke("TestGetChaseBgm"))


def main():
    host = WesPoco(instance=0)

    role = open_as_mouse(host)
    if not check("⓪ 판이 열렸고 내가 쥐다", role == "Mouse", "역할=%s" % role):
        return summary(aborted=True)

    # ---------- ①② 미니맵 방향 ----------
    host.invoke("TestSetCameraAngles", _yaw=0.0, _pitch=30.0)
    host.invoke("TestWalkDirection", _x=0.0, _z=-1.0, _seconds=0.8)   # 남쪽 — 몸이 180을 본다
    time.sleep(1.2)
    h1 = fields(host.invoke("TestGetMinimapHeading"))
    body_ok = angle_gap(h1.get("body", -1), (360.0 - float(h1.get("bodyYaw", 0))) % 360.0) < 2.0
    check("① 미니맵 삼각형이 몸 방향을 따라 돈다", body_ok,
          "삼각형 %s° · 몸 yaw %s°" % (h1.get("body"), h1.get("bodyYaw")), case="C-43")

    host.invoke("TestSetCameraAngles", _yaw=90.0, _pitch=30.0)
    time.sleep(0.6)
    h2 = fields(host.invoke("TestGetMinimapHeading"))
    view_ok = angle_gap(h2.get("view", -1), (360.0 - float(h2.get("camYaw", 0))) % 360.0) < 2.0
    still = angle_gap(h2.get("body", -1), h1.get("body", -1)) < 2.0
    check("② 부채꼴은 카메라 방향을 따라 돌고, 카메라만 돌리면 삼각형은 그대로다", view_ok and still,
          "부채꼴 %s° · 카메라 yaw %s° · 삼각형 %s→%s°"
          % (h2.get("view"), h2.get("camYaw"), h1.get("body"), h2.get("body")), case="C-43")

    # ---------- ③④ 선반 너머에서 나를 본다 ----------
    staged = host.invoke("TestStageShelfOcclusion", _gap=0.6, _pitch=20.0)
    time.sleep(1.2)
    probe = host.invoke("TestProbeOcclusion") or ""
    faded = probe.split("faded=", 1)[-1].split("|", 1)[0]
    wall_faded = "Wall_Shelf" in faded and "CTC/MapTriplanar q=3000 t=True" in faded
    check("③ 선반 너머에서 보면 선반 판벽(맵 셰이더)이 반투명해진다", wall_faded,
          "%s · 흐린 것: %s" % (staged, faded[:160]), case="B-38")
    check("④ 같은 자리에서 선반 가구(URP Lit)도 반투명해진다",
          "Universal Render Pipeline/Lit q=3000 t=True" in faded, faded[:160], case="B-38")

    # ---------- ⑤⑥⑦ 추격곡 판정 ----------
    host.invoke("TestSetBotSneak", _role="Cat", _on=False)
    host.invoke("TestTeleportAtRangeFrom", _role="Cat", _meters=40.0, _yawOffset=0.0, _botOnly=True)
    time.sleep(4.0)
    c_far = chase(host)
    check("⑤ 산 고양이가 반경 밖(40m)이면 추격곡이 아니다", c_far.get("chasing") == "False",
          host.invoke("TestGetChaseBgm"), case="B-42")

    host.invoke("TestSetBotSneak", _role="Cat", _on=True)
    time.sleep(0.4)
    host.invoke("TestTeleportAtRangeFrom", _role="Cat", _meters=14.0, _yawOffset=0.0, _botOnly=True)
    time.sleep(0.6)
    c_s14 = chase(host)
    host.invoke("TestTeleportAtRangeFrom", _role="Cat", _meters=8.0, _yawOffset=0.0, _botOnly=True)
    time.sleep(0.6)
    c_s8 = chase(host)
    check("⑥ 살금살금 고양이는 14m에선 안 걸리고 8m에선 걸린다",
          c_s14.get("chasing") == "False" and c_s8.get("chasing") == "True",
          "14m %s(반경 %s) · 8m %s" % (c_s14.get("chasing"), c_s14.get("trigger"), c_s8.get("chasing")), case="B-42")

    host.invoke("TestSetBotSneak", _role="Cat", _on=False)
    host.invoke("TestTeleportAtRangeFrom", _role="Cat", _meters=40.0, _yawOffset=0.0, _botOnly=True)
    time.sleep(0.6)
    c_hold = chase(host)
    time.sleep(3.5)
    c_after = chase(host)
    check("⑦ 벗어나면 3초를 버틴 뒤 돌아온다",
          c_hold.get("chasing") == "True" and c_after.get("chasing") == "False",
          "0.6초 뒤 %s · 4.1초 뒤 %s" % (c_hold.get("chasing"), c_after.get("chasing")), case="B-42")
    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다. 안 돈 항목은 통과가 아니다."""
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  | " + detail if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    unrun = EXPECTED - len(RESULTS)
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d" % (passed, failed, skipped, unrun, EXPECTED))
    if unrun > 0:
        print("!! 선언 %d · 실행 %d — 안 돈 항목은 통과가 아니다" % (EXPECTED, len(RESULTS)))
    if aborted:
        print("!! 중단·열화된 실행이다 — 통과로 읽지 마라")
    return 0 if (failed == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
