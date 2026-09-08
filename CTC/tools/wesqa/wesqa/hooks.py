# -*- coding: utf-8 -*-
"""TestManager 훅 명부 — 파이썬이 부르기 전에 이름·인자를 대조한다.

**왜 필요한가.** 게임 쪽 다리(`Assets/WesQA/Runtime/InvokeBridge.cs`)는 인자 바인딩이
조용하다:

    ① 메서드가 안 쓰는 키는 **에러 없이 버린다**.
    ② 대응 파라미터가 없으면 `default(T)` — `long _clientId`는 **0 = 호스트 clientId**.
    ③ 이름이 하나도 안 맞아도 **개수만 같으면 JObject 순서대로** 꽂는다.
    ④ 메서드 이름 조회는 `IgnoreCase`라 대소문자 오타가 안 걸린다.

2026-08-09 「엉뚱한 쥐를 가둔」 사고가 ①+②였다(`TestManager.Map.cs`의 전례 주석).
다리 자체는 유니티 C#이라 여기서 못 고친다 — 대신 **부르기 전에 파이썬이 막는다.**
이름이 안 맞으면 조용히 0을 보내는 대신 `HookSignatureError`로 시끄럽게 죽는다.

명부의 원본은 `Assets/Scripts/Testing/TestManager*.cs` 소스다. 매 실행 시 다시 읽으므로
훅이 바뀌면 따라간다. 소스를 못 찾으면(다른 기계에서 스위트만 돌리는 경우) 검사를
끄고 **경고를 한 번 찍는다** — 조용히 넘어가지 않는다.

끄는 법: 환경변수 `WESQA_HOOK_CHECK=off`.
소스 위치 지정: 환경변수 `CTC_TESTING_DIR`.
"""
import os
import re
import sys

__all__ = ["HookSignatureError", "load_hooks", "check_call", "registry_path",
           "registry_available"]


class HookSignatureError(Exception):
    """훅 이름 또는 인자 이름이 `TestManager` 명부와 안 맞는다."""


# `public <반환형> TestXxx(<params>)` — TestManager는 전부 public instance 메서드다.
_SIG = re.compile(
    r"^\s*public\s+(?:static\s+|async\s+|override\s+|virtual\s+)*"
    r"[A-Za-z0-9_<>\[\]\.\?]+\s+"
    r"(Test[A-Za-z0-9_]*)\s*\(([^)]*)\)",
    re.MULTILINE)

_CACHE = {}


def registry_path():
    """`Assets/Scripts/Testing` 디렉터리 경로(존재 여부는 안 본다)."""
    override = os.environ.get("CTC_TESTING_DIR")
    if override:
        return override
    here = os.path.abspath(__file__)
    # .../CTC/tools/wesqa/wesqa/hooks.py → .../CTC
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(here))))
    return os.path.join(root, "Assets", "Scripts", "Testing")


def _parse_params(raw):
    """`"long _clientId = -1, float _x"` → `[("_clientId", True), ("_x", False)]`."""
    out = []
    for part in raw.split(","):
        part = part.strip()
        if not part:
            continue
        has_default = "=" in part
        head = part.split("=", 1)[0].strip()
        # `params string[] _xs` 같은 수식어를 걷어내고 마지막 토큰이 이름이다.
        tokens = head.replace("\t", " ").split()
        if not tokens:
            continue
        out.append((tokens[-1], has_default))
    return out


def load_hooks(path=None, force=False):
    """`{훅이름: [(인자이름, 기본값있음), ...]}`. 소스를 못 읽으면 `None`."""
    path = path or registry_path()
    if not force and path in _CACHE:
        return _CACHE[path]
    hooks = None
    if os.path.isdir(path):
        hooks = {}
        for name in sorted(os.listdir(path)):
            if not name.endswith(".cs"):
                continue
            with open(os.path.join(path, name), encoding="utf-8") as f:
                src = f.read()
            for m in _SIG.finditer(src):
                hooks[m.group(1)] = _parse_params(m.group(2))
        if not hooks:
            hooks = None
    _CACHE[path] = hooks
    return hooks


def registry_available():
    return load_hooks() is not None


_WARNED = [False]


def _warn_missing():
    if _WARNED[0]:
        return
    _WARNED[0] = True
    sys.stderr.write(
        "!! wesqa: TestManager 소스를 못 찾아 훅 인자 검사를 못 한다 (%s).\n"
        "   인자 이름 오타가 조용히 0(=호스트 clientId)으로 들어갈 수 있다.\n"
        "   CTC_TESTING_DIR로 경로를 지정하거나 저장소 안에서 실행할 것.\n"
        % registry_path())
    sys.stderr.flush()


def check_call(listener, kwargs, hooks=None):
    """부르기 전 대조. 어긋나면 `HookSignatureError`.

    잡는 것: ① 없는 훅 이름 ② 대소문자만 다른 이름(다리가 IgnoreCase라 통과하던 것)
    ③ 훅이 안 받는 인자 이름 ④ 기본값 없는 인자 누락(= `default(T)` 사고).
    """
    if os.environ.get("WESQA_HOOK_CHECK", "").lower() in ("off", "0", "false"):
        return
    if hooks is None:
        hooks = load_hooks()
    if hooks is None:
        _warn_missing()
        return

    if listener not in hooks:
        lowered = {k.lower(): k for k in hooks}
        alt = lowered.get(listener.lower())
        if alt is not None:
            raise HookSignatureError(
                "훅 이름의 대소문자가 다르다: '%s' → '%s'. "
                "다리는 IgnoreCase라 통과하지만 명부와 어긋난 것이다." % (listener, alt))
        raise HookSignatureError(
            "그런 훅이 없다: '%s' (TestManager 명부 %d개)" % (listener, len(hooks)))

    params = hooks[listener]
    names = [p for p, _ in params]
    unknown = [k for k in kwargs if k not in names]
    if unknown:
        raise HookSignatureError(
            "'%s'가 안 받는 인자: %s — 받는 인자는 %s. "
            "다리는 이걸 조용히 버리고 빠진 자리에 0을 넣는다(= 호스트 clientId)."
            % (listener, sorted(unknown), names or "(없음)"))
    missing = [p for p, has_default in params if not has_default and p not in kwargs]
    if missing:
        raise HookSignatureError(
            "'%s'에 필수 인자가 빠졌다: %s — 받는 인자는 %s. "
            "다리는 빠진 자리에 default(T)=0을 넣는다." % (listener, missing, names))
