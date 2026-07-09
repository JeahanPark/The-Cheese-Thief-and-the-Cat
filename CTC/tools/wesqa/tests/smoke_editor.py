# coding=utf-8
"""에디터 플레이모드의 실제 게임에 붙어 UI 노드를 단언하는 수동 스모크.
사용: Unity 에디터에서 씬 플레이 중 실행.
    python tests/smoke_editor.py <존재하는_노드이름>
인자를 주지 않으면 화면의 첫 Button 노드를 자동으로 찾아 단언한다."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
from wesqa import WesPoco


def _first_button_name(game):
    root = game.agent.rpc.call("Dump", True).wait()[0]
    found = []

    def walk(n):
        p = n.get("payload", {})
        if p.get("type") == "Button":
            found.append(p.get("name"))
        for c in (n.get("children") or []):
            walk(c)

    walk(root)
    return found[0] if found else None


def main():
    game = WesPoco(instance=0)
    print("sdk:", game.sdk_version())

    node_name = sys.argv[1] if len(sys.argv) > 1 else _first_button_name(game)
    assert node_name, "화면에서 단언할 노드를 찾지 못함 — 씬에 UI가 떠 있는지 확인"

    found = game(node_name).exists()
    print(f"node '{node_name}' exists:", found)
    assert found, f"'{node_name}' 노드를 찾지 못함 — 덤프 스키마/씬 확인"

    assert game("__ghost_node_that_must_not_exist__").exists() is False, "유령 노드가 존재로 나옴"
    print("SMOKE OK")


if __name__ == "__main__":
    main()
