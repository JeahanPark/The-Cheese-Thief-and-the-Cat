# coding=utf-8
import pytest
from fake_server import FakeWesPocoServer
from wesqa import WesPoco


@pytest.fixture
def server():
    srv = FakeWesPocoServer({"GetSDKVersion": lambda: "wesqa-0.1"}).start()
    yield srv
    srv.stop()


def test_handshake_returns_sdk_version(server):
    game = WesPoco(host=server.host, port=server.port)
    assert game.sdk_version() == "wesqa-0.1"
