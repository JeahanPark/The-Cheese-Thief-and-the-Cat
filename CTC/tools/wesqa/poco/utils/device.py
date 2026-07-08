# coding=utf-8
# stripped fork: airtest 의존 device 유틸 제거. wesqa는 직접 TCP 접속하므로 불필요.
from __future__ import absolute_import


class VirtualDevice(object):
    """Stub — airtest 없는 환경에서 import 오류 방지용."""
    def __init__(self, ip='localhost'):
        self.ip = ip

    @property
    def uuid(self):
        return 'virtual-device'

    def get_current_resolution(self):
        return [1920, 1080]

    def get_ip_address(self):
        return self.ip


def default_device():
    """Stub — wesqa는 airtest device를 사용하지 않는다."""
    return VirtualDevice()
