# coding=utf-8
from poco.agent import PocoAgent
from poco.drivers.std.dumper import StdDumper
from poco.drivers.std.attributor import StdAttributor
from poco.drivers.std.screen import StdScreen
from poco.drivers.std.inputs import StdInput
from poco.freezeui.hierarchy import FrozenUIHierarchy
from poco.utils.simplerpc.rpcclient import RpcClient
from poco.utils.simplerpc.transport.tcp.main import TcpClient
from poco.utils.simplerpc.utils import sync_wrapper


class WesPocoAgent(PocoAgent):
    """원본 StdPocoAgent에서 airtest 의존(AirtestInput·connect_device)을 제거하고
    host:port로 직접 연결하는 에이전트. 입력·스크린샷은 전부 RPC(StdInput/StdScreen)."""

    def __init__(self, addr):
        self.conn = TcpClient(addr)
        self.c = RpcClient(self.conn)
        self.c.DEBUG = False
        self.c.connect()

        hierarchy = FrozenUIHierarchy(StdDumper(self.c), StdAttributor(self.c))
        screen = StdScreen(self.c)
        inputs = StdInput(self.c)
        super(WesPocoAgent, self).__init__(hierarchy, inputs, screen, None)

    @property
    def rpc(self):
        return self.c

    @sync_wrapper
    def get_sdk_version(self):
        return self.c.call("GetSDKVersion")
