using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 이 씬에서 어떤 배경음을 틀지 <see cref="BgmDirector"/>에게 말한다 (2026-09-01).
    ///
    /// 소리를 내는 건 이 컴포넌트가 아니다 — 재생기는 씬을 넘어 하나뿐이고, 같은 곡이면
    /// 그대로 이어진다. 그래서 인트로·로비·대기실이 **한 곡으로 끊김 없이** 이어지고,
    /// 인게임에 들어가면 그때 곡이 바뀐다.
    /// </summary>
    public class SceneBgm : MonoBehaviour
    {
        [SerializeField] private AudioClip m_Clip;

        private void Start()
        {
            if (m_Clip == null) return;

            if (BgmDirector.Instance == null)
            {
                var go = new GameObject(nameof(BgmDirector), typeof(AudioSource));
                var source = go.GetComponent<AudioSource>();
                source.volume = 0.35f;   // 설계 볼륨 — AudioVolumeFollower가 이 비율을 지킨다
                go.AddComponent<BgmDirector>();
                // 붙이면서 카테고리까지 준다 — 붙인 **뒤에** 고치면 AddComponent가 이미 Awake를
                // 돌린 뒤라 예전엔 배경음이 효과음 슬라이더를 따라갔다(2026-09-08).
                AudioVolumeFollower.Attach(go, AudioVolumeFollower.Category.Bgm);
            }

            BgmDirector.Instance.Play(m_Clip);
        }
    }
}
