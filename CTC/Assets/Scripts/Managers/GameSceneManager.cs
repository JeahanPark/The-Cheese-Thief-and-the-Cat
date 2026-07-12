// WES에서 이식(2026-07-12) — 씬 상수를 CTC 실제 씬으로 교체: 현재 CTC는 Game 씬 1개뿐이라
// SCENE_GAME만 활성, Intro/Lobby 상수는 씬 추가 시 해제하도록 주석 처리. 로직 변경 없음.
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using CTC.Runtime.Core;

namespace CTC.Runtime.Managers
{
    public class GameSceneManager : MonoSingleton<GameSceneManager>
    {
        // Scene Names — CTC 현재 씬 구성(Assets/Scenes/Game.unity 1개)
        public const string SCENE_GAME = "Game";
        // public const string SCENE_INTRO = "Intro";   // TODO: Intro 씬 추가 시 해제
        // public const string SCENE_LOBBY = "Lobby";   // TODO: Lobby 씬 추가 시 해제

        private bool m_IsLoadingScene = false;

        public bool IsLoadingScene => m_IsLoadingScene;

        public void LoadScene(string _sceneName)
        {
            if (m_IsLoadingScene)
            {
                GameDebug.LogWarning($"[GameSceneManager] Already loading a scene. Cannot load {_sceneName}");
                return;
            }

            // 호스트/서버 상태에서는 Netcode SceneManager로 로드해야 in-scene NetworkObject가 정상 스폰됨
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(_sceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(_sceneName);
            }
        }

        public void LoadSceneAsync(string _sceneName)
        {
            if (m_IsLoadingScene)
            {
                GameDebug.LogWarning($"[GameSceneManager] Already loading a scene. Cannot load {_sceneName}");
                return;
            }

            StartCoroutine(CoLoadSceneAsync(_sceneName));
        }

        private IEnumerator CoLoadSceneAsync(string _sceneName)
        {
            m_IsLoadingScene = true;

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(_sceneName);
            asyncLoad.allowSceneActivation = false;

            // 로딩 진행률 체크
            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            // 씬 활성화
            asyncLoad.allowSceneActivation = true;

            // 씬 로드 완료 대기
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            m_IsLoadingScene = false;
        }
    }
}
