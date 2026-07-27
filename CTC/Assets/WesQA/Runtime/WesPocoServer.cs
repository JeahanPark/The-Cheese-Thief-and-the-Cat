using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace WesQA
{
    /// <summary>TcpListener + [4B LE len][utf-8] 프레이밍 + JSON-RPC 디스패치.
    /// 핸들러는 메인스레드(펌프)에서 실행. 동기 메서드는 즉시 응답, Screenshot는 end-of-frame 코루틴으로 비동기 응답.</summary>
    public class WesPocoServer
    {
        private const int MaxFrameBytes = 64 * 1024 * 1024;
        private readonly int _port;
        private TcpListener _listener;
        private Thread _accept;
        private volatile bool _running;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        // 수락된 연결 추적 — Stop()에서 강제로 닫아야 포트가 풀린다.
        // (클라가 비정상 종료하면 ClientLoop이 Read에서 블로킹된 채 남아 소켓이 포트를 계속 점유)
        private readonly System.Collections.Generic.List<TcpClient> _clients =
            new System.Collections.Generic.List<TcpClient>();
        private GameObject _pump;
        private WesQAPump _pumpComp;

        public WesPocoServer(int port) { _port = port; }

        /// <summary>실제 바인드된 포트(폴백 포함). 클라이언트가 읽는 공유 파일에 기록된다.</summary>
        public int BoundPort { get; private set; }

        private const int PORT_FALLBACK_ATTEMPTS = 5;
        private const int PORT_FALLBACK_STRIDE = 10; // 인스턴스 간(±1) 충돌 없이 건너뛰기

        public void Start()
        {
            _running = true;

            // 고아 리스너(도메인 리로드로 참조를 잃은 소켓)가 기본 포트를 쥐고 있어도 QA가 막히지 않게
            // 포트를 건너뛰며 바인드하고 실제 포트를 파일로 알린다 — NGO HostPortFallback과 같은 패턴.
            // (Windows는 살아있는 리스닝 소켓을 SO_REUSEADDR로도 가로챌 수 없어 회수는 불가)
            for (int attempt = 0; attempt < PORT_FALLBACK_ATTEMPTS; attempt++)
            {
                int port = _port + attempt * PORT_FALLBACK_STRIDE;
                _listener = new TcpListener(IPAddress.Loopback, port);
                try
                {
                    _listener.Start();
                    BoundPort = port;
                    if (attempt > 0) Debug.LogWarning($"[WesQA] 포트 {_port} 점유 — {port}로 폴백");
                    break;
                }
                catch (SocketException)
                {
                    _listener = null;
                }
            }

            if (_listener == null)
            {
                _running = false;
                Debug.LogError($"[WesQA] 포트 {_port}~{_port + (PORT_FALLBACK_ATTEMPTS - 1) * PORT_FALLBACK_STRIDE} " +
                               "전부 바인드 실패 — Unity 에디터 재시작 필요");
                return;
            }
            WritePortFile();
            _accept = new Thread(AcceptLoop) { IsBackground = true };
            _accept.Start();

            _pump = new GameObject("[WesQA.Pump]");
            UnityEngine.Object.DontDestroyOnLoad(_pump);
            _pumpComp = _pump.AddComponent<WesQAPump>();
            _pumpComp.Bind(this);
        }

        public void Stop()
        {
            _running = false;
            // TcpListener.Stop()만으로는 accept 스레드가 블로킹된 채면 핸들이 남아
            // 포트가 LISTENING으로 고아가 된다(2026-07-27 실측) — 하부 소켓을 직접 닫는다
            try { _listener?.Server?.Close(0); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;

            // 연결된 클라이언트까지 끊어야 로컬 포트가 즉시 해제된다 —
            // 이걸 빠뜨리면 다음 플레이에서 SocketException(주소 사용 중)으로 QA가 막힌다
            lock (_clients)
            {
                foreach (TcpClient client in _clients)
                {
                    try { client.Close(); } catch { }
                }
                _clients.Clear();
            }
        }

        // 파이썬 클라이언트가 읽는 실제 포트 — %TEMP%/ctc-mppm-qa/wesqa_port_<기본포트>.txt
        private void WritePortFile()
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "ctc-mppm-qa");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"wesqa_port_{_port}.txt"), BoundPort.ToString());
            }
            catch (IOException) { }
        }

        internal void PumpMainThread()
        {
            while (_mainThread.TryDequeue(out var act)) act();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    lock (_clients) _clients.Add(client);
                    new Thread(() => ClientLoop(client)) { IsBackground = true }.Start();
                }
                catch { if (_running) Thread.Sleep(50); }
            }
        }

        private void ClientLoop(TcpClient client)
        {
            try { ClientLoopInner(client); }
            finally { lock (_clients) _clients.Remove(client); }
        }

        private void ClientLoopInner(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var streamLock = new object();
                var header = new byte[4];
                while (_running)
                {
                    if (!ReadExactly(stream, header, 4)) break;
                    int len = BitConverter.ToInt32(header, 0);
                    if (len < 0 || len > MaxFrameBytes) break;
                    var body = new byte[len];
                    if (!ReadExactly(stream, body, len)) break;
                    string json = Encoding.UTF8.GetString(body);
                    _mainThread.Enqueue(() => HandleOnMainThread(stream, streamLock, json));
                }
            }
        }

        // 메인스레드에서 실행. 동기 메서드는 즉시 응답. Screenshot는 코루틴 위임(end-of-frame).
        private void HandleOnMainThread(NetworkStream stream, object streamLock, string json)
        {
            RpcRequest req = null;
            try
            {
                req = RpcRequest.Parse(json);
                if (req.Method == "Screenshot" && _pumpComp != null)
                {
                    int w = 0;
                    var a = req.Args();
                    if (a.Count > 0) w = a[0].ToObject<int>();
                    _pumpComp.StartCoroutine(ScreenshotCoroutine.Run(w, req.Id, stream, streamLock));
                    return;
                }
                object result = RpcMethods.Invoke(req);
                Send(stream, streamLock, RpcResponse.Result(req.Id, result));
            }
            catch (Exception e)
            {
                Send(stream, streamLock, RpcResponse.Error(req != null ? req.Id : null, e.Message));
            }
        }

        // 스트림 쓰기는 streamLock으로 직렬화(동기 응답 + 코루틴 응답 경쟁 방지)
        internal static void Send(NetworkStream stream, object streamLock, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var headerBytes = BitConverter.GetBytes(payload.Length);
            lock (streamLock)
            {
                try
                {
                    stream.Write(headerBytes, 0, 4);
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush();
                }
                catch { }
            }
        }

        private static bool ReadExactly(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }
    }

    /// <summary>메인스레드 큐를 매 프레임 비우는 펌프. 코루틴 호스트.</summary>
    public class WesQAPump : MonoBehaviour
    {
        private WesPocoServer _server;
        public void Bind(WesPocoServer s) { _server = s; }
        private void Update() { _server?.PumpMainThread(); }
        private void OnDestroy() { _server?.Stop(); }
    }
}
