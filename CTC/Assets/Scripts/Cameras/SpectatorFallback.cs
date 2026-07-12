using CTC.Runtime.Lobby;
using CTC.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 관전 폴백: 게임 중 로컬 플레이어가 사라지면(탈락) 카메라를
    /// 아무 생존 플레이어에게 재바인딩한다. (MECCHA식 자유 관전의 MVP)
    /// </summary>
    [RequireComponent(typeof(OrbitCameraController))]
    public class SpectatorFallback : MonoBehaviour
    {
        private const float SPECTATE_FOV = 60f;
        private const float SPECTATE_DISTANCE = 5f;

        [SerializeField] private LobbyManager m_Lobby;

        private OrbitCameraController m_Orbit;
        private Transform m_Current;

        private void Awake()
        {
            m_Orbit = GetComponent<OrbitCameraController>();
        }

        private void Update()
        {
            if (m_Lobby == null || !m_Lobby.IsSpawned || !m_Lobby.GameStarted) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;
            if (nm.SpawnManager.GetLocalPlayerObject() != null) return; // 아직 살아 있음

            if (m_Current != null) return; // 이미 관전 대상 추적 중

            PlayerMovement any = FindFirstObjectByType<PlayerMovement>();
            if (any == null) return;

            m_Current = any.transform;
            m_Orbit.Bind(m_Current, SPECTATE_FOV, SPECTATE_DISTANCE);
            Debug.Log($"[SpectatorFallback] 관전 전환 → {any.name}");
        }
    }
}
