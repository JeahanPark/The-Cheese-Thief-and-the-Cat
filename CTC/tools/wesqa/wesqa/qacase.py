# -*- coding: utf-8 -*-
"""QA 케이스 ID를 **파이썬 테스트에 붙이는** 유일한 문법.

대장의 원본은 `Assets/Scripts/Tests/EditMode/QaCases.cs`다. 여기 있는 것은 부착 수단뿐이다.

    · wesqa 스위트(`qa_v*.py`)  →  check("…", ok, detail, case="E-18")
    · pytest(`test_*.py`)       →  @qa_case("E-04")

둘 다 **기존 문법 안**이라 새 러너도 새 포맷도 없다. 붙인 ID가 대장에 없거나,
대장이 「미구현」이라 말하는데 여기 붙어 있으면 `QaCaseRegistryTests`가 깨진다.
"""

import re

_ID = re.compile(r"^[A-Z]-[0-9]{2}$")


def qa_case(*ids):
    """이 테스트가 덮는 케이스 ID를 박는다. 실행에는 아무 영향이 없다 — 표식일 뿐이다.

    스캐너는 소스에서 `qa_case(...)` 안의 문자열을 찾으므로, ID는 **리터럴로** 적는다
    (변수로 넘기면 대장이 못 본다).
    """
    for i in ids:
        if not _ID.match(i):
            raise ValueError("케이스 ID 형식이 아니다: %r" % (i,))

    def wrap(fn):
        existing = tuple(getattr(fn, "qa_cases", ()))
        fn.qa_cases = existing + tuple(ids)
        return fn

    return wrap
