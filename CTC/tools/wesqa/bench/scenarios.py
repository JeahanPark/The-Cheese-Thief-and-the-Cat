# coding=utf-8
"""wesqa 단언 스위트 — 로그인 화면 read-only 검증(M1 수준).
각 check = (이름, 단언함수(game)->bool). M2 입력/M4 시나리오가 붙으면 확장."""


def login_checks():
    return [
        ("StartButton 존재", lambda g: g("StartButton").exists()),
        ("OptionButton 존재", lambda g: g("OptionButton").exists()),
        ("ExitButton 존재", lambda g: g("ExitButton").exists()),
        ("StartButton 클릭가능(Button)", lambda g: g("StartButton").attr("type") == "Button"),
    ]
