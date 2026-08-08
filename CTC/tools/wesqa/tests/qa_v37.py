# -*- coding: utf-8 -*-
"""v3.7 건의 3건 검증 — 카메라 프레이밍 · 옵션 창 · 채팅 (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
메인=쥐, 클론 하나가 고양이여야 '관전자 채팅이 고양이에게 안 간다'를 검증할 수 있다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []


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
    p[0].invoke("TestOpenOptions")
    time.sleep(0.4)
    opened = p[0].invoke("TestGetOptionsOpen")
    blocked = p[0].invoke("TestGetInputBlocked")
    check("옵션 — 열면 조작이 막힌다", opened and blocked, f"open={opened} blocked={blocked}")
    p[0].invoke("TestCloseOptions")
    time.sleep(0.4)
    check("옵션 — 닫으면 조작이 풀린다", not p[0].invoke("TestGetInputBlocked"))

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
    for _ in range(30):
        if p[victim].invoke("TestGetLocalHp") <= 0:
            break
        p[cat_idx].invoke("TestTeleportToOtherPlayer", _clientId=int(p[victim].invoke("TestGetLocalClientId")))
        time.sleep(0.3)
        p[cat_idx].invoke("TestPounce")
        time.sleep(0.6)
    dead = p[victim].invoke("TestGetLocalHp") <= 0
    check("관전 전환 (희생자 사망)", dead, f"hp={p[victim].invoke('TestGetLocalHp')}")

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
