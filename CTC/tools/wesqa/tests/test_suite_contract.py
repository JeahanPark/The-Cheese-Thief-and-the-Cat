# -*- coding: utf-8 -*-
"""qa_v*.py 스위트의 **채점 규약**을 정적으로 강제한다 (감사 E: E-05·E-07·E-08).

게임이 없어도 돈다 — 소스를 `ast`로 읽을 뿐이다. pytest가 실제로 수집하는 자리에
두는 이유는, 사람이 따로 기억하지 않아도 되게 하기 위해서다(`pytest.ini`의 `test_*.py`).

여기서 막는 것:

    ① 실패해도 exit 0        — 2026-09-08까지 스위트 7개가 그랬다(F-2)
    ② `or True` 류 항진명제  — `qa_v41`에 실제로 박혀 있었다(F-4)
    ③ 조용한 조건부 건너뛰기 — 분모가 실행마다 달라져 "16/16 통과"가 24항목 중
                              8개를 안 돈 것일 수 있었다(F-11)
    ④ 실패 센티널(-1)을 성공 쪽으로 바꾸는 fail-open — `qa_v49`가 그랬다(F-5)
"""
import ast
import io
import os

import pytest
from wesqa.qacase import qa_case

HERE = os.path.dirname(os.path.abspath(__file__))
SUITES = sorted(f for f in os.listdir(HERE)
                if f.startswith("qa_v") and f.endswith(".py"))
RECORDERS = ("check", "skip")          # 분모에 들어가는 것. note()는 참고(EXTRA)라 뺀다


def _tree(name):
    with io.open(os.path.join(HERE, name), encoding="utf-8") as f:
        return ast.parse(f.read(), filename=name)


def _const(tree, name):
    for node in tree.body:
        if isinstance(node, ast.Assign):
            for t in node.targets:
                if isinstance(t, ast.Name) and t.id == name:
                    return ast.literal_eval(node.value)
    return None


def test_suites_exist():
    assert len(SUITES) >= 14, SUITES


@pytest.mark.parametrize("name", SUITES)
@qa_case("E-05")
def test_exits_nonzero_on_failure(name):
    """모듈 최하단이 `sys.exit(main())`이고 `summary()`가 값을 돌려준다."""
    tree = _tree(name)
    last = tree.body[-1]
    assert isinstance(last, ast.If), "%s: 최하단이 `if __name__ == \"__main__\":`가 아니다" % name
    src = ast.dump(last)
    assert "sys" in src and "exit" in src and "main" in src, \
        "%s: 최하단이 `sys.exit(main())`이 아니다 — 실패해도 exit 0이 된다" % name

    summary = next((n for n in tree.body
                    if isinstance(n, ast.FunctionDef) and n.name == "summary"), None)
    assert summary is not None, "%s: 모듈 수준 summary()가 없다" % name
    returns = [n for n in ast.walk(summary)
               if isinstance(n, ast.Return) and n.value is not None]
    assert returns, "%s: summary()에 return문이 없다 — sys.exit()로 감싸도 0이다" % name

    # main()의 모든 종료 경로가 값을 돌려준다
    main = next((n for n in tree.body
                 if isinstance(n, ast.FunctionDef) and n.name == "main"), None)
    assert main is not None, "%s: main()이 없다" % name
    bare = [n.lineno for n in ast.walk(main)
            if isinstance(n, ast.Return) and n.value is None]
    assert not bare, \
        "%s:%s — 값 없는 return이 main()에 있다. 조기 중단이 exit 0으로 나간다" % (name, bare)


@pytest.mark.parametrize("name", SUITES)
@qa_case("E-07")
def test_no_tautological_assertion(name):
    """`or True` · `and False` 처럼 **절대 실패할 수 없는** 단언을 막는다."""
    tree = _tree(name)
    bad = []
    for node in ast.walk(tree):
        if not (isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
                and node.func.id in RECORDERS):
            continue
        for sub in ast.walk(node):
            if isinstance(sub, ast.BoolOp):
                for v in sub.values:
                    if isinstance(v, ast.Constant) and isinstance(v.value, bool):
                        bad.append((node.lineno, ast.dump(sub)[:60]))
    assert not bad, "%s: 항진명제 단언 %s" % (name, bad)


