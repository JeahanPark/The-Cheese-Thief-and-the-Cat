# coding=utf-8
"""동작(플로우) 회귀 검증 — 실 게임에서 입력→상태변화를 단언(M2).
가짜서버 mutation이 아닌 라이브 게임 대상. 플레이모드에서 호출."""
import time


def _names(game):
    root = game.agent.rpc.call("Dump", True).wait()[0]
    out = []

    def w(n):
        out.append((n.get("payload") or {}).get("name"))
        for c in (n.get("children") or []):
            w(c)

    w(root)
    return set(out)


def action_checks():
    # (이름, 동작함수(game)->bool) — 입력 후 의도한 UI 변화가 나타나면 True
    def login_to_lobby(g):
        assert g("StartButton").exists(), "StartButton 없음(시작 화면 아님?)"
        before = _names(g)
        g("StartButton").click()
        time.sleep(0.8)
        after = _names(g)
        # 로그인 화면이 사라지고 로비 진입 노드가 등장해야 함
        appeared = after - before
        return ("StartButton" not in after) and any(
            n in appeared for n in ("LobbyPopup", "RoomCreateButton")
        )

    return [
        ("로그인→로비 전환(StartButton)", login_to_lobby),
    ]
