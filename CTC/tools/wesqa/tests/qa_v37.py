# -*- coding: utf-8 -*-
"""v3.7 건의 3건 검증 — 카메라 프레이밍 · 옵션 창 · 채팅 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
메인=쥐, 클론 하나가 고양이여야 '관전자 채팅이 고양이에게 안 간다'를 검증할 수 있다.

**한 판에 한 번만 돌린다.** 중간에 쥐를 죽이고 설정·채팅 상태를 남기므로 같은 판에서
두 번째로 돌리면 앞선 실행의 잔여물 때문에 엉뚱한 항목이 실패한다(2026-08-09 실측).
다시 돌리려면 플레이모드를 껐다 켜고 매치를 새로 시작한다.
"""
import subprocess, sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []

_FOCUS_SCRIPT = r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\focus-unity.ps1"


def focus_unity():
    """게임 뷰에 포커스를 준다 — 커서 잠금은 포커스가 없으면 에디터가 강제로 푼다."""
    subprocess.run(["powershell", "-NoProfile", "-File", _FOCUS_SCRIPT], capture_output=True)
    time.sleep(1.2)


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [x.invoke("TestGetLocalClientId") for x in p]
    check("3인 접속", all(i >= 0 for i in ids), str(ids))

    # 역할 파악
    roles = [x.invoke("TestGetRole") for x in p]
    cat_idx = roles.index("Cat") if "Cat" in roles else None
    mice = [i for i in range(3) if i != cat_idx]
    check("역할 확인 (고양이 1 · 쥐 2)", cat_idx is not None and len(mice) == 2, str(roles))

    # ---------- 옵션 ----------
    p[0].invoke("TestSetSetting", _key="sensitivity", _value=0.33)
    time.sleep(0.3)
    check("옵션 — 감도 설정이 Model에 반영",
          abs(p[0].invoke("TestGetSetting", _key="sensitivity") - 0.33) < 0.001)

    p[0].invoke("TestSetSetting", _key="sfx", _value=0.25)
    time.sleep(0.3)
    check("옵션 — 효과음 설정이 Model에 반영",
          abs(p[0].invoke("TestGetSetting", _key="sfx") - 0.25) < 0.001)

    check("옵션 — 열기 전에는 입력이 안 막혀 있다", not p[0].invoke("TestGetInputBlocked"))

    # 커서 항목 3개는 게임 뷰 포커스가 있어야 의미가 있다 —
    # 에디터는 포커스를 잃으면 잠금을 강제 해제해 전부 False로 읽힌다(2026-08-08 실측).
    focus_unity()
    locked_ingame = p[0].invoke("TestGetCursorLocked")
    check("커서 — 인게임에서는 잠겨 있다", locked_ingame,
          "" if locked_ingame else "게임 뷰 포커스 실패 — 아래 커서 2항목은 무효")

    p[0].invoke("TestOpenOptions")
    time.sleep(0.4)
    opened = p[0].invoke("TestGetOptionsOpen")
    blocked = p[0].invoke("TestGetInputBlocked")
    check("옵션 — 열면 조작이 막힌다", opened and blocked, f"open={opened} blocked={blocked}")

    # 2026-08-08 회귀: LobbyUI가 매 프레임 커서를 다시 잠가 옵션 슬라이더를 클릭할 수 없었다.
    focus_unity()
    check("커서 — 옵션을 열면 풀린다 (클릭 가능)", not p[0].invoke("TestGetCursorLocked"))

    p[0].invoke("TestCloseOptions")
    time.sleep(0.4)
    check("옵션 — 닫으면 조작이 풀린다", not p[0].invoke("TestGetInputBlocked"))

    focus_unity()
    check("커서 — 옵션을 닫으면 다시 잠긴다", p[0].invoke("TestGetCursorLocked"))

    # ---------- 미리듣기 / 확인 / 취소 (2026-08-09) ----------
    def sfx():
        return p[0].invoke("TestGetSetting", _key="sfx")

    # 기준값 하나를 디스크에 굳혀 둔다
    p[0].invoke("TestSetSetting", _key="sfx", _value=0.40)
    p[0].invoke("TestSaveSettings")
    time.sleep(0.3)

    p[0].invoke("TestOpenOptions")
    p[0].invoke("TestSetSetting", _key="sfx", _value=0.90)
    time.sleep(0.3)
    check("옵션 — 슬라이더는 즉시 반영된다(미리듣기)", abs(sfx() - 0.90) < 0.001, f"sfx={sfx():.2f}")

    p[0].invoke("TestCloseOptions")  # 취소·ESC와 같은 경로
    time.sleep(0.4)
    check("옵션 — 확인 없이 닫으면 원복된다", abs(sfx() - 0.40) < 0.001, f"sfx={sfx():.2f} (기대 0.40)")

    p[0].invoke("TestOpenOptions")
    p[0].invoke("TestSetSetting", _key="sfx", _value=0.70)
    p[0].invoke("TestSaveSettings")
    p[0].invoke("TestCloseOptions")
    time.sleep(0.4)
    check("옵션 — 확인을 누르면 값이 굳는다", abs(sfx() - 0.70) < 0.001, f"sfx={sfx():.2f} (기대 0.70)")

    # 굳힌 뒤의 취소는 "창을 열었을 때"가 아니라 "마지막 확인" 값으로 돌아가야 한다
    p[0].invoke("TestOpenOptions")
    p[0].invoke("TestSetSetting", _key="sfx", _value=0.10)
    p[0].invoke("TestCloseOptions")
    time.sleep(0.4)
    check("옵션 — 취소는 마지막 확인 값으로 돌아간다", abs(sfx() - 0.70) < 0.001, f"sfx={sfx():.2f} (기대 0.70)")

    # ---------- 입력창을 거친 전송 (2026-08-09) ----------
    # 한글 마지막 음절이 잘리던 버그는 ChatHud.Submit()에 있었는데, 기존 채팅 항목은
    # TestSendChat(UI 우회)만 써서 그 경로를 한 번도 안 지나갔다.
    #
    # **주입한 Enter는 게임 뷰 포커스를 탄다.** 포커스가 없으면 키가 통째로 버려져
    # 채팅창만 열린 채 남는다(커서 항목과 같은 함정, 2026-08-09 실측으로 규명).
    # 그래서 치기 직전에 포커스를 주고, 실패하면 한 번 더 준 뒤 다시 친다.
    typed = "입력창경유테스트"
    arrived = still_open = None
    for attempt in range(2):
        focus_unity()
        p[0].invoke("TestTypeChatAndEnter", _text=typed)  # 호스트=쥐, 이 시점엔 아직 살아 있다
        time.sleep(1.4)
        log = p[0].invoke("TestGetChatLog")
        arrived = typed in log
        still_open = p[0].invoke("TestGetChatOpen")
        if arrived:
            break

    # 창이 열린 채면 Open()은 됐고 Enter가 안 온 것 — 제품이 아니라 포커스 문제다.
    if arrived:
        detail = "마지막 글자까지 도착"
    elif still_open:
        detail = "게임 뷰 포커스 실패 — 주입한 Enter가 버려졌다(제품 아님)"
    else:
        detail = f"Submit은 돌았는데 안 왔다 — 로그 끝: ...{log[-40:]}"
    check("채팅 — 입력창으로 친 글이 통째로 전송된다", arrived, detail)
    if still_open:
        p[0].invoke("TestCloseChat")

    # ---------- 실제로 들리는 볼륨 (2026-08-09) ----------
    # Model 값만 보던 기존 항목은 "설정은 바뀌는데 소리는 그대로"를 못 잡았다.
    # 루프 소스는 생성기가 볼륨을 구워 두므로 AudioVolumeFollower가 붙어 있어야만 따라온다.
    def vol(name):
        return p[0].invoke("TestGetAudioVolume", _name=name)

    p[0].invoke("TestSetSetting", _key="bgm", _value=1.0)
    time.sleep(0.4)
    bgm_hi, amb_hi = vol("BgmPlayer"), vol("AmbiencePlayer")
    p[0].invoke("TestSetSetting", _key="bgm", _value=0.0)
    time.sleep(0.4)
    bgm_lo, amb_lo = vol("BgmPlayer"), vol("AmbiencePlayer")

    check("사운드 — 배경음 슬라이더가 BGM 볼륨을 실제로 바꾼다",
          bgm_hi > 0.001 and bgm_lo < 0.001, f"100%={bgm_hi} 0%={bgm_lo}")
    check("사운드 — 앰비언스도 배경음을 따라간다",
          amb_hi > 0.001 and amb_lo < 0.001, f"100%={amb_hi} 0%={amb_lo}")
    check("사운드 — 구워진 비율이 유지된다(BGM 0.35 · 앰비언스 0.15)",
          abs(bgm_hi - 0.35) < 0.001 and abs(amb_hi - 0.15) < 0.001,
          f"bgm={bgm_hi} amb={amb_hi}")

    # 이 PC를 CSV 기본값(50%)으로 돌려놓고 나간다
    p[0].invoke("TestResetSettings")
    p[0].invoke("TestSaveSettings")

    # ---------- 채팅 ----------
    alive = mice[0]
    p[alive].invoke("TestSendChat", _text="살아있는쥐테스트")
    time.sleep(1.2)
    seen_by_cat = "살아있는쥐테스트" in p[cat_idx].invoke("TestGetChatLog")
    seen_by_self = "살아있는쥐테스트" in p[alive].invoke("TestGetChatLog")
    check("채팅 — 살아있는 쥐 메시지는 전원에게", seen_by_cat and seen_by_self,
          f"cat={seen_by_cat} self={seen_by_self}")

    # 길이 제한 — 서버가 120자로 자른다
    p[alive].invoke("TestSendChat", _text="가" * 300)
    time.sleep(1.2)
    log = p[alive].invoke("TestGetChatLog")
    longest = max((len(x) for x in log.split("\n")), default=0)
    check("채팅 — 서버가 길이를 자른다(120자)", longest < 200, f"가장 긴 줄 {longest}자")

    # 도배 쿨다운 — 연달아 두 줄을 보내면 두 번째는 버려진다
    before = p[alive].invoke("TestGetChatLog").count("\n")
    p[alive].invoke("TestSendChat", _text="도배1")
    p[alive].invoke("TestSendChat", _text="도배2")
    time.sleep(1.2)
    after = p[alive].invoke("TestGetChatLog")
    check("채팅 — 쿨다운으로 도배가 막힌다",
          ("도배1" in after) != ("도배2" in after), "둘 중 하나만 통과")

    # 관전자 채팅은 고양이에게 안 간다 — 쥐 하나를 죽인다
    victim = mice[1]
    p[cat_idx].invoke("TestGrantCheeseAll")
    # 덮치기 쿨다운이 2.5초다 — 0.9초 간격으로 30번 두드리면 대부분 쿨다운에 씹혀
    # 실제 타격은 몇 번뿐이고, 그러다 못 잡고 끝난다(2026-08-09 실측). 간격을 넓힌다.
    vid = int(p[victim].invoke("TestGetLocalClientId"))
    for _ in range(14):
        if p[victim].invoke("TestGetLocalHp") <= 0:
            break
        p[cat_idx].invoke("TestTeleportToOtherPlayer", _clientId=vid)
        time.sleep(0.4)
        p[cat_idx].invoke("TestPounce")
        time.sleep(1.4)

    # 잡히면 즉시 탈락(관전) — 감옥·구출(v3.8)은 2026-08-17에 뺐다.
    dead = p[victim].invoke("TestGetLocalHp") <= 0
    check("관전 전환 (잡히면 즉시 탈락)", dead, f"hp={p[victim].invoke('TestGetLocalHp')}")

    if dead:
        time.sleep(1.5)
        p[victim].invoke("TestSendChat", _text="관전자비밀")
        time.sleep(1.5)
        cat_log = p[cat_idx].invoke("TestGetChatLog")
        mouse_log = p[alive].invoke("TestGetChatLog")
        check("채팅 — 관전자 메시지가 고양이에게 안 보인다", "관전자비밀" not in cat_log)
        check("채팅 — 관전자 메시지가 살아있는 쥐에게는 보인다", "관전자비밀" in mouse_log)

    print("\n===== 요약 =====")
    for name, ok, detail in RESULTS:
        print(("  OK   " if ok else "  FAIL ") + name + (f"  | {detail}" if detail else ""))
    passed = sum(1 for _, ok, _ in RESULTS if ok)
    print(f"{passed}/{len(RESULTS)}")


main()
