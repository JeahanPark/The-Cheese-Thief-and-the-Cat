# -*- coding: utf-8 -*-
"""단판제 — 대기실에서 연 판이 끝나면 결과를 보여 주고 대기실로 돌아간다 (2026-09-15).

**혼자 돈다.** 클론이 필요 없다 — 대기실에서 봇을 채워 판을 열면 사람은 호스트 하나다.
전제: 메인 에디터가 플레이모드이고 **아무 판도 안 열려 있어야 한다**(`Room.unity`부터 시작).
    이 스위트가 직접 방을 만들고 봇을 채우고 강제 시작한다.

## 왜 바뀌었나

예전엔 3판 시리즈의 판 전이(봇 재투입 P0-4 · 단계 초기화 P0-5 · 시작 실패 복구 P0-6)를 쟀다.
사용자 결정으로 **한 판 한 판**이 됐다 — 판을 넘기며 상태를 이어 붙이는 코드 자체가 사라졌고,
다음 판은 대기실에서 씬째로 새로 연다. 그래서 이제 재는 것은 「끝나면 대기실로 돌아가는가」와
「돌아간 대기실에서 다음 판이 처음처럼 열리는가」다.

## 무엇을 재는가

  ⓪ 대기실에서 봇 판을 열면 「대기실에서 온 판」으로 켜지고 봇이 선다
  ① 시간 만료로 끝나면 결과 패널이 뜨고 **재시작 버튼은 없다**(대기실이 다음 판을 연다)
  ② 결과를 보여 준 뒤 **대기실로 돌아간다**
  ③ 돌아간 대기실에서 다시 시작하면 판이 **처음처럼** 열린다 — 단계 Hunt · 봇 수 같음
  ④ 2단계(깡패판)에서 깡패가 아닌 쥐가 이기면 결과가 「GANG DOWN! MICE WIN」이다
  ⑤ 그 판도 끝나면 대기실로 돌아간다
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 6      # ⓪ 전제 + ①~⑤


def check(name, ok, detail="", case=None):
    # `case`는 QA 케이스 대장(`QaCases.cs`)의 영구 ID다. 붙이면 출력이 곧
    # 「무엇이 언제 돌았나」가 되고, 선언과 어긋나면 대장 드리프트 테스트가 깨진다.
    if case:
        name = "[%s] %s" % (case, name)
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def note(name, detail):
    EXTRA.append((name, detail))
    print("[NOTE] " + name + "  " + detail)
    sys.stdout.flush()


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def start_from_room(host, bots=4):
    """대기실에서 봇을 채워 강제 시작 → (대기실 봇 수, 판에 선 봇 수, 대기실에서 온 판인가)."""
    host.invoke("TestSetRoomBots", _count=bots)
    time.sleep(1.0)
    room_bots = host.invoke("TestGetRoomBots")
    host.invoke("TestRoomForceStart")
    time.sleep(12.0)     # 씬 전환 + AutoStart(참가 대기·맵·판) + 봇 1초
    return room_bots, host.invoke("TestGetBotCount"), host.invoke("TestGetRoomReturn") == "True"


def end_by_timer(host):
    """시간 만료로 판을 끝낸다 → 결과 문자열. 결과가 뜰 때까지 기다린다(상한 20초)."""
    host.invoke("TestExpireTimer")
    return wait_result(host)


def wait_result(host, limit=20.0):
    t0 = time.time()
    while time.time() - t0 < limit:
        result = host.invoke("TestGetMatchResult")
        if result not in ("None", "Missing"):
            return result
        time.sleep(0.5)
    return host.invoke("TestGetMatchResult")


def result_panel(host, limit=5.0):
    """결과 패널이 떠 있는 동안의 (문구, 재시작 버튼 -1/0/1). 문구가 보일 때까지 기다린다."""
    t0 = time.time()
    text, restart = "", -1
    while time.time() - t0 < limit:
        text = host.invoke("TestGetResultText")
        restart = host.invoke("TestGetResultRestartVisible")
        if text:
            break
        time.sleep(0.3)
    return text, restart


def wait_scene(host, name, limit=20.0):
    """그 씬이 될 때까지 기다린다 → (씬, 걸린 초)."""
    t0 = time.time()
    scene = ""
    while time.time() - t0 < limit:
        scene = host.invoke("TestGetActiveSceneName")
        if scene == name:
            time.sleep(2.0)     # 대기실이 서고 방 상태가 복제될 여유
            return scene, time.time() - t0
        time.sleep(0.5)
    return scene, time.time() - t0


def main():
    host = WesPoco(instance=0)

    # ---------- 대기실에서 봇 판을 연다 ----------
    # 로비의 「방 만들기」 → 대기실 → 봇 채우기 → 시작. 사람이 밟는 그 길이다.
    # **이 길로 온 판만** `MatchHandoff.FromRoom`이라 대기실로 돌아간다 — `Game.unity`를 혼자 열면
    # `MatchRoomReturn`이 아예 안 켜진다. Room.unity를 직접 로드하면 호스트가 안 서서 `RoomSession`이 안 뜬다.
    host.invoke("TestLoadScene", _sceneName="Lobby")
    time.sleep(3.5)
    host.invoke("TestCreateRoom")
    time.sleep(6.0)     # 호스트 기동 + Room.unity 로드
    print("대기실 진입: 씬=%s" % host.invoke("TestGetActiveSceneName"))

    room_bots, bots1, from_room = start_from_room(host)
    ok_setup = check("⓪ 대기실 봇 판이 열렸다 — 대기실에서 온 판 · 봇이 섰다",
                     from_room and bots1 >= 1 and host.invoke("TestGetMatchResult") == "None"
                     and host.invoke("TestGetPhase") == "Hunt",
                     "대기실 봇=%s · 대기실 판=%s · 선 봇=%s · 단계=%s · 씬=%s"
                     % (room_bots, from_room, bots1, host.invoke("TestGetPhase"),
                        host.invoke("TestGetActiveSceneName")))
    if not ok_setup:
        print("!! 판이 안 열렸다 — Room.unity에서 시작했는지, 봇이 켜졌는지 본다")
        return summary(aborted=True)

    # ---------- ① 결과 패널 · 재시작 버튼 없음 ----------
    r1 = end_by_timer(host)
    text1, restart1 = result_panel(host)
    # 패널이 안 떠 있으면 버튼도 꺼져 있어 거짓 통과한다 — 결과 문구가 보이는 순간의 값만 인정한다.
    check("① 끝나면 결과 패널이 뜨고 재시작 버튼은 없다",
          bool(text1) and restart1 == 0,
          "결과=%s · 문구=%r · 재시작 버튼=%s(-1 없음·0 숨김·1 보임)" % (r1, text1[:30], restart1), case="A-03")

    # ---------- ② 대기실로 돌아간다 ----------
    scene1, took1 = wait_scene(host, "Room")
    check("② 결과를 보여 준 뒤 대기실로 돌아간다",
          scene1 == "Room", "씬=%s · %.1f초" % (scene1, took1), case="A-26")
    if scene1 != "Room":
        return summary(aborted=True)

    # ---------- ③ 다음 판이 처음처럼 열린다 ----------
    room_bots2, bots2, from_room2 = start_from_room(host)
    phase2 = host.invoke("TestGetPhase")
    check("③ 돌아간 대기실에서 다시 시작하면 판이 처음처럼 열린다 — 단계 Hunt · 봇 수 같음",
          from_room2 and phase2 == "Hunt" and bots2 == bots1
          and host.invoke("TestGetMatchResult") == "None",
          "대기실 판=%s · 단계=%s · 봇 %s→%s" % (from_room2, phase2, bots1, bots2), case="A-26")

    # ---------- ④ 깡패가 아닌 쥐의 승리 ----------
    # 정상 경로(깡패 쥐 + 고양이 사망)는 봇 판에서 못 만든다 — 봇에는 clientId가 없어
    # `TestSetMouseJob`이 안 닿는다. 그래서 **전제만** 훅으로 세운다(`TestForceGangPhase`).
    # 깡패 팀이 0이라 다음 판정 틱에 「깡패 전멸 → 나머지 쥐 승」으로 끝난다.
    forced = bool(host.invoke("TestForceGangPhase"))
    time.sleep(1.0)
    phase_gang = host.invoke("TestGetPhase")
    # 단계는 판정 틱(0.5초) 안에 끝나 버릴 수 있어 단계 이름이 아니라 전제가 섰는지로 가른다.
    if forced:
        r2 = wait_result(host)
        text2, _ = result_panel(host)
        check("④ 2단계에서 깡패가 아닌 쥐가 이기면 결과가 따로 뜬다",
              r2 == "MiceBeatGang" and text2 == "GANG DOWN! MICE WIN",
              "force=%s · 단계=%s · 결과=%s · 문구=%r" % (forced, phase_gang, r2, text2))
        scene2, took2 = wait_scene(host, "Room")
        check("⑤ 그 판도 끝나면 대기실로 돌아간다",
              scene2 == "Room", "씬=%s · %.1f초" % (scene2, took2), case="A-26")
    else:
        skip("④ 2단계에서 깡패가 아닌 쥐가 이기면 결과가 따로 뜬다",
             "2단계 전제를 못 세웠다(force=%s · 단계=%s)" % (forced, phase_gang))
        skip("⑤ 그 판도 끝나면 대기실로 돌아간다", "④가 안 돌았다")

    note("P0-6(인원 이탈 복구)", "판을 넘기는 코드가 사라져 해당 경로가 없다 — 끝나면 무조건 대기실로 간다")
    return summary()


def summary(aborted=False):
    """선언 항목 수와 실제 실행 수를 대조해 채점한다. 안 돈 항목은 통과가 아니다."""
    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  | " + detail if detail else ""))
    for name, detail in EXTRA:
        print("  NOTE " + name + ("  | " + detail if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    unrun = EXPECTED - len(RESULTS)
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d · NOTE %d"
          % (passed, failed, skipped, unrun, EXPECTED, len(EXTRA)))
    if unrun > 0:
        print("!! 선언 %d · 실행 %d — 안 돈 항목은 통과가 아니다" % (EXPECTED, len(RESULTS)))
    if unrun < 0:
        print("!! 실행 %d > 선언 %d — EXPECTED를 고쳐라" % (len(RESULTS), EXPECTED))
    if aborted:
        print("!! 중단·열화된 실행이다 — 통과로 읽지 마라")
    return 0 if (failed == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
