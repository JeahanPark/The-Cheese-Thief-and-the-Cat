// WES에서 이식(2026-07-12) — AudioChannel enum을 WES GameEnum.cs에서 이 파일로 함께 이식.
// WES 기획 참조 주석(R4/R5 §) 정리, 미구현이던 BGM_CROSSFADE_DURATION 상수 제거. 로직 변경 없음.
// Addressables 2.11.1 API 확인 완료: LoadResourceLocationsAsync(key, type)/LoadAssetAsync/Release 동일.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using CTC.Runtime.Core;

namespace CTC.Runtime.Managers
{
    /// <summary>
    /// 사운드 채널. 믹스 우선순위: Stinger > Sfx > Ambient > Bgm.
    /// </summary>
    public enum AudioChannel
    {
        Bgm,      // 배경음(루프 1소스)
        Ambient,  // 환경음(루프 1소스)
        Sfx,      // UI·상호작용 원샷(라운드로빈 풀)
        Stinger,  // 사건 강조 원샷(최우선)
    }

    /// <summary>
    /// 4채널(BGM/Ambient/SFX/Stinger) 사운드 매니저.
    ///
    /// 음원이 0개여도 동작한다(null 가드) — 키에 해당하는 AudioClip이 Addressable에 없으면
    /// 조용히 무음 통과하고 실패 키를 캐시해 재시도/스팸을 막는다.
    /// 음원이 채워지면 Addressable에 같은 키로 등록만 하면 곧장 소리가 난다.
    ///
    /// 모든 재생은 각 클라이언트 로컬(동기화 무관). 동기화된 상태를 각 클라가 읽어 로컬 발화한다.
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private const int SFX_POOL_SIZE = 8;            // 동시 SFX 라운드로빈 풀

        // 채널별 기본 볼륨(우선순위 가중 — Stinger 최상).
        private const float DEFAULT_VOLUME_BGM = 0.5f;
        private const float DEFAULT_VOLUME_AMBIENT = 0.6f;
        private const float DEFAULT_VOLUME_SFX = 0.85f;
        private const float DEFAULT_VOLUME_STINGER = 1.0f;

        private AudioSource m_BgmSource;
        private AudioSource m_AmbientSource;
        private AudioSource m_StingerSource;
        private AudioSource[] m_SfxPool;
        private int m_SfxCursor;

        private float m_MasterVolume = 1.0f;
        private readonly Dictionary<AudioChannel, float> m_ChannelVolume = new();

        // 로드 캐시. 키→클립. 실패 키는 null로 저장해 재로드/스팸 차단.
        private readonly Dictionary<string, AudioClip> m_ClipCache = new();
        private readonly HashSet<string> m_WarnedKeys = new();

        private string m_CurrentBgmKey;

        public override void Init()
        {
            base.Init();
            EnsureSources();
            InitChannelVolume();
        }

        public override void Clear()
        {
            base.Clear();
            m_ClipCache.Clear();
            m_WarnedKeys.Clear();
            m_CurrentBgmKey = null;
        }

        // ── public 재생 계약 (호출부 1줄) ─────────────────────────────

        public void PlayBgm(string _key, bool _loop = true)
        {
            EnsureSources();
            if (string.IsNullOrEmpty(_key))
                return;
            if (_key == m_CurrentBgmKey)
                return; // 같은 BGM 재요청 무시(루프 유지)

            m_CurrentBgmKey = _key;
            AudioClip clip = GetClip(_key);
            if (clip == null)
                return; // 음원 미등록 — 무음(키만 기억, 나중 등록 시 다음 호출에 반영)

            m_BgmSource.clip = clip;
            m_BgmSource.loop = _loop;
            m_BgmSource.volume = ChannelVolume(AudioChannel.Bgm);
            m_BgmSource.Play();
        }

        public void StopBgm()
        {
            if (m_BgmSource != null)
                m_BgmSource.Stop();
            m_CurrentBgmKey = null;
        }

        public void PlayAmbient(string _key)
        {
            EnsureSources();
            AudioClip clip = GetClip(_key);
            if (clip == null)
            {
                // 환경음 없음 — 기존 앰비언트 정지(전환 시 잔류 방지)
                m_AmbientSource.Stop();
                return;
            }
            if (m_AmbientSource.clip == clip && m_AmbientSource.isPlaying)
                return;

            m_AmbientSource.clip = clip;
            m_AmbientSource.loop = true;
            m_AmbientSource.volume = ChannelVolume(AudioChannel.Ambient);
            m_AmbientSource.Play();
        }

        public void StopAmbient()
        {
            if (m_AmbientSource != null)
                m_AmbientSource.Stop();
        }

