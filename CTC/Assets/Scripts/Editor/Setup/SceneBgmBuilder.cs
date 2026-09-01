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
        private const float DESIGN_VOLUME = 0.35f;

        /// <summary>씬에 배경음 오브젝트를 세운다. 클립이 없으면 아무것도 안 한다(무음 유지).</summary>
        public static void Attach(string _clipPath, string _name = "SceneBgm")
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[SceneBgm] 클립 없음(무음): {_clipPath}");
                return;
            }

            var go = new GameObject(_name);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;   // SceneBgm이 볼륨을 보고 켠다
            source.volume = DESIGN_VOLUME;
            source.spatialBlend = 0f;     // 2D — 거리 감쇠 없음

            go.AddComponent<CTC.Runtime.Audio.SceneBgm>();

            // 옵션 창의 배경음 볼륨을 따라간다 — 0이면 SceneBgm이 정지시킨다.
            var follower = go.AddComponent<CTC.Runtime.Audio.AudioVolumeFollower>();
            var so = new SerializedObject(follower);
            so.FindProperty("m_Category").enumValueIndex =
                (int)CTC.Runtime.Audio.AudioVolumeFollower.Category.Bgm;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
