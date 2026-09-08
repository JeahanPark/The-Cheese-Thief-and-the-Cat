# -*- coding: utf-8 -*-
"""QA 케이스 대장의 **파이썬 쪽 대칭** (전체 검수 3단계).

대장의 원본은 C# 한 곳이다 — `Assets/Scripts/Tests/EditMode/QaCases.cs`.
C# 쪽 `QaCaseRegistryTests`가 같은 검사를 양쪽 소스에 대고 하지만, **유니티를 안 켜도**
파이썬 부착이 어긋난 걸 알 수 있어야 한다. 이 파일이 그 자리다.
`pytest.ini`가 수집하는 `test_*.py`라 사람이 따로 기억할 필요가 없다.

여기서 깨지는 것:

    ① 파이썬에 박힌 케이스 ID가 대장에 없다        (오타이거나 대장에 안 올렸다)
    ② 대장이 「구현됨」이라는데 파이썬에 부착이 없다  (없는 것을 있다고 적었다)
    ③ 대장이 「미구현」이라는데 파이썬에 부착이 있다  (상태를 안 올렸다)
    ④ 폐기 번호가 다시 쓰였다                       (번호는 재사용하지 않는다)
"""
import io
import os
import re

import pytest


HERE = os.path.dirname(os.path.abspath(__file__))
WESQA = os.path.dirname(HERE)                                   # tools/wesqa
CTC = os.path.dirname(os.path.dirname(WESQA))                   # CTC/
REGISTRY = os.path.join(CTC, "Assets", "Scripts", "Tests", "EditMode", "QaCases.cs")

_OF = re.compile(r'QaCase\.Of\(\s*"([A-Z]-[0-9]{2})".*?QaLayer\.(\w+)\s*,\s*QaStatus\.(\w+)',
                 re.S)
_MANUAL = re.compile(r'QaCase\.ManualOf\(\s*"([A-Z]-[0-9]{2})"')
_RETIRED = re.compile(r'QaCase\.RetiredOf\(\s*"([A-Z]-[0-9]{2})"')

_KWARG = re.compile(r'\bcase\s*=\s*(["\'])([^"\']*)\1')   # `case="B-04 C-23"`처럼 둘 이상도 받는다
_BARE = re.compile(r'\b([A-Z]-[0-9]{2})\b')
_DECOR = re.compile(r'qa_case\s*\(([^)]*)\)')
_LITERAL = re.compile(r'["\']([A-Z]-[0-9]{2})["\']')


def load_registry():
    """ID → (층, 상태). C# 선언을 정규식으로 읽는다 — 유니티가 없어도 돈다."""
    with io.open(REGISTRY, encoding="utf-8") as f:
        src = f.read()
    cases = {}
    for cid, layer, status in _OF.findall(src):
        cases[cid] = (layer, status)
    for cid in _MANUAL.findall(src):
        cases[cid] = ("Manual", "Implemented")
    for cid in _RETIRED.findall(src):
        cases[cid] = ("Manual", "Retired")
    return cases


def scan_python():
    """ID → 그 ID가 박힌 파이썬 파일들(tools/wesqa 상대 경로)."""
    found = {}
    for root, dirs, files in os.walk(WESQA):
        dirs[:] = [d for d in dirs if d != "__pycache__"]
        for name in sorted(files):
            if not name.endswith(".py"):
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, WESQA).replace("\\", "/")
            if rel.endswith("tests/test_case_registry.py") or rel.endswith("wesqa/qacase.py"):
                continue                                  # 부착 수단 자신은 세지 않는다
            with io.open(path, encoding="utf-8") as f:
                text = f.read()
            ids = set()
            for _quote, body in _KWARG.findall(text):
                ids.update(_BARE.findall(body))
            for args in _DECOR.findall(text):
                ids.update(_LITERAL.findall(args))
            for cid in ids:
                found.setdefault(cid, []).append(rel)
    for files in found.values():
        files.sort()
    return found


REGISTRY_CASES = load_registry()
ATTACHED = scan_python()


def test_registry_is_readable():
    assert len(REGISTRY_CASES) >= 100, \
        "대장을 못 읽었다(경로가 바뀌었나?): %s → %d건" % (REGISTRY, len(REGISTRY_CASES))
    assert "E-04" in REGISTRY_CASES


def test_every_attached_id_is_declared():
    """① 파이썬에 박힌 ID는 반드시 대장에 있다."""
    unknown = ["%s (%s)" % (cid, " · ".join(files))
               for cid, files in sorted(ATTACHED.items()) if cid not in REGISTRY_CASES]
    assert not unknown, \
        "대장에 없는 케이스 ID가 파이썬에 박혀 있다 — QaCases.cs에 올리거나 오타를 고쳐라: %s" \
        % " / ".join(unknown)


def test_python_layer_cases_declared_implemented_are_attached():
    """② 「wesqa·pytest 층 · 구현됨」이라 적힌 케이스에는 파이썬 부착이 있다."""
    orphans = [cid for cid, (layer, status) in sorted(REGISTRY_CASES.items())
               if layer in ("Wesqa", "Pytest") and status == "Implemented"
               and cid not in ATTACHED]
    assert not orphans, \
        "「구현됨」이라 선언했는데 그 ID가 박힌 파이썬 테스트가 없다 — 대장이 거짓말을 한다: %s" \
        % ", ".join(orphans)


def test_missing_cases_have_no_python_attachment():
    """③ 「미구현」인데 붙었으면 상태를 올려야 한다."""
    stale = ["%s → %s" % (cid, " · ".join(ATTACHED[cid]))
             for cid, (_layer, status) in sorted(REGISTRY_CASES.items())
             if status == "Missing" and cid in ATTACHED]
    assert not stale, \
        "구현이 붙었는데 대장이 아직 「미구현」이다 — QaCases.cs의 상태를 올려라: %s" \
        % " / ".join(stale)


def test_retired_ids_are_not_reused():
    """④ 폐기 번호는 아무 데도 안 붙어 있다."""
    revived = ["%s → %s" % (cid, " · ".join(ATTACHED[cid]))
               for cid, (_layer, status) in sorted(REGISTRY_CASES.items())
               if status == "Retired" and cid in ATTACHED]
    assert not revived, "폐기 번호가 다시 쓰였다 — 번호는 재사용하지 않는다: %s" % " / ".join(revived)


@pytest.mark.parametrize("cid", sorted(ATTACHED))
def test_attachment_sits_in_the_declared_layer(cid):
    """선언한 층과 실제로 붙은 파일 종류가 같다(스위트 vs pytest)."""
    layer = REGISTRY_CASES.get(cid, (None, None))[0]
    if layer not in ("Wesqa", "Pytest"):
        pytest.skip("%s는 파이썬 층이 아니다(%s) — C# 쪽 검사가 본다" % (cid, layer))
    names = [os.path.basename(f) for f in ATTACHED[cid]]
    want = "qa_v" if layer == "Wesqa" else "test_"
    assert any(n.startswith(want) for n in names), \
        "%s는 %s 층인데 붙은 자리가 %s다" % (cid, layer, names)
