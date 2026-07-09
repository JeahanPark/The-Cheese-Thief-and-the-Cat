# coding=utf-8
import pytest
from fake_server import FakeWesPocoServer
from wesqa import WesPoco

# Poco 노드 스키마: {"name", "payload":{attr...}, "children":[...] or None}
CANNED_TREE = {
    "name": "Root",
    "payload": {"name": "Root", "type": "Root", "visible": True},
    "children": [
        {
            "name": "InventoryPanel",
            "payload": {"name": "InventoryPanel", "type": "Panel", "visible": True},
            "children": [
                {
                    "name": "wood_count",
                    "payload": {"name": "wood_count", "type": "Text",
                                "visible": True, "text": "3"},
                    "children": None,
                }
            ],
        }
    ],
}


@pytest.fixture
def server():
    srv = FakeWesPocoServer({
        "GetSDKVersion": lambda: "wesqa-0.1",
        "Dump": lambda only_visible=True: CANNED_TREE,
    }).start()
    yield srv
    srv.stop()


def test_existing_node_is_found(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game("wood_count").exists() is True


def test_missing_node_is_absent(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game("does_not_exist").exists() is False


def test_read_text_attribute(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game("wood_count").get_text() == "3"
