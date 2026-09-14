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

                // **먼저 열어 본다.** 그냥 StartHost로 찔러 보면 실패할 때마다 NGO가 빨간 줄을
                // 셋 찍는데, 기본 포트 7788은 MPPM 수집기가 늘 물고 있어서 **판을 열 때마다**
                // 그 에러가 뜬다. 진짜 문제와 구분이 안 되는 노이즈다.
                if (!IsPortFree(port)) continue;

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

        // UDP 소켓을 잠깐 열어 본다. 열리면 비어 있는 포트다.
        // 닫고 다시 여는 사이의 틈은 남지만, 이건 같은 PC 개발 경로 전용이라 그걸로 충분하다.
        private static bool IsPortFree(ushort _port)
        {
            try
            {
                using (var probe = new System.Net.Sockets.UdpClient(
                           new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), _port)))
                {
                    return true;
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
        }

        /// <summary>
        /// 이 PC에 로컬 호스트가 떠 있는 것 같은가 — 빠른 참가의 스팀 없는 대체 경로가 "참가냐 생성이냐"를 고른다.
        /// 포트 파일의 포트가 **사용 중**이면 그렇다고 본다. 누수 소켓이 거짓 양성을 낼 수 있으니
        /// 부르는 쪽이 접속 시간 제한으로 되받는다(개발 전용 경로라 그걸로 충분하다).
        /// </summary>
        public static bool IsLocalHostLikelyUp()
        {
            if (!File.Exists(PortFilePath)) return false;
            return !IsPortFree((ushort)ReadHostPort());
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