        public void PlaySfx(string _key)
        {
            EnsureSources();
            AudioClip clip = GetClip(_key);
            if (clip == null)
                return;

            AudioSource src = m_SfxPool[m_SfxCursor];
            m_SfxCursor = (m_SfxCursor + 1) % m_SfxPool.Length;
            src.PlayOneShot(clip, ChannelVolume(AudioChannel.Sfx));
        }

        public void PlayStinger(string _key)
        {
            EnsureSources();
            AudioClip clip = GetClip(_key);
            if (clip == null)
                return;

            m_StingerSource.PlayOneShot(clip, ChannelVolume(AudioChannel.Stinger));
        }

        public void SetMasterVolume(float _volume01)
        {
            m_MasterVolume = Mathf.Clamp01(_volume01);
            ApplyLiveVolumes();
        }

        public void SetChannelVolume(AudioChannel _channel, float _volume01)
        {
            m_ChannelVolume[_channel] = Mathf.Clamp01(_volume01);
            ApplyLiveVolumes();
        }

        // ── private ─────────────────────────────────────────────────

        private void EnsureSources()
        {
            if (m_BgmSource != null)
                return;

            m_BgmSource = CreateSource("BgmSource", _loop: true);
            m_AmbientSource = CreateSource("AmbientSource", _loop: true);
            m_StingerSource = CreateSource("StingerSource", _loop: false);

            m_SfxPool = new AudioSource[SFX_POOL_SIZE];
            for (int i = 0; i < SFX_POOL_SIZE; i++)
                m_SfxPool[i] = CreateSource($"SfxSource_{i}", _loop: false);
        }

        private AudioSource CreateSource(string _name, bool _loop)
        {
            GameObject go = new GameObject(_name);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = _loop;
            return src;
        }

        private void InitChannelVolume()
        {
            if (m_ChannelVolume.Count > 0)
                return;
            m_ChannelVolume[AudioChannel.Bgm] = DEFAULT_VOLUME_BGM;
            m_ChannelVolume[AudioChannel.Ambient] = DEFAULT_VOLUME_AMBIENT;
            m_ChannelVolume[AudioChannel.Sfx] = DEFAULT_VOLUME_SFX;
            m_ChannelVolume[AudioChannel.Stinger] = DEFAULT_VOLUME_STINGER;
        }

        private float ChannelVolume(AudioChannel _channel)
        {
            float ch = m_ChannelVolume.TryGetValue(_channel, out float v) ? v : 1f;
            return ch * m_MasterVolume;
        }

        private void ApplyLiveVolumes()
        {
            if (m_BgmSource != null)
                m_BgmSource.volume = ChannelVolume(AudioChannel.Bgm);
            if (m_AmbientSource != null)
                m_AmbientSource.volume = ChannelVolume(AudioChannel.Ambient);
            // SFX/Stinger는 PlayOneShot 시점 볼륨 적용(라이브 변경은 다음 재생부터).
        }

        // Addressable에서 AudioClip 로드. 실패는 조용히 null 캐시(음원 0개가 정상 상태).
        private AudioClip GetClip(string _key)
        {
            if (string.IsNullOrEmpty(_key))
                return null;

            if (m_ClipCache.TryGetValue(_key, out AudioClip cached))
                return cached; // null도 캐시(재시도 차단)

            AudioClip clip = TryLoadClip(_key);
            m_ClipCache[_key] = clip;

            if (clip == null && m_WarnedKeys.Add(_key))
                GameDebug.Log($"[AudioManager] 음원 미등록(무음 통과): {_key}");

            return clip;
        }

        private AudioClip TryLoadClip(string _key)
        {
            try
            {
                // 키 미등록 시 LoadAssetAsync가 InvalidKeyException을 콘솔에 로깅하므로,
                // 먼저 location 존재 여부만 확인해 예외/로그 없이 조용히 무음 통과한다(음원 0개=정상).
                AsyncOperationHandle<IList<IResourceLocation>> locHandle =
                    Addressables.LoadResourceLocationsAsync(_key, typeof(AudioClip));
                locHandle.WaitForCompletion();
                bool exists = locHandle.Status == AsyncOperationStatus.Succeeded
                           && locHandle.Result != null && locHandle.Result.Count > 0;
                Addressables.Release(locHandle);
                if (!exists)
                    return null;

                AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(_key);
                handle.WaitForCompletion();
                if (handle.Status == AsyncOperationStatus.Succeeded)
                    return handle.Result;
                return null;
            }
            catch (Exception)
            {
                // 키 미등록/로드 실패 — 음원 0개 상태의 정상 경로. 조용히 무음.
                return null;
            }
        }
    }
}
