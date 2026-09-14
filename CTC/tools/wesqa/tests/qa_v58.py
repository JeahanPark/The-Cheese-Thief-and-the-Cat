# -*- coding: utf-8 -*-
"""깡패 쥐 모습 · 몽둥이 — 2페이즈에 켜지고, 남의 화면에서도 보이고, 몽둥이가 실제로 때린다. MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=쥐** — 세팅: `python setup_match.py 0 mouse 3` (클론 하나가 고양이, 하나가 쥐).

## 왜 이 스위트가 있는가

깡패 모습(`GangLook`)은 복제값 둘(직업·페이즈)을 **각 클라가 구독**해 켠다 — Rpc가 없다.
EditMode는 조건(`GangLookLogic`)만 본다. 구독이 빠지거나 페이즈 감독을 못 찾으면 **초록인 채로 아무도 안 바뀐다.**
그래서 **호스트가 아닌 인스턴스**에서 호스트 쥐의 모습을 읽는다. 몽둥이는 같은 공격 버튼(`FireRpc`)을 타므로
총알 대신 근접 피해가 들어가는지를 HP로 잰다.

**기대 이름은 파이썬에 그대로 적는다**(qa_v53과 같은 이유).
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 7     # ⓪ 전제 + ① 표만으로는 안 바뀐다 + ② 2페이즈에 켜진다(남의 화면) + ③ 휘두르기 모션·이펙트 + ④ 근접 피해 + ⑤ 총알 아님 + ⑥ 총 테크 구매 잠금

BIPED_IDLE, CLUB_SWING, CLUB_SMASH, MUZZLE = "BipedIdle", "ClubSwing", "ClubSmash", "MuzzleFlash"
CLUB_DAMAGE = 18   # BalanceInfo.GangClubDamage — 글자가 계약이다
GANG_NO_GUN = "GangNoGun"   # TechDeny.GangNoGun


def check(name, ok, detail=""):
    RESULTS.append((name, bool(ok), detail))
    print(("[PASS] " if ok else "[FAIL] ") + name + ("  " + detail if detail else ""))
    sys.stdout.flush()
    return ok


def look(inst, client_id):
    raw = inst.invoke("TestGetGangLook", _clientId=client_id) or ""
    out = {}
    for part in raw.split(" "):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out, raw


def counts(inst):
    raw = inst.invoke("TestGetFxCounts") or ""
    return dict((p.split("=")[0], int(p.split("=")[1])) for p in raw.split(",") if "=" in p)


def main():
    host = WesPoco(instance=0)
    a = WesPoco(instance=1)
    b = WesPoco(instance=2)
    roles = [host.invoke("TestGetRole"), a.invoke("TestGetRole"), b.invoke("TestGetRole")]
    if host.invoke("TestGetRole") != "Mouse" or sorted(roles) != ["Cat", "Mouse", "Mouse"]:
        check("⓪ 전제 — 호스트=쥐 · 클론 고양이 1 · 쥐 1", False, "역할=%s" % roles)
        return summary(aborted=True)
    observer = a                                   # 호스트가 아닌 아무 인스턴스
    victim = a if roles[1] == "Mouse" else b       # 맞을 쥐
    me = host.invoke("TestGetLocalClientId")
    victim_id = victim.invoke("TestGetLocalClientId")
    check("⓪ 전제 — 호스트=쥐 · 클론 고양이 1 · 쥐 1", True, "역할=%s" % roles)

    # ---------- ① 표를 쥐었을 뿐 — 아직 안 바뀐다 ----------
    host.invoke("TestSetMouseJob", _clientId=me, _job="Gang")
    time.sleep(1.5)
    l1, raw1 = look(observer, me)
    check("① 깡패표만 쥔 상태(1페이즈)에선 모습이 그대로다 — 누가 쥐었는지 안 드러난다",
          l1.get("active") == "False" and l1.get("club") == "False" and l1.get("hat") == "False",
          "관측=%s · 호스트 직업=%s" % (raw1, host.invoke("TestGetMouseJob")))

    # ---------- ② 2페이즈 ----------
    forced = bool(host.invoke("TestForceGangPhase"))
    time.sleep(2.0)
    l2, raw2 = look(observer, me)
    check("② 2페이즈가 열리면 남의 화면에서 두 발·몽둥이·모자·살짝 화난 눈이 켜진다",
          forced and l2.get("active") == "True" and l2.get("biped") == "True" and l2.get("club") == "True"
          and l2.get("hat") == "True" and l2.get("state") == BIPED_IDLE and l2.get("rabid") == "55",
          "강제=%s · 페이즈=%s · 관측=%s" % (forced, host.invoke("TestGetPhase"), raw2))

    # ---------- ⑥ 총 테크 구매 잠금 ----------
    # 깡패판의 깡패 편은 총을 못 쓴다 → 총·후추 탄도 못 산다(2026-09-14 사용자 결정). 서버 판정(BuyRpc)도 같은 CanBuyNow를 지나므로
    # 로컬 판정 글자 + **레벨이 안 올랐는지**를 같이 본다. 대시는 깡패도 쓰니 이 사유로 막히면 안 된다.
    gun_before = host.invoke("TestGetTechLevel", _clientId=me, _techId="Gun")
    buy_gun = host.invoke("TestBuyTech", _clientId=me, _techId="Gun") or ""
    buy_pepper = host.invoke("TestBuyTech", _clientId=me, _techId="Pepper") or ""
    buy_dash = host.invoke("TestBuyTech", _clientId=me, _techId="Dash") or ""
    time.sleep(1.0)
    gun_after = host.invoke("TestGetTechLevel", _clientId=me, _techId="Gun")
    check("⑥ 깡패판의 깡패는 총·후추 탄을 못 산다(대시는 이 사유로 안 막힌다)",
          "local=" + GANG_NO_GUN in buy_gun and "local=" + GANG_NO_GUN in buy_pepper
          and GANG_NO_GUN not in buy_dash and gun_before == gun_after,
          "총=%s · 후추=%s · 대시=%s · 총 레벨 %s→%s" % (buy_gun, buy_pepper, buy_dash, gun_before, gun_after))

    # ---------- ③·⑤ 휘두르기 ----------
    observer.invoke("TestResetFxCounts")
    host.invoke("TestResetAnimObserved", _clientId=me)
    shot = bool(host.invoke("TestShootForward"))
    time.sleep(1.0)
    seen = host.invoke("TestGetAnimObserved", _clientId=me) or ""
    fx = counts(observer)
    check("③ 공격 버튼이 몽둥이 휘두르기(ClubSwing)가 되고 남의 화면에 ClubSmash가 뜬다",
          shot and CLUB_SWING in seen.split(",") and fx.get(CLUB_SMASH, 0) >= 1,
          "발사=%s 관측=[%s] 이펙트=%s" % (shot, seen, fx))
    check("⑤ 총은 안 쏜다 — 총구 섬광이 0", fx.get(MUZZLE, 0) == 0, "이펙트=%s" % fx)

    # ---------- ④ 근접 피해 ----------
    # 고양이 1m 앞에 서서 **고양이 쪽으로** 휘두른다(TestShootAtCat — 봇이 겨누는 입구와 같다).
    # 첫 실행은 쥐를 몸 앞방향으로 쳤는데, 텔레포트한 몸이 표적을 안 보고 있어 부채꼴 밖이었다.
    time.sleep(1.0)   # 쿨다운 0.7초
    cat_id = [i for i, r in zip((me, a.invoke("TestGetLocalClientId"), b.invoke("TestGetLocalClientId")), roles) if r == "Cat"][0]
    cx, cz = [float(v) for v in host.invoke("TestGetPlayerPos", _clientId=cat_id).split(",")]
    host.invoke("TestTeleportLocalPlayer", _x=cx + 1.0, _z=cz)
    time.sleep(0.8)
    hp_before = host.invoke("TestGetHpOf", _clientId=cat_id)
    host.invoke("TestShootAtCat")
    time.sleep(1.0)
    hp_after = host.invoke("TestGetHpOf", _clientId=cat_id)
    check("④ 1m 앞 고양이를 치면 HP가 몽둥이 피해(%d)만큼 준다" % CLUB_DAMAGE,
          hp_before - hp_after == CLUB_DAMAGE,
          "HP %s → %s (피해 %s)" % (hp_before, hp_after, hp_before - hp_after))
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
    print("PASS %d · FAIL %d · SKIP %d · 미실행 %d / 선언 %d" % (passed, failed, skipped, unrun, EXPECTED))
    return 0 if (failed == 0 and skipped == 0 and unrun == 0 and not aborted) else 1


if __name__ == "__main__":
    sys.exit(main())
