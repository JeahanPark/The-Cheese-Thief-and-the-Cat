# coding=utf-8
"""프레이밍·JSON-RPC를 원본 Poco 프로토콜 그대로 말하는 테스트용 서버.
C# WesPocoServer가 구현해야 할 계약의 실행 가능한 참조본이기도 하다."""
import json
import socket
import struct
import threading

HEADER = 4


class FakeWesPocoServer(object):
    def __init__(self, handlers, host="127.0.0.1", port=0):
        self.handlers = handlers
        self._stop = False
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind((host, port))
        self.sock.listen(1)
        self.host, self.port = self.sock.getsockname()
        self.thread = threading.Thread(target=self._serve, daemon=True)

    def start(self):
        self.thread.start()
        return self

    def _serve(self):
        try:
            conn, _ = self.sock.accept()
        except OSError:
            return
        buf = b""
        with conn:
            while not self._stop:
                try:
                    data = conn.recv(65536)
                except OSError:
                    break
                if not data:
                    break
                buf += data
                while len(buf) > HEADER:
                    (length,) = struct.unpack("i", buf[:HEADER])
                    if len(buf) < length + HEADER:
                        break
                    content = buf[HEADER:HEADER + length]
                    buf = buf[HEADER + length:]
                    self._handle(conn, content)

    def _handle(self, conn, content):
        req = json.loads(content.decode("utf-8"))
        params = req.get("params", [])
        args = params if isinstance(params, list) else []
        kwargs = params if isinstance(params, dict) else {}
        result = self.handlers[req["method"]](*args, **kwargs)
        resp = json.dumps({"jsonrpc": "2.0", "result": result, "id": req["id"]})
        payload = resp.encode("utf-8")
        conn.sendall(struct.pack("i", len(payload)) + payload)

    def stop(self):
        self._stop = True
        try:
            self.sock.close()
        except OSError:
            pass
