using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 개발용 포트 폴백. 에디터가 플레이 종료 시 UTP UDP 소켓을 놓지 않아(누수)
    /// 기본 포트(7788)가 막히는 일이 잦다 — 호스트는 빈 포트를 찾을 때까지 올려가며
    /// 바인드하고, 선택한 포트를 시스템 temp 공유 파일에 기록한다.
    /// 클라이언트(MPPM 클론 포함)는 그 파일을 읽어 같은 포트로 접속한다.
    /// UGS Relay(Plan 2) 도입 시 이 로컬 직결 경로 전체가 대체된다.
    /// </summary>
    public static class HostPortFallback
    {
        public const int BASE_PORT = 7788;
        public const int MAX_ATTEMPTS = 20;
        private const string PORT_DIR_NAME = "ctc-mppm-qa";
        private const string PORT_FILE_NAME = "host_port.txt";

        public static string PortFilePath =>
            Path.Combine(Path.GetTempPath(), PORT_DIR_NAME, PORT_FILE_NAME);

        /// <summary>빈 포트를 찾아 호스트 시작. 성공 시 포트를 공유 파일에 기록.</summary>
        public static bool StartHost(NetworkManager _nm)
        {
            var transport = _nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null) return _nm.StartHost();

            for (int i = 0; i < MAX_ATTEMPTS; i++)
            {
                ushort port = (ushort)(BASE_PORT + i);
                transport.SetConnectionData("127.0.0.1", port);

                if (_nm.StartHost())
                {
                    WriteHostPort(port);
                    if (i > 0)
                        Debug.LogWarning($"[HostPortFallback] 포트 {BASE_PORT}~{port - 1} 점유 — {port}로 호스트");
                    return true;
                }
            }

            Debug.LogError($"[HostPortFallback] {BASE_PORT}~{BASE_PORT + MAX_ATTEMPTS - 1} 전부 바인드 실패");
            return false;
        }

        /// <summary>클라이언트용: 호스트가 기록한 포트를 트랜스포트에 적용한다.</summary>
        public static void ApplyHostPortToTransport(NetworkManager _nm)
        {
            var transport = _nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null) return;
            transport.SetConnectionData("127.0.0.1", (ushort)ReadHostPort());
        }

        public static int ReadHostPort()
        {
            try
            {
                if (File.Exists(PortFilePath) &&
                    int.TryParse(File.ReadAllText(PortFilePath).Trim(), out int port))
                {
                    return port;
                }
            }
            catch (IOException) { }
            return BASE_PORT;
        }

        private static void WriteHostPort(int _port)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PortFilePath));
                File.WriteAllText(PortFilePath, _port.ToString());
            }
            catch (IOException) { }
        }
    }
}
