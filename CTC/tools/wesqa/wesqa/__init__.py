# coding=utf-8
import os
import tempfile

from poco.pocofw import Poco
from .agent import WesPocoAgent
from .hooks import HookSignatureError, check_call

__all__ = ["WesPoco", "connect_all", "HookSignatureError"]

DEFAULT_HOST = "localhost"
BASE_PORT = 5001


def _resolve_port(base_port):
    """게임이 기록한 실제 포트를 읽는다. 고아 소켓 때문에 폴백 포트로 떴을 수 있다."""
    path = os.path.join(tempfile.gettempdir(), "ctc-mppm-qa",
                        "wesqa_port_%d.txt" % base_port)
    try:
        with open(path) as f:
            return int(f.read().strip())
    except (IOError, OSError, ValueError):
        return base_port


def connect_all(count, host="localhost", **options):
    """instance 0..count-1을 각각 WesPoco로 연결해 리스트 반환."""
    return [WesPoco(instance=i, host=host, **options) for i in range(count)]


class WesPoco(Poco):
    """게임 내 WesPocoServer에 직접 붙는 Poco. 포트 = BASE_PORT + instance."""

    def __init__(self, instance=0, host=DEFAULT_HOST, port=None, **options):
        addr = (host, port if port is not None else _resolve_port(BASE_PORT + instance))
        agent = WesPocoAgent(addr)
        options.setdefault("action_interval", 0.5)
        options["reevaluate_volatile_attributes"] = True
        super(WesPoco, self).__init__(agent, **options)

    def sdk_version(self):
        return self.agent.get_sdk_version()

    def invoke(self, listener, **kwargs):
        """게임 내 TestManager.<listener>(**kwargs)를 호출. 반환값 또는 None.

        보내기 전에 `hooks.check_call`로 이름·인자를 대조한다 — 게임 쪽 다리는
        모르는 인자를 조용히 버리고 빠진 자리에 0(=호스트 clientId)을 넣기 때문이다.
        """
        check_call(listener, kwargs)
        cb = self.agent.rpc.call("Invoke", listener=listener, data=kwargs)
        value, error = cb.wait()
        if error is not None:
            raise RuntimeError("invoke '%s' failed: %s" % (listener, error))
        return value

    def screenshot(self, path=None, width=0):
        """현재 화면 캡처 → BGR numpy 이미지. path 주면 파일로도 저장."""
        from . import vision
        b64, _fmt = self.agent.screen.getScreen(width)
        img = vision.b64_to_image(b64)
        if path:
            import cv2
            cv2.imwrite(path, img)
        return img
