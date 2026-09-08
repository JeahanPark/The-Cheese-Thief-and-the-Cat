# -*- coding: utf-8 -*-
"""훅 이름·인자 대조 (감사 E: E-01·E-02·E-03·E-04). 게임 없이 돈다.

게임 쪽 다리(`InvokeBridge`)는 인자 바인딩이 조용하다 — 모르는 키를 **에러 없이 버리고**,
빠진 자리에 `default(T)`(= `0` = 호스트 clientId)를 넣고, 이름이 안 맞아도 **개수만 같으면
순서로** 꽂는다. 2026-08-09 「엉뚱한 쥐를 가둔」 사고가 그것이었다.

다리 자체는 유니티 C#이라 여기서 못 고친다. 대신 **부르기 전에 파이썬이 막고**,
이 테스트가 스위트 전량을 정적으로 훑어 오타를 실행 전에 잡는다.
"""
import ast
import io
import os

import pytest

from wesqa.hooks import HookSignatureError, check_call, load_hooks, registry_path
from wesqa.qacase import qa_case

HERE = os.path.dirname(os.path.abspath(__file__))
HOOKS = load_hooks()


def _py_files():
    for root, _dirs, files in os.walk(HERE):
        if "__pycache__" in root:
            continue
        for f in sorted(files):
            if f.endswith(".py"):
                yield os.path.join(root, f)


def _invocations(path):
    """`<무엇>.invoke("TestX", _a=…)` 호출을 모은다 → (줄, 훅이름, [인자이름])."""
    with io.open(path, encoding="utf-8") as f:
        tree = ast.parse(f.read(), filename=path)
    for node in ast.walk(tree):
        if not (isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
                and node.func.attr == "invoke"):
            continue
        if not node.args:
            continue
        first = node.args[0]
        if not (isinstance(first, ast.Constant) and isinstance(first.value, str)):
            continue                      # 이름을 변수로 넘기는 자리(setup_match)는 못 본다
        yield node.lineno, first.value, [k.arg for k in node.keywords if k.arg]


def test_registry_loads():
    assert HOOKS is not None, "TestManager 소스를 못 읽었다: %s" % registry_path()
    assert len(HOOKS) > 150, len(HOOKS)
    assert "TestGetLocalClientId" in HOOKS


@qa_case("E-04")
def test_every_hook_call_matches_the_signature():
    """스위트가 부르는 모든 훅 이름·인자가 `TestManager` 명부와 정확히 맞는다."""
    assert HOOKS is not None
    bad = []
    for path in _py_files():
        for lineno, name, kwargs in _invocations(path):
            try:
                check_call(name, {k: None for k in kwargs}, hooks=HOOKS)
            except HookSignatureError as e:
                bad.append("%s:%d %s" % (os.path.relpath(path, HERE), lineno, e))
    assert not bad, "훅 호출이 명부와 어긋난다:\n" + "\n".join(bad)


# ---------- check_call 자신의 단위 검사 ----------

FAKE = {"TestKick": [("_clientId", False), ("_force", True)], "TestPing": []}


@qa_case("E-01")
def test_unknown_kwarg_raises():
    with pytest.raises(HookSignatureError):
        check_call("TestKick", {"_clientID": 3}, hooks=FAKE)   # 대소문자 오타


@qa_case("E-02")
def test_missing_required_arg_raises():
    """빠진 `_clientId`가 조용히 0(=호스트)이 되지 않는다 — 2026-08-09 사고."""
    with pytest.raises(HookSignatureError):
        check_call("TestKick", {}, hooks=FAKE)


@qa_case("E-03")
def test_wrong_names_same_count_raises():
    """개수만 같다고 순서로 꽂히던 폴백 경로를 파이썬이 먼저 막는다."""
    with pytest.raises(HookSignatureError):
        check_call("TestKick", {"_id": 3, "_f": True}, hooks=FAKE)


@qa_case("E-04")
def test_case_mismatch_on_hook_name_raises():
    """다리는 IgnoreCase라 통과하지만 명부와 어긋난 것이다."""
    with pytest.raises(HookSignatureError):
        check_call("testkick", {"_clientId": 3}, hooks=FAKE)


def test_unknown_hook_raises():
    with pytest.raises(HookSignatureError):
        check_call("TestNope", {}, hooks=FAKE)


def test_valid_call_passes():
    check_call("TestKick", {"_clientId": 3}, hooks=FAKE)        # 기본값 있는 인자는 생략 가능
    check_call("TestKick", {"_clientId": 3, "_force": True}, hooks=FAKE)
    check_call("TestPing", {}, hooks=FAKE)