@pytest.mark.parametrize("name", SUITES)
@qa_case("E-08")
def test_declared_item_count_matches_sites(name):
    """`EXPECTED`가 정적 항목 수와 같다.

    if/else로 갈라지는 자리는 **양쪽이 같은 수를 기록해야** 한다 — 한쪽만 기록하면
    분모가 실행마다 달라진다(감사 E §2-4). 그래서 `max(body, orelse)`로 세고,
    한쪽이 비면 아래 `test_no_silent_conditional_skip`이 따로 잡는다.
    """
    tree = _tree(name)
    expected = _const(tree, "EXPECTED")
    assert isinstance(expected, int), "%s: EXPECTED 상수가 없다" % name

    def _exits(body):
        """이 갈래가 main을 빠져나가며 끝나는가 — 중단 가드를 가려낸다."""
        return bool(body) and isinstance(body[-1], ast.Return)

    def count(body):
        total = 0
        for node in body:
            if isinstance(node, ast.If):
                # 조건식 자체가 기록할 수 있다 — `if not check("전제 …"): return`
                total += sum(1 for s in ast.walk(node.test)
                             if isinstance(s, ast.Call) and isinstance(s.func, ast.Name)
                             and s.func.id in RECORDERS)
                # 중단 가드(`if not setup(p): check(실패); return summary(aborted=True)`)는
                # 정상 판에서 한 번도 안 돈다 — 분모에 넣으면 매 판 「미실행 1」이 뜬다.
                # 갈래가 return으로 끝나면 그 갈래는 대안 종착점이지 항목이 아니다.
                body_exits = _exits(node.body)
                else_exits = _exits(node.orelse)
                if body_exits and not else_exits:
                    total += count(node.orelse)
                elif else_exits and not body_exits:
                    total += count(node.body)
                elif body_exits and else_exits:
                    total += 0        # 양쪽 다 종착점 — 어느 쪽도 정상 경로가 아니다
                else:
                    total += max(count(node.body), count(node.orelse))
                continue
            if isinstance(node, (ast.For, ast.While)):
                # 반복 안의 기록은 횟수가 실행에 달렸다 — 분모에 넣지 않는다
                inner = count(node.body) + count(node.orelse)
                assert inner == 0, \
                    "%s:%d — 반복 안에서 %s를 부른다. 분모가 실행마다 달라진다" \
                    % (name, node.lineno, "/".join(RECORDERS))
                continue
            if isinstance(node, ast.Expr) and isinstance(node.value, ast.Call) \
                    and isinstance(node.value.func, ast.Name) \
                    and node.value.func.id in RECORDERS:
                total += 1
                continue
            # `if not check(...)` 처럼 식 안에 숨은 호출까지 센다
            direct = [s for s in ast.walk(node)
                      if isinstance(s, ast.Call) and isinstance(s.func, ast.Name)
                      and s.func.id in RECORDERS]
            if direct and not isinstance(node, (ast.FunctionDef, ast.If,
                                                ast.For, ast.While)):
                total += len(direct)
                continue
            for field in ("body", "orelse", "finalbody"):
                inner = getattr(node, field, None)
                if isinstance(inner, list) and inner and isinstance(inner[0], ast.stmt):
                    total += count(inner)
        return total

    main = next(n for n in tree.body
                if isinstance(n, ast.FunctionDef) and n.name == "main")
    sites = count(main.body)
    assert sites == expected, \
        "%s: EXPECTED=%d인데 정적 항목 수는 %d다 — 선언과 구현이 어긋난다" \
        % (name, expected, sites)


@pytest.mark.parametrize("name", SUITES)
@qa_case("E-08")
def test_no_silent_conditional_skip(name):
    """항목을 감싼 `if`에 else가 없으면 **조용히 사라지는 항목**이 생긴다."""
    tree = _tree(name)
    main = next(n for n in tree.body
                if isinstance(n, ast.FunctionDef) and n.name == "main")

    def n_records(body):
        return sum(1 for node in body for s in ast.walk(node)
                   if isinstance(s, ast.Call) and isinstance(s.func, ast.Name)
                   and s.func.id in RECORDERS)

    bad = []
    for node in ast.walk(main):
        if not isinstance(node, ast.If):
            continue
        body_n = n_records(node.body)
        else_n = n_records(node.orelse)
        if body_n == 0:
            continue
        # 조기 중단(return summary(aborted=True))은 요약이 「미실행」으로 남긴다
        aborts = any(isinstance(s, ast.Return) for s in node.body)
        if aborts:
            continue
        if else_n < body_n:
            bad.append((node.lineno, body_n, else_n))
    assert not bad, (
        "%s: 조건부로 사라지는 항목이 있다 (줄, if쪽, else쪽) = %s — "
        "else에 skip()을 남겨라. 결과에 안 남는 건너뛰기는 통과가 아니다" % (name, bad))


@pytest.mark.parametrize("name", SUITES)
@qa_case("E-07")
def test_no_fail_open_sentinel(name):
    """훅의 실패 센티널(-1)을 무한대로 바꿔 단언을 통과시키지 않는다.

    `qa_v49`가 실제로 그랬다 — 미니맵이 통째로 없어도 「적 마커 0」이 초록이었다.
    """
    with io.open(os.path.join(HERE, name), encoding="utf-8") as f:
        lines = f.readlines()
    bad = []
    for i, line in enumerate(lines, 1):
        stripped = line.split("#", 1)[0]
        if 'float("inf")' not in stripped and "float('inf')" not in stripped:
            continue
        if "< 0" in stripped or "is None" in stripped or "== -1" in stripped:
            bad.append((i, line.strip()))
    assert not bad, \
        "%s: 실패 센티널을 무한대로 바꾼다 %s — 못 잰 것은 실패다" % (name, bad)
