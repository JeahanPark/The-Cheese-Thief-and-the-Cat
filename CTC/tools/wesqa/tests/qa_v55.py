# -*- coding: utf-8 -*-
"""쥐 핫바의 **상점 안 / 상점 밖** 두 모드 — 사람 손 입력 경로. MPPM 3인.

전제: 메인 에디터가 플레이모드, 클론 2개 접속, 매치 시작 완료.
**호스트=쥐** — 세팅: `python setup_match.py 0 mouse 3` (클론 하나가 고양이).

## 왜 이 스위트가 있는가

쥐 핫바의 핵심은 **자리에 따라 숫자키의 뜻이 바뀐다**는 것인데, 지금까지 QA가 그 분기를
사람 손 경로로 밟은 적이 없다.

| 쥐 위치 | 숫자키 | Enter |
|---|---|---|
| 상점 원반 안 | 빈 칸 = 후보 넘김 / 산 칸 = 키운다 | 고른 칸 구매 |
| 상점 밖 | 산 칸 = **쓴다** / 빈 칸 = 「상점에서만 살 수 있다」 | — |

그리고 2026-09-11 신고의 나머지 절반 — **넘기다 보면 두 칸이 같은 후보를 가리켰다**.
고양이는 칸을 고정해 없앴고(qa_v54), 쥐는 「다른 칸이 이미 보여 주는 후보는 건너뛴다」로 고쳤다.
여기서 그게 실제로 안 겹치는지를 **여러 번 넘겨** 본다.

`TestBuyTechLocal` · `TestUseTech`를 쓰지 않는다 — 그 훅은 핫바 분기를 통째로 건너뛴다.
`TestPressSlotKey` · `TestPressEnterKey`만 쓴다.
"""
import sys, time, io

sys.path.insert(0, r"c:\GitFork\The-Cheese-Thief-and-the-Cat\CTC\tools\wesqa")
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from wesqa import WesPoco

RESULTS = []
EXTRA = []
EXPECTED = 8      # ⓪ 전제 + ①~⑦

CYCLES = 12       # 넘기기를 이만큼 반복해 겹침을 노린다(후보 8 · 빈 칸 5)


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


def slot_labels(host):
    """핫바가 실제로 그린 칸 이름들 — 번호 접두를 뗀다. 꺼진 칸은 훅이 건너뛴다."""
    raw = host.invoke("TestGetHotbarSlots") or ""
    out = []
    for chunk in raw.split("|"):
        if not chunk:
            continue
        label = chunk.split("#")[0]
        parts = label.split("  ", 1)
        out.append(parts[1].strip() if len(parts) > 1 else label.strip())
    return out


def tech_name(label):
    """"요새 Lv1" → "요새" — 레벨 꼬리를 뗀다."""
    return label.split(" Lv")[0].strip()


def empty_names(labels, owned_count):
    """빈 칸이 지금 보여 주는 후보 이름들(산 칸은 뺀다)."""
    return [tech_name(x) for x in labels[owned_count:] if tech_name(x) != "—"]


def move_out_of_shops(host, actor, client_id):
    """`actor`의 몸을 모든 상점 원반 **밖**으로 옮긴다 → 실제로 밖에 섰는가(bool).

    상점 자리는 판마다 시드로 정해진다 — 스폰이 원반 안에 들어앉는 판이 있어
    「밖」은 가정이 아니라 **세워야 하는 전제**다(qa_v51과 같은 이유).
    """
    raw = host.invoke("TestGetShopSpotPositions") or ""
    spots = []
    for chunk in raw.split("|"):
        if "," not in chunk:
            continue
        sx, sz = chunk.split(",")
        spots.append((float(sx), float(sz)))
    if not spots:
        return not bool(host.invoke("TestIsInShop", _clientId=client_id))

    shape = host.invoke("TestGetMapShape") or ""
    half = 15.0
    for part in shape.split(";"):
        if part.startswith("half="):
            half = float(part[5:])
    edge = max(4.0, half - 3.0)

    best, best_gap = None, -1.0
    step = max(2.0, edge / 12.0)
    y = -edge
    while y <= edge:
        x = -edge
        while x <= edge:
            gap = min(((x - sx) ** 2 + (y - sz) ** 2) ** 0.5 for sx, sz in spots)
            if gap > best_gap:
                best, best_gap = (x, y), gap
            x += step
        y += step

    actor.invoke("TestTeleportLocalPlayer", _x=best[0], _z=best[1])
    time.sleep(1.5)
    return not bool(host.invoke("TestIsInShop", _clientId=client_id))


def cycle_and_confirm(host, want, tries=14):
    """빈 칸을 넘겨 `want`를 그 칸에 올린 뒤 **Enter로 확정 구매** — 사람 손 경로 그대로.

    이미 그 후보를 보여 주는 칸이 있으면 **그 칸**을 쓴다: 겹침 회피 때문에 다른 칸은
    그것을 집을 수 없다(그게 이 스위트가 재는 규칙이다). 넘겨서 자리를 비우면 돌아온다.
    돌려주는 것은 (성공 여부, 마지막으로 그 칸이 보여 준 이름).
    """
    labels = slot_labels(host)
    target = 0
    for i, label in enumerate(labels):
        if tech_name(label) == want:
            target = i
            break

    shown = ""
    for _ in range(tries):
        host.invoke("TestPressSlotKey", _slot=target)
        time.sleep(0.55)
        labels = slot_labels(host)
        shown = tech_name(labels[target]) if target < len(labels) else ""
        if shown == want:
            break
    if shown != want:
        return False, shown
    host.invoke("TestPressEnterKey")
    time.sleep(1.4)
    return True, shown


