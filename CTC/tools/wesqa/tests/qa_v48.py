# -*- coding: utf-8 -*-
"""고양이 테크 효과 7항목 — 테크 v3 계획 2b (MPPM 3인).

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=고양이** — 세팅: `python setup_match.py 0 cat 3` (클론 둘 다 쥐).

왜 호스트가 고양이인가:
  · 재는 것이 전부 고양이 테크다. `TestBuyTech`/`TestUseTech`는 **소유자 RPC**라 그 몸의
    인스턴스에서만 듣는다 — 고양이를 클론에 두면 구매·사용을 전부 클론에서 불러야 한다.
  · `TestGetListenerMultiplier`는 **복제되지 않는 로컬 값**이다(소리는 듣는 쪽에서 곱한다).
    고양이를 쥔 인스턴스에서 물어야 참이라, 호스트가 고양이여야 한 인스턴스로 다 읽는다.
  · 고양이는 상점 자리에 갈 필요가 없다(`TechLogic.NeedsShopSpot` — 쥐만). 쥐(요새)만 옮긴다.

칸은 5개(TechSlotCap)이고 고양이 테크는 6종이라 **한 몸에 다 못 든다** — 그게 곧 ⑤다.
  두꺼운 가죽 · 배고픔 · 광분 · 예민한 귀 · 발톱 = 5칸 → 여섯째(발 구르기)는 거절돼야 한다.

피해 배율(③⑥)은 **쥐가 아니라 요새 벽**으로 잰다. 쥐 HP는 100인데 광분 Lv3 한 방이
150이라, 쥐로 재면 배율이 아니라 "죽었다"만 보인다. 요새 Lv3은 1200이라 다 견딘다.

값은 전부 훅에서 읽는다(`TestGetTechCost`·`TestGetPlaceCost`) — 가격을 손으로 적지 않는다.
한 판에 한 번만 돌린다(칸을 다 채우고 요새가 남는다). 다시 돌리려면 플레이모드를 껐다 켠다.
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


def note(name, detail):
    """설계 확인용 — 성공/실패를 매기지 않고 **관측값만** 남긴다(⑥)."""
    RESULTS.append((name, None, detail))
    print("[NOTE] " + name + "  " + detail)
    sys.stdout.flush()


def skip(name, why):
    RESULTS.append((name, None, why))
    print("[SKIP] " + name + "  " + why)
    sys.stdout.flush()


def warp_to_shop(host, client_id):
    """가장 가까운 상점 자리로 옮긴다 — qa_v36·v46·v47과 같은 규약.

    `TestWarpToNearestShop`은 `BodyPush.ServerPush` 경유라 **호스트 인스턴스**에서 불러야 하고,
    그 안에서 다시 소유자 RPC를 타므로 반환값은 "보냈다"지 "도착했다"가 아니다 — 쉬었다 확인한다.
    """
    host.invoke("TestWarpToNearestShop", _clientId=client_id)
    time.sleep(1.2)
    return bool(host.invoke("TestIsInShop", _clientId=client_id))


def buy_to(inst, host, client_id, tech, level):
    """그 몸의 인스턴스에서 목표 레벨까지 산다. 값은 CSV 훅에서 읽어 로그로만 남긴다."""
    costs = [host.invoke("TestGetTechCost", _techId=tech, _level=lv) for lv in range(1, level + 1)]
    for _ in range(level):
        inst.invoke("TestBuyTechLocal", _techId=tech)
        time.sleep(0.9)
    got = host.invoke("TestGetTechLevel", _clientId=client_id, _techId=tech)
    print("    %s Lv%s 구매 시도 (비용 %s) → Lv%s" % (tech, level, costs, got))
    sys.stdout.flush()
    return got


def pounce_player(cat, host, cat_id, target_id, attempts=3):
    """고양이를 쥐 앞에 세우고 실제로 깎일 때까지 덮친다 → (쥐 HP 손실, 고양이 장난기 적립, 시도 수).

    한 번 쳐서 0이 나와도 "안 맞는다"가 아니다 — 덮치기는 **차폐**(치즈 지점 콜라이더 등)에
    막히면 그 대상만 건너뛴다. 자리를 다시 잡고 다시 친다. 장난기는 실피해에서만 나오므로
    HP가 안 깎인 시도는 지갑도 안 움직인다.
    """
    for i in range(1, attempts + 1):
        cat.invoke("TestTeleportToOtherPlayer", _clientId=target_id)
        time.sleep(1.5)
        hp0 = host.invoke("TestGetHpOf", _clientId=target_id)
        fun0 = host.invoke("TestGetCheeseOf", _clientId=cat_id)
        cat.invoke("TestPounce")
        time.sleep(1.5)
        loss = hp0 - host.invoke("TestGetHpOf", _clientId=target_id)
        fun = host.invoke("TestGetCheeseOf", _clientId=cat_id) - fun0
        if loss > 0:
            return loss, fun, i
        time.sleep(2.5)   # 덮치기 쿨(PlayerInfo Cat.PounceCooldown)
    return 0, 0, attempts


def pounce_wall(cat, host, mouse_id, settle=1.6, attempts=2):
    """요새 벽을 한 대 치고 깎인 HP를 돌려준다 — 배율 계측의 자.

    고양이를 요새 안 쥐에게 보내면 벽이 밀어내고, 쥐는 차폐로 걸러져 **벽만** 맞는다
    (`CatPounce.IsOccluded`). 씬에 선 구조물이 이 요새 하나뿐이라 최저 HP가 곧 그 벽이다.

    0이 나오면 "배율이 0"이 아니라 **안 맞은 것**이다 — 자리를 다시 잡고 한 번 더 치고,
    그래도 0이면 서버와 같은 규칙으로 이유를 찍는다(`TestDebugPounceTargets`).
    무엇에 막혔는지가 로그에 없으면 실패를 읽을 수 없다(2026-09-08 실측: 바닥 콜라이더).
    """
    for i in range(1, attempts + 1):
        cat.invoke("TestTeleportToOtherPlayer", _clientId=mouse_id)
        time.sleep(settle)
        before = host.invoke("TestGetLowestStructureHp")
        cat.invoke("TestPounce")
        time.sleep(1.5)
        after = host.invoke("TestGetLowestStructureHp")
        if before - after > 0:
            return before - after, before, after
        print("    벽에 안 맞았다(%d회차) — %s" % (i, host.invoke("TestDebugPounceTargets")))
        sys.stdout.flush()
        if i < attempts:
            time.sleep(2.5)   # 덮치기 쿨
    return 0, before, after


def wait_frenzy_ready(host, cat_id, limit=25.0):
    """광분 쿨이 끝날 때까지 기다린다(상한 limit초). 남은 초를 돌려준다."""
    until = time.time() + limit
    cd = host.invoke("TestGetCatFrenzyCooldown", _clientId=cat_id)
    while cd > 0.05 and time.time() < until:
        time.sleep(1.0)
        cd = host.invoke("TestGetCatFrenzyCooldown", _clientId=cat_id)
    return cd


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [int(x.invoke("TestGetLocalClientId")) for x in p]
    roles = [x.invoke("TestGetRole") for x in p]
    ok_setup = check("⓪ 3인 접속 · 호스트=고양이 · 쥐 2",
                     all(i >= 0 for i in ids) and roles[0] == "Cat"
                     and roles.count("Cat") == 1 and roles.count("Mouse") == 2,
                     "roles=%s ids=%s" % (roles, ids))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 0 cat 3`으로 판을 다시 연다")
        return 1

    host = p[0]          # 호스트 = 고양이. 읽기·구매·사용을 전부 여기서 한다.
    c = ids[0]
    m1_idx, m2_idx = [i for i in range(3) if i != 0]
    m1i, m1 = p[m1_idx], ids[m1_idx]
    m2i, m2 = p[m2_idx], ids[m2_idx]
    print("고양이=%s(호스트) · 쥐A=%s(inst %s) · 쥐B=%s(inst %s)" % (c, m1, m1_idx, m2, m2_idx))

    # 지갑 채우기 — 고양이가 4종×3레벨(약 2700) + 발톱, 쥐가 요새 Lv3(760)+설치비를 쓴다.
    # **계측 전에 다 준다**(중간에 넣으면 ②의 장난기 적립 계측이 오염된다).
    for _ in range(8):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)
    time.sleep(0.6)
    print("지갑: 고양이(장난기)=%s 쥐A=%s 쥐B=%s"
          % (host.invoke("TestGetCheeseOf", _clientId=c),
             host.invoke("TestGetCheeseOf", _clientId=m1),
             host.invoke("TestGetCheeseOf", _clientId=m2)))

    spot = host.invoke("TestGetBestCheeseSpotPos")
    sx, sz = [float(v) for v in spot.split(",")]

    # 쥐 둘을 치즈 지점에서 떼어 놓는다 — 지점 콜라이더가 덮치기를 가린다(qa_v47 ④와 같은 이유).
    m1i.invoke("TestTeleportLocalPlayer", _x=sx + 8.0, _z=sz + 8.0)
    m2i.invoke("TestTeleportLocalPlayer", _x=sx - 8.0, _z=sz - 8.0)
    time.sleep(2.0)

    # ---------- ① 두꺼운 가죽 Lv3 — 최대 HP 1450 · 현재 HP도 함께 오른다 ----------
    max_before = host.invoke("TestGetMaxHpOf", _clientId=c)
    hp_before = host.invoke("TestGetHpOf", _clientId=c)
    hide = buy_to(host, host, c, "CatHide", 3)
    time.sleep(0.6)
    max_after = host.invoke("TestGetMaxHpOf", _clientId=c)
    hp_after = host.invoke("TestGetHpOf", _clientId=c)
    check("① 두꺼운 가죽 Lv3 — 최대 HP 1450 · 현재 HP도 오른다",
          hide == 3 and max_after == 1450 and hp_after > hp_before,
          "Lv=%s 최대 HP %s→%s(기대 1450) · 현재 HP %s→%s"
          % (hide, max_before, max_after, hp_before, hp_after))

    # ---------- ② 배고픔 Lv3 — 같은 피해에 장난기 ×1.6 ----------
    # 고양이 재화 이름은 **장난기**지만 지갑은 쥐의 치즈와 같은 컴포넌트다(CheeseWallet) —
    # 그래서 훅 이름이 TestGetCheeseOf다. 쥐 둘을 각각 한 번씩만 친다(HP 100 · 덮치기 60이라
    # 한 방으로는 안 죽고, 두 방이면 죽어 킬 보너스가 섞인다).
    base_loss, base_fun, base_tries = pounce_player(host, host, c, m1)
    time.sleep(2.5)
    hunger = buy_to(host, host, c, "CatHunger", 3)
    hung_loss, hung_fun, hung_tries = pounce_player(host, host, c, m2)
    fun_ratio = (float(hung_fun) / base_fun) if base_fun > 0 else 0.0
    check("② 배고픔 Lv3 — 같은 피해에 장난기 ×1.6",
          hunger == 3 and base_loss > 0 and base_loss == hung_loss
          and base_fun > 0 and 1.45 <= fun_ratio <= 1.75,
          "Lv=%s 피해 %s/%s · 장난기 %s→%s (×%.2f, 기대 ×1.6) 시도 %s/%s"
          % (hunger, base_loss, hung_loss, base_fun, hung_fun, fun_ratio, base_tries, hung_tries))

    # ---------- 계측대 세우기: 쥐B의 요새 Lv3(HP 1200) ----------
    # ③⑥의 피해 배율은 여기서 잰다. 쥐 몸(HP 100)으로는 ×2.5를 볼 수 없다.
    # **선 자리에서 그대로 세운다**(qa_v47 ③과 같은 자리). 임의 좌표로 옮겨 세웠더니
    # 고양이와 벽 사이에 방 바닥 콜라이더가 끼어 `SightLine`이 전부 차폐로 판정했다
    # (2026-09-08 실측: `Floor_R5@MapBuilder`에 막혀 네 번의 덮치기가 전부 0). 상점 자리는
    # 사람이 실제로 서는 자리라 그 사고가 안 난다.
    in_shop = warp_to_shop(host, m2)
    fort = buy_to(m2i, host, m2, "Fortress", 3)
    fort_place = host.invoke("TestGetPlaceCost", _techId="Fortress")
    m2i.invoke("TestUseTechLocal", _techId="Fortress")
    time.sleep(2.0)
    wall_max = host.invoke("TestGetNearestFortressMaxHp", _clientId=m2)
    wall_hp = host.invoke("TestGetLowestStructureHp")
    print("    계측대: 쥐B 요새 Lv%s (상점 안=%s · 설치비 %s) MaxHp=%s 현재=%s"
          % (fort, in_shop, fort_place, wall_max, wall_hp))
    if wall_max != 1200:
        print("!! 계측대가 안 섰다 — ③⑥의 피해 배율을 잴 수 없다")

    # ---------- ③ 광분 Lv3 — 켜짐 · 덮치기 ×2.5 · 10초 뒤 꺼짐 · 쿨 20 ----------
    frenzy = buy_to(host, host, c, "CatFrenzy", 3)
    frenzy_cost = host.invoke("TestGetTechCost", _techId="CatFrenzy", _level=3)   # 로그용(가격)

    plain_loss, w0, w1 = pounce_wall(host, host, m2)
    time.sleep(2.5)

    t0 = time.time()
    host.invoke("TestUseTech", _clientId=c, _techId="CatFrenzy")
    time.sleep(0.6)
    active_on = bool(host.invoke("TestGetCatFrenzyActive", _clientId=c))
    cd_now = host.invoke("TestGetCatFrenzyCooldown", _clientId=c)
    frenzy_loss, w2, w3 = pounce_wall(host, host, m2, settle=1.2)
    ratio = (float(frenzy_loss) / plain_loss) if plain_loss > 0 else 0.0

    check("③-a 광분 Lv3 — 켜지고 쿨 20초가 돈다",
          frenzy == 3 and active_on and 18.0 <= cd_now <= 20.5,
          "Lv=%s(Lv3 가격 %s) active=%s 남은 쿨 %.1f초 (기대 ~20)"
          % (frenzy, frenzy_cost, active_on, cd_now))
    check("③-b 광분 중 덮치기 피해 ×2.5",
          plain_loss > 0 and 2.2 <= ratio <= 2.8,
          "벽 HP %s→%s(평소 -%s) · %s→%s(광분 -%s) ×%.2f (기대 ×2.5)"
          % (w0, w1, plain_loss, w2, w3, frenzy_loss, ratio))

    while time.time() - t0 < 11.0:
        time.sleep(0.5)
    active_off = bool(host.invoke("TestGetCatFrenzyActive", _clientId=c))
    cd_left = host.invoke("TestGetCatFrenzyCooldown", _clientId=c)
    check("③-c 10초 뒤 꺼진다 (쿨은 아직 남아 있다)",
          not active_off and cd_left > 0.0,
          "t+%.1f초 active=%s 남은 쿨 %.1f초" % (time.time() - t0, active_off, cd_left))

    # ---------- ④ 예민한 귀 Lv3 — 듣는 쪽 가청 배율 2배 ----------
    # **복제되지 않는 로컬 값**이라 고양이를 쥔 인스턴스(=호스트)에서만 참이다.
    ear_before = host.invoke("TestGetListenerMultiplier")
    ear = buy_to(host, host, c, "CatEar", 3)
    time.sleep(0.8)
    ear_after = host.invoke("TestGetListenerMultiplier")
    check("④ 예민한 귀 Lv3 — 가청 거리 배율 2배",
          ear == 3 and abs(ear_after - 2.0) < 0.01,
          "Lv=%s 배율 %.2f→%.2f (기대 2.0)" % (ear, ear_before, ear_after))

    # ---------- ⑤ 고양이 칸 5 상한 — 여섯째 테크 거절 ----------
    slots_4 = host.invoke("TestGetTechSlotCount", _clientId=c)
    claw = buy_to(host, host, c, "CatClaw", 1)
    slots_5 = host.invoke("TestGetTechSlotCount", _clientId=c)

    stomp_cost = host.invoke("TestGetTechCost", _techId="CatStomp", _level=1)
    purse_before = host.invoke("TestGetCheeseOf", _clientId=c)
    host.invoke("TestBuyTechLocal", _techId="CatStomp")
    time.sleep(1.2)
    slots_6 = host.invoke("TestGetTechSlotCount", _clientId=c)
    stomp = host.invoke("TestGetTechLevel", _clientId=c, _techId="CatStomp")
    purse_after = host.invoke("TestGetCheeseOf", _clientId=c)
    check("⑤ 칸 5 상한 — 여섯째(발 구르기)가 거절되고 장난기도 안 빠진다",
          slots_5 == 5 and slots_6 == 5 and stomp == 0
          and purse_before >= stomp_cost and purse_after == purse_before,
          "칸 %s→%s→%s · 발 구르기 Lv=%s(값 %s, 지갑 %s→%s)"
          % (slots_4, slots_5, slots_6, stomp, stomp_cost, purse_before, purse_after))

    # ---------- ⑥ 광분 + 전직 불 — 곱이 아니라 max인가(계획 3 Task 5) ----------
    # 곱이면 ×2.5 × ×2 = ×5.0, 흡수(기획서 §5)면 ×2.5다. 관측 배율로 재고 판정한다.
    host.invoke("TestMaxAllUpgrades")          # 덮치기 기본값을 올린다(전직 조건과는 무관하다)
    time.sleep(0.6)
    host.invoke("TestGrantAdvanceMischief")    # 전직 문턱 = 누적 획득 장난기(2026-09-09)
    time.sleep(0.6)
    host.invoke("TestChooseCatClass", _id="Fire")
    time.sleep(1.0)
    cat_class = host.invoke("TestGetCatClass")

    stack_loss, s0, s1 = pounce_wall(host, host, m2)   # 강화 만렙 · 아무것도 안 켠 새 기준선
    time.sleep(2.5)
    cd_wait = wait_frenzy_ready(host, c)
    host.invoke("TestUseCatSkill")
    host.invoke("TestUseTech", _clientId=c, _techId="CatFrenzy")
    time.sleep(0.6)
    both_on = (bool(host.invoke("TestGetCatSkillActive")),
               bool(host.invoke("TestGetCatFrenzyActive", _clientId=c)))
    both_loss, s2, s3 = pounce_wall(host, host, m2, settle=1.2)
    both_ratio = (float(both_loss) / stack_loss) if stack_loss > 0 else 0.0
    # 흡수라면 큰 쪽(광분 ×2.5)만 남는다. 곱(×5.0)이면 요새 Lv3이 한 방에 무너진다.
    frenzy_only = 2.5
    check("⑥ 광분 + 전직 불 — 곱(×5.0)이 아니라 큰 쪽(×2.5)만 곱한다",
          cat_class == "Fire" and both_on == (True, True)
          and abs(both_ratio - frenzy_only) < 0.35,
          "전직=%s 켜짐(불,광분)=%s · 벽 %s→%s(기준 -%s) %s→%s(둘 다 -%s) ×%.2f "
          "(흡수면 ×2.5 · 곱이면 ×5.0) 대기 쿨=%.1f"
          % (cat_class, both_on, s0, s1, stack_loss, s2, s3, both_loss, both_ratio, cd_wait))

    # ---------- ⑦ 원샷 애니메이션 6종 ----------
    skip("⑦ 원샷 애니메이션(대시·끈끈이·요새·광분·구매·채굴)",
         "재는 훅이 없다 — 지금 어떤 상태가 재생 중인지 읽는 훅"
         "(예: TestGetAnimationState(clientId))이 있어야 자동으로 잴 수 있다. 눈으로 볼 것")

    print("\n===== 요약 =====")
    passed = sum(1 for _, ok, _ in RESULTS if ok is True)
    failed = sum(1 for _, ok, _ in RESULTS if ok is False)
    skipped = sum(1 for _, ok, _ in RESULTS if ok is None)
    for name, ok, detail in RESULTS:
        mark = "  OK   " if ok is True else ("  SKIP " if ok is None else "  FAIL ")
        print(mark + name + ("  | " + detail if detail else ""))
    print("PASS %d · FAIL %d · SKIP/NOTE %d" % (passed, failed, skipped))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
