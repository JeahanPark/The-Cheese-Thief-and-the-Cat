using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 플레이 종료 시 UTP UDP 소켓 누수 방지. NGO의 정상 teardown이 예외로 끊기면
    /// 트랜스포트가 소켓을 안 놓아 폴백 포트(7788~)가 하나씩 영구 점유된다(에디터 재시작 전까지).
    /// ExitingPlayMode 시점(오브젝트 파괴 전)에 Shutdown을 명시 호출해 소켓을 먼저 정리한다.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeNetworkShutdown
    {
        static PlayModeNetworkShutdown()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state != PlayModeStateChange.ExitingPlayMode) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            nm.Shutdown(true);
            Debug.Log("[PlayModeNetworkShutdown] ExitingPlayMode — NetworkManager.Shutdown()으로 소켓 정리");
        }
    }
}
