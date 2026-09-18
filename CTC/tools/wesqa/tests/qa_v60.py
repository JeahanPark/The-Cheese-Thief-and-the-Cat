# -*- coding: utf-8 -*-
"""용사 쥐 — 변신 · 칼 스턴 · 무적 · 구매 제한 (2026-09-19). MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, **새 판**이 시작된 직후.
**호스트=쥐** — 세팅: `python setup_match.py 0 mouse 3` (클론 1 = 고양이, 클론 2 = 쥐).

## 왜 이 스위트가 있는가

EditMode는 규칙(스턴 겹침·칼 시간표·무적 쿨)만 본다. 여기서 재는 것은 **전파**다 — 스턴·무적·칼 모습은
복제값과 원샷 구독으로 켜지는 기능이라, 서버에만 걸리고 **맞은 쪽 화면에서는 멀쩡히 움직이는** 사고가
EditMode로는 안 보인다. 그래서 값은 **다른 인스턴스에서** 읽고, 굳었는지는 **실제로 걸어 보고** 잰다.

기획: docs/superpowers/specs/2026-09-19-hero-sword-design.md
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXPECTED = 9     # ⓪ 전제 + ①~⑧

SWORD_DAMAGE = 30        # HeroSwordDamage 20 × 용사 배율 1.5 (BalanceInfo.csv)
STUN = 0.8               # StunSeconds
TRANSFORM = 1.5          # HeroTransformLogic.SECONDS
INVINCIBLE = 3.0         # HeroInvincibleSeconds


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


def fields(line):
    """"a=1 b=True" → {"a": "1", "b": "True"}."""
    out = {}
    for part in (line or "").split():
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def pos_of(inst, client_id):
    raw = inst.invoke("TestGetPlayerPos", _clientId=client_id)
    if not raw:
        return None
    x, z = raw.split(",")
    return float(x), float(z)


def dist(a, b):
    if a is None or b is None:
        return 999.0
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5


def main():
    host = WesPoco(instance=0)     # 쥐 → 용사
    cat = WesPoco(instance=1)      # 고양이
    other = WesPoco(instance=2)    # 다른 쥐 — 고양이가 엉뚱한 쥐를 고르지 않게 치운다

    role = host.invoke("TestGetRole")
    host_id = host.invoke("TestGetLocalClientId")
    cat_id = cat.invoke("TestGetLocalClientId")
    ok_setup = check("⓪ 전제 — 호스트가 쥐이고 고양이 클론이 있다",
                     role == "Mouse" and cat.invoke("TestGetRole") == "Cat" and host.invoke("TestGetLocalHp") > 0,
                     "호스트=%s(%s) · 고양이 id=%s" % (role, host_id, cat_id))
    if not ok_setup:
        print("!! `python setup_match.py 0 mouse 3`으로 세팅했는지 본다")
        return summary(aborted=True)

    # 싸움터를 비운다 — 다른 쥐는 멀리, 용사·고양이는 트인 자리로.
    other.invoke("TestTeleportLocalPlayer", _x=13.0, _z=13.0)
    host.invoke("TestTeleportLocalPlayer", _x=0.0, _z=0.0)
    time.sleep(1.0)

    # ---------- ① 표를 밟으면 1.5초 변신 — 칼이 나타났다 거둬지고, 그동안 못 움직인다 ----------
    p0 = pos_of(host, host_id)
    host.invoke("TestSetMouseJob", _clientId=host_id, _job="Hero")
    host.invoke("TestWalkDirection", _x=1.0, _z=0.0, _seconds=0.9)
    saw_transforming, saw_sword = False, False
    t0 = time.time()
    while time.time() - t0 < TRANSFORM + 0.2:
        look = fields(cat.invoke("TestGetHeroLook", _clientId=host_id))   # **고양이 화면**에서 본다
        saw_transforming |= look.get("transforming") == "True"
        saw_sword |= look.get("swordOn") == "True"
        time.sleep(0.08)
    p1 = pos_of(host, host_id)
    time.sleep(0.8)
    after = fields(cat.invoke("TestGetHeroLook", _clientId=host_id))
    check("① 용사 표를 밟으면 남의 화면에서도 1.5초 변신(칼이 나타났다 거둬짐) · 그동안 걷기가 막힌다",
          saw_transforming and saw_sword and after.get("hero") == "True"
          and after.get("transforming") == "False" and after.get("swordOn") == "False" and dist(p0, p1) < 0.3,
          "변신 봄=%s · 칼 봄=%s · 끝난 뒤 %s · 변신 중 이동 %.2fm" % (saw_transforming, saw_sword, after, dist(p0, p1)),
          case="B-35")

    # ---------- ② 용사는 총·후추·대시를 못 산다(실제 상점 경로 — 서버가 거절한다) ----------
    host.invoke("TestGrantCheeseAll")          # 돈이 모자라서 못 사는 것과 구분한다
    host.invoke("TestWarpToNearestShop", _clientId=host_id)
    in_shop = False
    t0 = time.time()
    while time.time() - t0 < 10.0 and not in_shop:
        in_shop = host.invoke("TestIsInShop", _clientId=host_id)
        time.sleep(0.4)
    cheese0 = host.invoke("TestGetCheese")
    verdicts = {}
    for tech in ("Gun", "Pepper", "Dash"):
        verdicts[tech] = host.invoke("TestBuyTech", _clientId=host_id, _techId=tech)
    time.sleep(1.2)
    levels = {t: host.invoke("TestGetTechLevel", _clientId=host_id, _techId=t) for t in ("Gun", "Pepper", "Dash")}
    cheese1 = host.invoke("TestGetCheese")
    # 치즈는 「그대로」가 아니라 「안 빠졌다」로 본다 — 그 사이 채굴·창고 수입이 몇 개 들어온다(2026-09-19 실측 +5).
    check("② 용사는 치즈가 넉넉해도 총·후추·대시를 못 산다 — 레벨 0 · 값이 안 빠진다",
          in_shop and all(v == 0 for v in levels.values()) and cheese1 >= cheese0
          and all("HeroNoGun" in (verdicts[t] or "") for t in verdicts),
          "상점 안=%s · 판정=%s · 레벨=%s · 치즈 %s→%s" % (in_shop, verdicts, levels, cheese0, cheese1),
          case="C-38")
    host.invoke("TestTeleportLocalPlayer", _x=0.0, _z=0.0)
    time.sleep(1.0)

    # ---------- ③ 칼로 벤 고양이가 굳는다 — 고양이 화면에서도 · 피해는 약하다 ----------
    cat.invoke("TestTeleportLocalPlayer", _x=0.0, _z=1.1)
    time.sleep(0.8)
    host.invoke("TestTeleportAtRangeFrom", _role="Cat", _meters=1.1, _yawOffset=0.0, _botOnly=False)
    time.sleep(0.6)
    hp0 = host.invoke("TestGetHpOf", _clientId=cat_id)
    host.invoke("TestShootForward")            # 용사의 공격 버튼 = 칼(MousePopgun → ServerSword)
    time.sleep(0.15)
    stun_cat_screen = cat.invoke("TestGetStunRemaining", _clientId=cat_id)
    edge_cat = cat.invoke("TestGetStunVignette")        # 맞은 **고양이 화면** 가장자리가 하얗게 선다
    edge_host = host.invoke("TestGetStunVignette")      # 벤 쪽 화면은 멀쩡하다
    cat_state = fields(host.invoke("TestGetHeroLook", _clientId=cat_id)).get("state", "")
    time.sleep(0.4)
    hp1 = host.invoke("TestGetHpOf", _clientId=cat_id)
    check("③ 칼에 맞은 고양이가 **고양이 화면에서도** 굳고(Stunned 자세·가장자리 흰 테두리) 피해는 약하다(%d)" % SWORD_DAMAGE,
          0.0 < stun_cat_screen <= STUN + 0.05 and cat_state == "Stunned" and hp0 - hp1 == SWORD_DAMAGE
          and edge_cat > 0.3 and edge_host == 0.0,
          "고양이 화면 스턴=%.2f초 · 테두리 고양이=%.2f/용사=%.2f · 호스트 화면 자세=%s · HP %s→%s"
          % (stun_cat_screen, edge_cat, edge_host, cat_state, hp0, hp1), case="B-35")

    # ---------- ④ 굳은 동안 걷지도 덮치지도 못한다(길게 걸어 재기) → 풀리면 걷는다 ----------
    time.sleep(1.0)                            # ③의 스턴이 풀리고 나서
    host.invoke("TestApplyStun", _clientId=cat_id, _seconds=3.0)
    time.sleep(0.3)
    c0 = pos_of(host, cat_id)
    host_hp0 = host.invoke("TestGetLocalHp")
    cat.invoke("TestWalkDirection", _x=0.0, _z=1.0, _seconds=1.2)
    cat.invoke("TestPounce")
    time.sleep(1.4)
    cat.invoke("TestPounce")
    time.sleep(0.4)
    c1 = pos_of(host, cat_id)
    host_hp1 = host.invoke("TestGetLocalHp")
    time.sleep(1.4)                            # 3초 스턴이 끝난다
    cat.invoke("TestWalkDirection", _x=0.0, _z=1.0, _seconds=1.0)
    time.sleep(1.3)
    c2 = pos_of(host, cat_id)
    check("④ 굳은 고양이는 걷지도 덮치지도 못하고, 풀리면 다시 걷는다",
          dist(c0, c1) < 0.2 and host_hp1 == host_hp0 and dist(c1, c2) > 0.5,
          "굳은 동안 이동 %.2fm · 용사 HP %s→%s · 풀린 뒤 이동 %.2fm" % (dist(c0, c1), host_hp0, host_hp1, dist(c1, c2)),
          case="B-35")

    # ---------- ⑤ 겹쳐 맞아도 스턴이 늘지 않는다 ----------
    host.invoke("TestApplyStun", _clientId=cat_id, _seconds=STUN)
    host.invoke("TestApplyStun", _clientId=cat_id, _seconds=STUN)
    host.invoke("TestApplyStun", _clientId=cat_id, _seconds=STUN)
    stacked = host.invoke("TestGetStunRemaining", _clientId=cat_id)
    check("⑤ 연달아 맞아도 스턴은 %.1f초를 넘지 않는다(더하지 않고 갱신)" % STUN,
          0.0 < stacked <= STUN + 0.01, "남은 스턴 %.2f초" % stacked, case="B-35")
    time.sleep(1.2)

    # ---------- ⑥ 무적 — 남의 화면에도 흰 테두리 · 맞아도 HP 그대로 ----------
    host.invoke("TestUseInvincible")
    time.sleep(0.4)
    seen = fields(cat.invoke("TestGetHeroInvincible", _clientId=host_id))   # 고양이 화면
    cat.invoke("TestTeleportAtRangeFrom", _role="Mouse", _meters=1.2, _yawOffset=0.0, _botOnly=False)
    time.sleep(0.4)
    inv_hp0 = host.invoke("TestGetLocalHp")
    cat.invoke("TestPounce")
    time.sleep(0.5)
    inv_hp1 = host.invoke("TestGetLocalHp")
    check("⑥ 무적이 켜지면 고양이 화면에도 테두리가 뜨고, 덮쳐도 HP가 그대로다",
          seen.get("on") == "True" and float(seen.get("rim", "0")) > 0.5 and inv_hp1 == inv_hp0,
          "고양이 화면 %s · HP %s→%s" % (seen, inv_hp0, inv_hp1), case="B-36")

    # ---------- ⑦ 끝나면 테두리가 꺼진다 ----------
    time.sleep(INVINCIBLE + 0.6)
    ended = fields(cat.invoke("TestGetHeroInvincible", _clientId=host_id))
    check("⑦ 무적이 끝나면 남의 화면에서도 테두리가 꺼진다",
          ended.get("on") == "False" and float(ended.get("rim", "1")) == 0.0,
          "고양이 화면 %s" % ended, case="B-36")

    # ---------- ⑧ 쿨다운 동안은 다시 안 켜진다 ----------
    host.invoke("TestUseInvincible")
    time.sleep(0.5)
    again = fields(host.invoke("TestGetHeroInvincible", _clientId=host_id))
    check("⑧ 쿨다운(10초) 동안은 Space를 눌러도 다시 안 켜진다",
          again.get("on") == "False" and float(again.get("cd", "0")) > 0.0,
          "%s" % again, case="B-36")

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
