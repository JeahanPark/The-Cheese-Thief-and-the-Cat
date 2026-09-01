using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>
    /// 인게임 밖 씬(인트로·로비·대기실)에 배경음을 깐다 (2026-09-01).
    ///
    /// 인게임 BGM은 `LobbyController`의 매치 상태를 보는 `BgmPlayer`가 맡는데, 그 컴포넌트는
    /// `Game.unity`에만 있다. 그래서 앞단 세 화면이 통째로 무음이었다(빌드 실측).
    /// 여기서 세우는 것은 네트워크를 모르는 <see cref="CTC.Runtime.Audio.SceneBgm"/>이다.
    ///
    /// 볼륨 비율(0.35)은 인게임 BGM과 같게 둔다 — 화면이 바뀔 때 소리 크기가 튀면 안 된다.
    /// </summary>
    internal static class SceneBgmBuilder
    {
        public const string LOBBY_BGM = "Assets/Audio/BGM/bgm_lobby.ogg";
        public const string INGAME_BGM = "Assets/Audio/BGM/bgm_ingame.ogg";

        /// <summary>씬에 "이 곡을 틀어라" 요청만 놓는다. 재생기는 씬을 넘어 하나뿐이다.</summary>
        public static void Attach(string _clipPath, string _name = "SceneBgm")
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[SceneBgm] 클립 없음(무음): {_clipPath}");
                return;
            }

            var go = new GameObject(_name);
            var scene = go.AddComponent<CTC.Runtime.Audio.SceneBgm>();
            var so = new SerializedObject(scene);
            so.FindProperty("m_Clip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
