using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>재생 가능한 효과음 목록 — SfxPlayer.m_Clips 배열 인덱스와 1:1 (씬 생성기가 순서대로 배선).</summary>
    public enum SfxId
    {
        PopgunFire = 0,
        SlingshotFire = 1,
        MachinegunFire = 2,
        PounceSwing = 3,
        MouseHurt = 4,
        CatHurt = 5,
        MouseDeath = 6,
        CatDeath = 7,
        FortressHit = 8,
        FortressBreak = 9,
        FortressPlace = 10,
        DecoyPlace = 11,
        MatchStart = 12,
        MouseWin = 13,
        CatWin = 14,
    }

    /// <summary>
    /// 효과음 재생 허브(씬 오브젝트 1개). 월드 소리는 3D(거리 감쇠), UI·알림은 2D.
    /// 원샷은 임시 GameObject로 재생 — 발신자가 despawn돼도 소리는 끝까지 남는다(사망음).
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        private const float WORLD_MIN_DISTANCE = 1.5f; // 이 안에서는 최대 볼륨
        private const float WORLD_MAX_DISTANCE = 20f;  // 이 밖에서는 무음 (Linear 감쇠)

        [SerializeField] private AudioClip[] m_Clips; // SfxId 순서와 1:1

        private static SfxPlayer s_Instance;

        private void Awake()
        {
            s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        /// <summary>월드 위치 3D 원샷 — 리스너(자기 카메라) 거리에 따라 감쇠.</summary>
        public static void PlayAt(SfxId _id, Vector3 _position, float _volume = 1f)
        {
            AudioClip clip = ClipOf(_id);
            if (clip == null) return;

            var go = new GameObject($"Sfx_{_id}");
            go.transform.position = _position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = WORLD_MIN_DISTANCE;
            source.maxDistance = WORLD_MAX_DISTANCE;
            source.Play();
            Destroy(go, clip.length + 0.1f);
        }

        /// <summary>2D 원샷 — UI·매치 알림 등 거리 무관 소리.</summary>
        public static void PlayUi(SfxId _id, float _volume = 1f)
        {
            AudioClip clip = ClipOf(_id);
            if (clip == null) return;

            var go = new GameObject($"Sfx_{_id}");
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = _volume;
            source.spatialBlend = 0f;
            source.Play();
            Destroy(go, clip.length + 0.1f);
        }

        private static AudioClip ClipOf(SfxId _id)
        {
            if (s_Instance == null || s_Instance.m_Clips == null) return null;
            int index = (int)_id;
            if (index < 0 || index >= s_Instance.m_Clips.Length) return null;
            return s_Instance.m_Clips[index];
        }
    }
}
