using CTC.Runtime.Core;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Lobby.Browser
{
    /// <summary>
    /// 로비에서 대기실로 넘어가는 문(Worker, 규칙 6) — **씬을 NGO로 넘긴다** (2026-08-30).
    ///
    /// 처음엔 로비가 `SceneManager.LoadScene("Room")`을 직접 불렀다. 화면은 떴는데 **참가자가
    /// 0명이었다** — 유니티 쪽 씬 로드는 NGO가 모르는 일이라, 대기실 씬에 박혀 있는
    /// in-scene NetworkObject(`RoomDirector`)가 스폰되지 않는다. 상태가 통째로 안 선다.
    ///
    /// 그래서 넘기는 주체를 바꾼다:
    /// - **방장**: 서버가 서면 `NetworkManager.SceneManager`로 대기실을 연다.
    /// - **참여자**: 아무것도 안 한다 — 붙는 순간 NGO가 방장이 열어 둔 씬으로 데려간다.
    /// </summary>
    public class RoomEntry : MonoBehaviour
    {
        [SerializeField] private string m_RoomSceneName = "Room";

        private NetworkManager m_Net;

        // NetworkRoot가 Awake에서 NetworkManager를 세운다 — 그 뒤인 Start에서 잡는다.
        private void Start()
        {
            m_Net = NetworkManager.Singleton;
            if (m_Net == null)
            {
                GameDebug.LogError("[RoomEntry] NetworkManager가 없다 — 씬에 NetworkRoot를 넣어라");
                return;
            }

            m_Net.OnServerStarted += OnServerStarted;
        }

        private void OnDestroy()
        {
            if (m_Net != null) m_Net.OnServerStarted -= OnServerStarted;
        }

        private void OnServerStarted()
        {
            if (m_Net == null || !m_Net.IsServer) return;

            var status = m_Net.SceneManager.LoadScene(
                m_RoomSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
                GameDebug.LogError($"[RoomEntry] 대기실 전환 실패: {status}");
        }
    }
}
