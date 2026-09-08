# -*- coding: utf-8 -*-
"""3판 시리즈의 판 전이 — 봇 재투입(P0-4)·단계 초기화(P0-5)·시작 실패 복구(P0-6).

**혼자 돈다.** 클론이 필요 없다 — 대기실에서 봇을 채워 시리즈를 열면 사람은 호스트 하나다.
전제: 메인 에디터가 플레이모드이고 **아무 판도 안 열려 있어야 한다**(`Room.unity`부터 시작).
    이 스위트가 직접 방을 만들고 봇을 채우고 강제 시작한다.

## 왜 이 스위트가 따로 있는가

시리즈는 **판 사이의 전이가 전부**인데 그 전이를 보는 테스트가 하나도 없었다.
그래서 2026-09-09 감사 전까지 다음이 아무 경보 없이 돌았다:

  · `MatchSeries`가 `MatchHandoff.Set`을 봇 인자 없이 불러 2판부터 `BotMice/BotCats = 0`.
    게다가 봇을 다시 세우는 코드가 아예 없었다(`MatchAutoStart`는 씬 진입 1회뿐) → **P0-4**
  · `PhaseDirector.ServerReset()`이 호출자 0인 죽은 코드라, 2단계(깡패)로 끝난 판 뒤에
    단계가 Gang으로 남아 다음 판이 시작 즉시 끝날 수 있었다 → **P0-5**
  · 다음 판 시작이 거부되면 조용히 아무 일도 안 해 빈 씬에 갇혔다 → **P0-6**

## 무엇을 재는가

  ① 대기실에서 봇 판을 열면 시리즈가 켜지고 1판에 봇이 선다
  ② 1판을 시간 만료로 끝내면 2판이 열린다 — **봇 수가 1판과 같다** ← P0-4
  ③ 2판의 단계가 Hunt다(1판이 남긴 것이 없다)
  ④ 2판을 2단계(깡패)로 끝낸 뒤 3판의 단계가 Hunt로 되돌아간다 ← P0-5
  ⑤ 3판에도 봇이 선다(2판에서 다시 세운 것이 3판에도 이어진다) ← P0-4
  ⑥ 시리즈 중에는 결과 패널의 재시작 버튼이 안 뜬다(이중 재시작 방지)

⑥은 호스트 화면의 버튼 상태라 훅이 없으면 못 잰다 — 없으면 SKIP으로 남긴다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 7      # ⓪ 전제 + ①~⑥


def check(name, ok, detail=""):
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


def series(host):
    """"round|catWins|mouseWins|active" → (판, 고양이승, 쥐승, 시리즈인가). 못 읽으면 (-1,…)."""
    raw = host.invoke("TestGetSeries")
    if not isinstance(raw, str) or "|" not in raw:
        return -1, -1, -1, False
    r, cw, mw, act = raw.split("|")
    return int(r), int(cw), int(mw), act.lower() == "true"


def wait_round(host, want, limit=60.0):
    """그 판 번호가 되고 판이 실제로 열릴 때까지 기다린다 → (판, 봇 수, 단계, 걸린 초).

    판 전환은 결과 5초 + 한 프레임 + 봇 1초라 최소 6초가 걸린다. 봇이 서는 것까지
    본 뒤에 돌려줘야 「2판에 봇이 0」을 「아직 안 섰다」와 헷갈리지 않는다.
    """
    t0 = time.time()
    while time.time() - t0 < limit:
        rnd = series(host)[0]
        if rnd == want and host.invoke("TestGetMatchResult") == "None":
            time.sleep(2.0)     # 봇 재투입(1초 지연)이 끝날 여유
            return rnd, host.invoke("TestGetBotCount"), host.invoke("TestGetPhase"), time.time() - t0
        time.sleep(1.0)
    return series(host)[0], host.invoke("TestGetBotCount"), host.invoke("TestGetPhase"), time.time() - t0


def end_by_timer(host):
    """시간 만료로 판을 끝낸다 → 결과 문자열. 결과가 뜰 때까지 기다린다(상한 20초)."""
    host.invoke("TestExpireTimer")
    t0 = time.time()
    while time.time() - t0 < 20.0:
        result = host.invoke("TestGetMatchResult")
        if result not in ("None", "Missing"):
            return result
        time.sleep(0.5)
    return host.invoke("TestGetMatchResult")


def main():
    host = WesPoco(instance=0)

    # ---------- 대기실에서 봇 판을 연다 ----------
    # 로비의 「방 만들기」 → 대기실 → 봇 채우기 → 시작. 사람이 밟는 그 길이다.
    # **이 길로 온 판만** `MatchHandoff.FromRoom`이라 시리즈가 돈다 — `Game.unity`를 혼자 열면
    # `MatchSeries`가 아예 안 켜지고(`m_Active=false`) 이 스위트가 재는 전이가 존재하지 않는다.
    # Room.unity를 직접 로드하면 안 된다: 호스트가 안 서서 `RoomSession`이 스폰되지 않는다.
    host.invoke("TestLoadScene", _sceneName="Lobby")
    time.sleep(3.5)
    host.invoke("TestCreateRoom")
    time.sleep(6.0)     # 호스트 기동 + Room.unity 로드
    print("대기실 진입: 씬=%s" % host.invoke("TestGetActiveSceneName"))
    host.invoke("TestSetRoomBots", _count=4)
    time.sleep(1.0)
    room_bots = host.invoke("TestGetRoomBots")
    host.invoke("TestRoomForceStart")
    time.sleep(12.0)     # 씬 전환 + AutoStart(참가 대기·맵·판) + 봇 1초

    rnd1, cw, mw, active = series(host)
    bots1 = host.invoke("TestGetBotCount")
    ok_setup = check("⓪ 대기실 봇 판이 시리즈로 열렸다 — 1판 · 봇이 섰다",
                     active and rnd1 == 1 and bots1 >= 1
                     and host.invoke("TestGetMatchResult") == "None",
                     "대기실 봇=%s · 시리즈=%s 판=%s (%s:%s) · 선 봇=%s · 씬=%s"
                     % (room_bots, active, rnd1, cw, mw, bots1,
                        host.invoke("TestGetActiveSceneName")))
    if not ok_setup:
        print("!! 시리즈가 안 열렸다 — Room.unity에서 시작했는지, 봇이 켜졌는지 본다")
        return summary(aborted=True)

    check("① 1판 — 단계가 Hunt이고 봇이 판에 섰다",
          host.invoke("TestGetPhase") == "Hunt" and bots1 >= 1,
          "단계=%s · 봇 %s" % (host.invoke("TestGetPhase"), bots1))

    # ---------- ② 1판을 시간 만료로 끝내고 2판을 본다  ← P0-4 ----------
    r1 = end_by_timer(host)
    rnd2, bots2, phase2, took = wait_round(host, 2)
    check("② 2판이 열리고 **봇 수가 1판과 같다**  ← P0-4",
          rnd2 == 2 and bots2 == bots1,
          "1판 결과=%s · 2판=%s · 봇 %s→%s · %.1f초" % (r1, rnd2, bots1, bots2, took))

    # ---------- ③ 2판의 단계가 Hunt다 ----------
    check("③ 2판 단계가 Hunt다(1판이 남긴 단계가 없다)",
          phase2 == "Hunt",
          "단계=%s" % phase2)

    # ---------- ④ 2판을 2단계로 끝낸 뒤 3판의 단계가 되돌아간다  ← P0-5 ----------
    # 정상 경로(깡패 쥐 + 고양이 사망)는 봇 판에서 못 만든다 — 봇에는 clientId가 없어
    # `TestSetMouseJob`이 안 닿는다. 그래서 **전제만** 훅으로 세운다(`TestForceGangPhase`).
    # 재는 것은 그대로다: 2단계에서 끝난 판 다음이 Hunt로 돌아오는가.
    forced = bool(host.invoke("TestForceGangPhase"))
    time.sleep(1.0)
    phase_gang = host.invoke("TestGetPhase")
    if phase_gang == "Gang":
        # 2단계는 깡패 팀이 0이라 다음 판정 틱(0.5초)에 스스로 끝난다 — 시계를 태워 확실히 한다.
        host.invoke("TestBurnPhaseClock")
        r2 = end_by_timer(host)
        rnd3, bots3, phase3, took3 = wait_round(host, 3)
        check("④ 2단계로 끝난 뒤 3판 단계가 Hunt로 되돌아간다  ← P0-5",
              rnd3 == 3 and phase3 == "Hunt",
              "2판 결과=%s(2단계) · 3판=%s 단계=%s · %.1f초" % (r2, rnd3, phase3, took3))
        check("⑤ 3판에도 봇이 선다  ← P0-4",
              rnd3 == 3 and bots3 == bots1,
              "봇 1판 %s → 3판 %s" % (bots1, bots3))
    else:
        skip("④ 2단계로 끝난 뒤 3판 단계가 Hunt로 되돌아간다  ← P0-5",
             "2단계 전제를 못 세웠다(force=%s · 단계=%s)" % (forced, phase_gang))
        skip("⑤ 3판에도 봇이 선다  ← P0-4",
             "④가 안 돌아 3판까지 못 갔다")

    # ---------- ⑥ 시리즈 중 재시작 버튼 ----------
    skip("⑥ 시리즈 중에는 결과 패널의 재시작 버튼이 안 뜬다",
         "버튼의 활성 상태를 읽는 훅이 없다(TestGetResultText는 문구만) — 눈으로 볼 것")

    note("P0-6(인원 이탈 복구)", "이 스위트로는 못 잰다 — 사람이 시리즈 도중 나가야 재현된다. "
                                "혼자 도는 봇 판은 `DevForceStartGame`이라 시작 조건이 항상 통과한다")
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
