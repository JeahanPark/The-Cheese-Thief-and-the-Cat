using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby
{
    /// <summary>
    /// 개발용 로비 UI(IMGUI, ConnectionUI 패턴). 접속 후~시작 전까지 역할 선택/시작 버튼을 그린다.
    /// 커서: 로비 중 해제, 게임 시작 시 잠금(궤도 카메라가 잠금 상태에서만 회전).
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobby;

        private void Update()
        {
            if (lobby == null || !lobby.IsSpawned) return;

            if (lobby.GameStarted)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnGUI()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || (!nm.IsClient && !nm.IsServer)) return; // 접속 전엔 ConnectionUI 담당
            if (lobby == null || !lobby.IsSpawned || lobby.GameStarted) return;

            GUILayout.BeginArea(new Rect(220, 10, 280, 320), GUI.skin.box);
            GUILayout.Label("로비 — 역할 선택");

            for (int i = 0; i < lobby.PlayerCount; i++)
            {
                LobbyPlayerState p = lobby.GetPlayer(i);
                string self = p.ClientId == nm.LocalClientId ? " (나)" : "";
                GUILayout.Label($"Client {p.ClientId}{self}: {p.Role}");
            }

            GUILayout.Space(8);
            if (GUILayout.Button("쥐로 플레이")) lobby.SelectRole(RoleId.Mouse);
            if (GUILayout.Button("고양이로 플레이")) lobby.SelectRole(RoleId.Cat);

            if (nm.IsHost)
            {
                GUILayout.Space(8);
                GUI.enabled = lobby.CanStart;
                if (GUILayout.Button("게임 시작")) lobby.RequestStartGame();
                GUI.enabled = true;
            }

            GUILayout.EndArea();
        }
    }
}