def main():
    p = [WesPoco(instance=i) for i in range(3)]
    ids = [int(x.invoke("TestGetLocalClientId")) for x in p]
    roles = [x.invoke("TestGetRole") for x in p]
    ok_setup = check("⓪ 3인 접속 · 호스트=쥐 · 고양이 1",
                     all(i >= 0 for i in ids) and roles[0] == "Mouse"
                     and roles.count("Cat") == 1,
                     "roles=%s ids=%s" % (roles, ids))
    if not ok_setup:
        print("!! 전제가 안 맞는다 — `python setup_match.py 0 mouse 3`으로 판을 다시 연다")
        return summary(aborted=True)

    host = p[0]
    c = ids[0]
    for _ in range(2):
        host.invoke("TestGrantCheeseAll")
        time.sleep(0.35)

    # ---------- ① 상점 안 ----------
    host.invoke("TestWarpToNearestShop", _clientId=c)
    time.sleep(2.0)
    in_shop = bool(host.invoke("TestIsInShop", _clientId=c))
    mode_in = host.invoke("TestGetHotbarModeText") or ""
    labels_in = slot_labels(host)
    check("① 상점 원반 안 — 모드 줄이 「상점 · 숫자키 = 산다 / 키운다 · Enter = 확정」이고 칸이 5개다",
          in_shop and "상점" in mode_in and "Enter = 확정" in mode_in and len(labels_in) == 5,
          "안=%s · 모드 줄=%r · 칸 %d개" % (in_shop, mode_in, len(labels_in)))

    # ---------- ② 시작부터 다섯 칸이 다르다 ----------
    owned0 = host.invoke("TestGetTechSlotCount", _clientId=c)
    start = empty_names(labels_in, owned0)
    check("② 빈 칸들이 시작부터 서로 다른 후보를 보여 준다",
          len(set(start)) == len(start) and len(start) > 0,
          "빈 칸=%s" % start)

    # ---------- ③ 여러 번 넘겨도 안 겹친다  ← 2026-09-11 신고의 나머지 절반 ----------
    dup_seen = []
    changed = 0
    prev = None
    for i in range(CYCLES):
        host.invoke("TestPressSlotKey", _slot=(owned0 + (i % max(1, 5 - owned0))))
        time.sleep(0.5)
        names = empty_names(slot_labels(host), owned0)
        if len(set(names)) != len(names):
            dup_seen.append(names)
        if prev is not None and names != prev:
            changed += 1
        prev = names
    check("③ 빈 칸을 %d번 넘겨도 두 칸이 같은 후보를 안 보여 준다 · 넘기기는 그대로 된다" % CYCLES,
          not dup_seen and changed > 0,
          "겹친 순간=%s · 바뀐 횟수=%d · 마지막=%s" % (dup_seen[:2], changed, prev))

    # ---------- ④ Enter로 확정 구매 ----------
    bought, shown = cycle_and_confirm(host, "요새")
    fort_lv = host.invoke("TestGetTechLevel", _clientId=c, _techId="Fortress")
    check("④ 넘겨서 고른 칸을 Enter로 산다 — 요새 Lv1이 된다",
          bought and fort_lv == 1,
          "칸이 보여 준 것=%r · 요새 Lv=%s" % (shown, fort_lv))

    # ---------- ⑤ 상점 밖으로 나가면 모드가 뒤집힌다 ----------
    outside = move_out_of_shops(host, host, c)
    time.sleep(0.8)
    mode_out = host.invoke("TestGetHotbarModeText") or ""
    labels_out = slot_labels(host)
    owned_now = host.invoke("TestGetTechSlotCount", _clientId=c)
    empties_out = [tech_name(x) for x in labels_out[owned_now:]]
    check("⑤ 상점 밖 — 모드 줄이 「숫자키 = 쓴다」로 바뀌고 빈 칸은 후보를 안 보여 준다",
          outside and mode_out.startswith("숫자키 = 쓴다")
          and all(x == "—" for x in empties_out),
          "밖=%s · 모드 줄=%r · 빈 칸=%s" % (outside, mode_out, empties_out))

    # ---------- ⑥ 밖에서 빈 칸을 누르면 거절 문구가 뜬다 ----------
    host.invoke("TestPressSlotKey", _slot=4)
    time.sleep(0.6)
    toast = host.invoke("TestGetHotbarToast") or ""
    check("⑥ 상점 밖에서 빈 칸을 누르면 「상점에서만 살 수 있다」가 뜬다",
          "상점에서만" in toast,
          "토스트=%r" % toast)

    # ---------- ⑦ 밖에서 산 액티브가 실제로 선다 ----------
    fort_before = host.invoke("TestGetAnyFortressPos") or ""
    host.invoke("TestPressSlotKey", _slot=0)      # 산 칸 = 쓴다(요새는 즉발 설치)
    time.sleep(2.0)
    fort_after = host.invoke("TestGetAnyFortressPos") or ""
    fort_dist = host.invoke("TestGetFortressDistance")
    check("⑦ 상점 밖에서 산 칸의 숫자키 = 쓴다 — 요새가 실제로 선다",
          fort_after != "" and fort_after != fort_before
          and fort_dist is not None and fort_dist >= 0,
          "설치 전=%r → 후=%r · 거리 %.1fm · 토스트=%r"
          % (fort_before, fort_after, fort_dist if fort_dist is not None else -1,
             host.invoke("TestGetHotbarToast")))

    note("핫바 칸", "%s" % slot_labels(host))
    note("품절", "%s" % host.invoke("TestGetSoldOutTechs"))
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
