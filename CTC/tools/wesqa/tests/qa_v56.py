# -*- coding: utf-8 -*-
"""대기실 앞단 **접속 관문** — 비밀번호 방 · 틀림/맞음 · 시작 후 난입 거절. MPPM 3인.

전제: 메인 에디터가 **Intro.unity**에서 플레이모드, 클론 2개도 Intro. 아무도 방을 안 연 상태.
(한 번 돌리면 판이 시작된다 — 다시 돌리려면 플레이모드를 껐다 켠다.)

## 왜 이 스위트가 있는가

2026-09-14 R.E.P.O.식 앞단에서 **모든 접속이 호스트의 NGO 승인(`ConnectionGate`)을 지나게** 됐다.
판정 자체는 EditMode(`FrontDoorLogicTests`)가 덮지만, 그건 「승인 콜백이 실제로 걸렸나」,
「클라이언트가 비밀번호를 실제로 실어 보내나」, 「대기실이 서면 열리고 시작하면 닫히나」를 못 본다.
**설치가 빠지면 EditMode는 초록인 채로 누구나 들어온다** — 그걸 여기서 잡는다.

스팀이 없으니 로컬 UTP 경로다. 관문은 트랜스포트와 무관해 스팀 경로와 같은 판정을 탄다.

## 사람 손 경로와의 차이

비밀번호·방 이름 팝업은 사람용이라 `TestFrontDoorHost` / `TestFrontDoorJoin`이 팝업만 건너뛰고
`SessionStarter`의 같은 절차(`HostLocalOrFail` · `StartLocalClient` → `AwaitClientResult`)를 부른다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 7      # ⓪ 전제 + ① 비밀번호 방 + ② 자동 접속 거절 + ③ 틀림 + ④ 맞음 + ⑤ 시작하면 닫힘 + ⑥ 난입 거절

PASSWORD = "CHEESE"
# 거절 코드 — **여기 글자가 곧 계약이다**(ConnectionGateLogic 상수를 훅으로 되읽지 않는다).
PW_REQUIRED, PW_WRONG, CLOSED = "PW_REQUIRED", "PW_WRONG", "CLOSED"


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다(qa_v52와 같은 규약).
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


def gate(inst):
    """`TestGetGateState` 한 줄 → dict."""
    raw = inst.invoke("TestGetGateState") or ""
    out = {}
    for part in raw.split(" "):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def wait_for(fn, timeout, interval=0.5):
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

    scenes = [p.invoke("TestGetActiveSceneName") for p in (host, c1, c2)]
    g0 = gate(host)
    if not check("⓪ 전제 — 셋 다 Intro · 호스트는 아직 방이 없다 · 승인이 켜져 있다",
                 scenes == ["Intro"] * 3 and g0.get("server") == "False" and g0.get("approval") == "True",
                 "씬=%s · 호스트=%s" % (scenes, g0)):
        return summary(aborted=True)

    # ---------- ① 비밀번호 방 ----------
    host.invoke("TestFrontDoorHost", _password=PASSWORD)
    in_room = wait_for(lambda: host.invoke("TestGetActiveSceneName") == "Room", 15.0)
    time.sleep(1.5)
    g1 = gate(host)
    check("① 비밀번호 방을 열면 대기실로 가고, 대기실이 서서 입장이 열린다",
          in_room and g1.get("hostPw") == "True" and g1.get("accepting") == "True" and g1.get("server") == "True",
          "씬=%s · %s" % (host.invoke("TestGetActiveSceneName"), g1), case="A-21")

    # ---------- ② 클론 자동 접속은 비밀번호 없이 두드린다 → 거절 ----------
    # 대기실 씬이 서는 몇 초 동안은 CLOSED로 거절된다(정상) — 자동 접속이 다시 두드려 PW_REQUIRED가 될 때까지 본다.
    r1 = wait_for(lambda: gate(c1).get("reject") == PW_REQUIRED and PW_REQUIRED, 20.0) or gate(c1).get("reject")
    check("② 비밀번호 없이 온 자동 접속은 PW_REQUIRED로 거절되고 붙지 않는다",
          r1 == PW_REQUIRED and gate(c1).get("connected") == "False" and gate(host).get("clients") == "1",
          "클론1 reject=%s · 호스트 인원=%s" % (r1, gate(host).get("clients")), case="A-21")
    time.sleep(2.5)   # 자동 접속이 멈추고 세션이 내려갈 때까지

    # ---------- ③ 틀린 비밀번호 ----------
    c1.invoke("TestFrontDoorJoin", _password="WRONG")
    r3 = wait_for(lambda: gate(c1).get("reject"), 12.0)
    check("③ 틀린 비밀번호는 PW_WRONG — 호스트 인원이 그대로다",
          r3 == PW_WRONG and gate(host).get("clients") == "1" and c1.invoke("TestGetActiveSceneName") == "Intro",
          "클론1 reject=%s · 씬=%s · 호스트 인원=%s"
          % (r3, c1.invoke("TestGetActiveSceneName"), gate(host).get("clients")), case="A-21")
    time.sleep(2.0)

    # ---------- ④ 맞는 비밀번호(소문자·공백 섞어도 같다) ----------
    c1.invoke("TestFrontDoorJoin", _password=" chee se ")
    joined = wait_for(lambda: c1.invoke("TestGetActiveSceneName") == "Room", 15.0)
    check("④ 맞는 비밀번호(대소문자·공백 무시)면 대기실에 합류한다",
          joined and gate(host).get("clients") == "2" and gate(c1).get("connected") == "True",
          "클론1 씬=%s · 호스트 인원=%s" % (c1.invoke("TestGetActiveSceneName"), gate(host).get("clients")), case="A-21")

    # ---------- ⑤ 시작하면 문이 닫힌다 ----------
    host.invoke("TestRoomForceStart")
    closed = wait_for(lambda: gate(host).get("accepting") == "False", 5.0)
    check("⑤ 방장이 시작하면 입장이 닫힌다", closed, "호스트=%s" % gate(host), case="A-22")

    # ---------- ⑥ 시작 후 난입 ----------
    time.sleep(2.5)   # 클론2의 자동 접속이 멈췄는지 한 번 더 기다린다
    c2.invoke("TestFrontDoorJoin", _password=PASSWORD)
    r6 = wait_for(lambda: gate(c2).get("reject") in (CLOSED,), 12.0)
    time.sleep(1.0)
    check("⑥ 시작한 방에는 맞는 비밀번호로도 못 들어온다 — CLOSED",
          r6 and gate(c2).get("connected") == "False" and c2.invoke("TestGetActiveSceneName") == "Intro",
          "클론2 %s · 씬=%s" % (gate(c2), c2.invoke("TestGetActiveSceneName")), case="A-22")

    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다. 안 돈 항목은 통과가 아니다."""
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  " + detail if detail else ""))

    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    unrun = max(0, EXPECTED - len(RESULTS))
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d"
          % (passed, failed, skipped, unrun, EXPECTED))
    return 0 if (failed == 0 and skipped == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
