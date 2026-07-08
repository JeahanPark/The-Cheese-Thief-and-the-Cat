using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Networking
{
    /// <summary>
    /// 개발용 접속 UI. 접속 전에는 Host/Client/Server 버튼을 IMGUI로 그린다.
    /// 로컬호스트(127.0.0.1) 기준. UGS Relay+Lobby는 Plan 2에서 대체한다.
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        private void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 200));

            if (!nm.IsClient && !nm.IsServer)
            {
                if (GUILayout.Button("Host")) nm.StartHost();
                if (GUILayout.Button("Client")) nm.StartClient();
                if (GUILayout.Button("Server")) nm.StartServer();
            }
            else
            {
                GUILayout.Label($"Mode: {(nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client")}");
                // ConnectedClientsList는 서버 전용 — 클라이언트에서 접근하면 NotServerException
                if (nm.IsServer)
                {
                    GUILayout.Label($"Players: {nm.ConnectedClientsList.Count}");
                }
                if (GUILayout.Button("Disconnect")) nm.Shutdown();
            }

            GUILayout.EndArea();
        }
    }
}
