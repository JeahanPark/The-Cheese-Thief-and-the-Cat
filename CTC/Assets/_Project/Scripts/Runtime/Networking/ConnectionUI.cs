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
            GUILayout.BeginArea(new Rect(10, 10, 200, 200));

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
                if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
                if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
            }
            else
            {
                GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client")}");
                GUILayout.Label($"Players: {NetworkManager.Singleton.ConnectedClientsList.Count}");
                if (GUILayout.Button("Disconnect")) NetworkManager.Singleton.Shutdown();
            }

            GUILayout.EndArea();
        }
    }
}
