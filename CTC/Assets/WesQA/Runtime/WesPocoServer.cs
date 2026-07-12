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
        private GameObject _pump;
        private WesQAPump _pumpComp;

        public WesPocoServer(int port) { _port = port; }

        public void Start()
        {
            _running = true;
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
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
            try { _listener?.Stop(); } catch { }
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
                    new Thread(() => ClientLoop(client)) { IsBackground = true }.Start();
                }
                catch { if (_running) Thread.Sleep(50); }
            }
        }

        private void ClientLoop(TcpClient client)
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
