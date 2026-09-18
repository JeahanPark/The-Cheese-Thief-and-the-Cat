# -*- coding: utf-8 -*-
"""접속 대기 로딩창 — 대기실에서 게임으로 넘어가면 판이 설 때까지 가리고, 서면 걷힌다 (2026-09-19). MPPM 3인.

전제: 메인 에디터가 **Intro.unity**에서 플레이모드, 클론 2개도 Intro. 아무도 방을 안 연 상태.
(한 번 돌리면 판이 시작된다 — 다시 돌리려면 플레이모드를 껐다 켠다.)

## 왜 이 스위트가 있는가

로딩창의 판정(`LoadingScreenLogic` — 판이 섰나 · 최소 · 상한)은 EditMode가 덮는다(A-28).
여기서 재는 것은 **배선**이다 — 로딩창은 대기실 추첨 연출의 끝에서 **각 프로세스가 스스로** 띄우고(`RoomDrawStage`)
씬을 건너와야 하며(DontDestroyOnLoad), 걷는 신호는 복제값(`GameStarted`)이라 **전원에게** 와야 한다.
하나라도 빠지면 EditMode는 초록인 채로 클라 화면만 영영 가려지거나(걷는 신호 누락), 아예 안 뜬다(띄우는 자리 누락).

**첫 판(게임 씬 캔버스에 붙어 있던 것)은 이 스위트에서 한 번도 안 잡혔다**(2026-09-19) — 게임 씬이 선 지 0.4초 안에
판이 서서 걷혔고, 그 앞 씬 로딩 동안은 검은 화면이었다. 그래서 대기실에서 띄워 씬을 건너오게 바꿨다.

기획: 사용자 시안 5(2026-09-19) — 로비 배경 같은 그림 한 장 + 오른쪽 아래 「로딩중」과 점(. → .. → ...).
"""
import os, sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 5      # ⓪ 전제 + ①~④

CAPTURE = os.path.join(os.environ.get("TEMP", "."), "ctc-loading-screen.png")


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다(qa_v52와 같은 규약).
    if case:
        name = "[%s] %s" % (case, name)
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def fields(line):
    out = {}
    for part in (line or "").split():
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def gate(inst):
    return fields(inst.invoke("TestGetGateState"))


def wait_for(fn, timeout, interval=0.3):
    t0 = time.time()
    while time.time() - t0 < timeout:
        value = fn()
        if value:
            return value
        time.sleep(interval)
    return fn()


def main():
    host = WesPoco(instance=0)
    c1 = WesPoco(instance=1)
    c2 = WesPoco(instance=2)
    everyone = (host, c1, c2)

    scenes = [p.invoke("TestGetActiveSceneName") for p in everyone]
    if not check("⓪ 전제 — 셋 다 Intro · 아직 방이 없다",
                 scenes == ["Intro"] * 3 and gate(host).get("server") == "False",
                 "씬=%s" % scenes):
        return summary(aborted=True)

    # 비밀번호 없는 방 — 클론의 자동 접속이 그대로 붙는다(qa_v56은 비밀번호로 막는 쪽을 잰다).
    host.invoke("TestFrontDoorHost", _password="")
    in_room = wait_for(lambda: all(p.invoke("TestGetActiveSceneName") == "Room" for p in everyone), 30.0, 0.8)
    clients = gate(host).get("clients")
    check("① 방을 열면 셋이 대기실에 모인다",
          in_room and clients == "3", "씬=%s · 호스트 인원=%s"
          % ([p.invoke("TestGetActiveSceneName") for p in everyone], clients))
    time.sleep(1.5)

    # ---------- ② 시작하면 **셋 다** 로딩창이 뜨고, 씬을 건너 게임 씬에서도 가리고 있다 ----------
    # 추첨 연출(4.2초)이 검게 닫히는 순간 대기실 씬에서 뜨고, 게임 씬이 로드되는 동안 계속 가린다.
    host.invoke("TestRoomForceStart")
    seen_scenes = {0: set(), 1: set(), 2: set()}
    dots = {0: set(), 1: set(), 2: set()}
    captured = False
    t0 = time.time()
    while time.time() - t0 < 20.0:
        done = True
        for i, p in enumerate(everyone):
            f = fields(p.invoke("TestGetLoadingScreen"))
            if f.get("showing") == "True":
                seen_scenes[i].add(f.get("scene"))
                dots[i].add(f.get("dots", ""))
                if i == 0 and not captured and f.get("scene") == "Game":
                    host.invoke("TestCaptureGameView", _path=CAPTURE)
                    captured = True
            done = done and ("Game" in seen_scenes[i] or (bool(seen_scenes[i]) and f.get("showing") == "False"))
        if done and all(seen_scenes.values()):
            break
        time.sleep(0.1)
    check("② 대기실에서 시작하면 **클라까지** 셋 다 로딩창이 뜬다 — 씬을 건너 게임 씬에서도 가린다",
          all(seen_scenes.values()) and any("Game" in s for s in seen_scenes.values()),
          "본 씬=%s" % {i: sorted(s) for i, s in seen_scenes.items()}, case="A-29")

    # ---------- ③ 점이 돈다 ----------
    t0 = time.time()
    while time.time() - t0 < 2.0:
        for i, p in enumerate(everyone):
            f = fields(p.invoke("TestGetLoadingScreen"))
            if f.get("showing") == "True":
                dots[i].add(f.get("dots", ""))
        time.sleep(0.2)
    turning = [i for i in dots if len(dots[i] - {""}) >= 2]
    check("③ 「로딩중」 옆 점이 . → .. → ...으로 돈다",
          len(turning) >= 1, "점 모양=%s" % {i: sorted(d) for i, d in dots.items()}, case="A-29")

    # ---------- ④ 판이 서면 전원 화면에서 걷힌다 ----------
    lifted = wait_for(lambda: all(fields(p.invoke("TestGetLoadingScreen")).get("showing") == "False"
                                  for p in everyone), 30.0, 0.5)
    roles = [p.invoke("TestGetRole") for p in everyone]
    check("④ 판이 서면(전원 접속) 셋 다 로딩창이 걷히고 역할이 선다",
          lifted and all(r in ("Cat", "Mouse") for r in roles),
          "걷힘=%s · 역할=%s · 캡처=%s" % (lifted, roles, CAPTURE if captured else "없음"), case="A-29")

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
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d"
          % (passed, failed, skipped, unrun, EXPECTED))
    if unrun > 0:
        print("!! 선언 %d · 실행 %d — 안 돈 항목은 통과가 아니다" % (EXPECTED, len(RESULTS)))
    if unrun < 0:
        print("!! 실행 %d > 선언 %d — EXPECTED를 고쳐라" % (len(RESULTS), EXPECTED))
    if aborted:
        print("!! 중단·열화된 실행이다 — 통과로 읽지 마라")
    return 0 if (failed == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
