using R3;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 플레이어 개인 설정 — 마우스 감도·배경음·효과음 (옵션 팝업이 쓰는 Model, 2026-08-01).
    ///
    /// **밸런스가 아니라 취향이라 CSV가 아니라 `PlayerPrefs`에 남는다.** CSV(BalanceInfo)는
    /// *처음 켰을 때의 기본값*만 준다 — 한 번이라도 손대면 그 사람 기기의 값이 이긴다.
    ///
    /// R3 `ReactiveProperty`라 View(슬라이더)와 적용부(카메라·오디오)가 서로를 모른 채
    /// 같은 값을 본다. 값이 바뀌는 즉시 저장한다 — 옵션 창에 "확인" 버튼을 두지 않으려고.
    /// (ReactiveProperty는 순수 C#이라 규칙 9의 `?.` 금지 대상이 아니다)
    /// </summary>
    public static class UserSettings
    {
        private const string KEY_SENSITIVITY = "ctc.sensitivity";
        private const string KEY_BGM = "ctc.bgm";
        private const string KEY_SFX = "ctc.sfx";

        public const float MIN_SENSITIVITY = 0.02f;
        public const float MAX_SENSITIVITY = 0.60f;

        private static ReactiveProperty<float> s_Sensitivity;
        private static ReactiveProperty<float> s_Bgm;
        private static ReactiveProperty<float> s_Sfx;

        /// <summary>마우스 감도(도/픽셀). 카메라가 구독한다.</summary>
        public static ReactiveProperty<float> MouseSensitivity
        {
            get { EnsureLoaded(); return s_Sensitivity; }
        }

        /// <summary>배경음 볼륨 0~1.</summary>
        public static ReactiveProperty<float> BgmVolume
        {
            get { EnsureLoaded(); return s_Bgm; }
        }

        /// <summary>효과음 볼륨 0~1. `SfxPlayer`와 `SfxVolumeScaler`가 곱한다.</summary>
        public static ReactiveProperty<float> SfxVolume
        {
            get { EnsureLoaded(); return s_Sfx; }
        }

        /// <summary>CSV 기본값으로 되돌린다(옵션 창의 "기본값" 버튼).</summary>
        public static void ResetToDefaults()
        {
            Info.BalanceInfo balance = Info.InfoManager.Instance.Balance;
            EnsureLoaded();
            MouseSensitivity.Value = balance.MouseSensitivityDefault;
            BgmVolume.Value = balance.BgmVolumeDefault;
            SfxVolume.Value = balance.SfxVolumeDefault;
        }

        private static void EnsureLoaded()
        {
            if (s_Sensitivity != null) return;

            Info.BalanceInfo balance = Info.InfoManager.Instance.Balance;
            s_Sensitivity = Bind(KEY_SENSITIVITY, balance.MouseSensitivityDefault, MIN_SENSITIVITY, MAX_SENSITIVITY);
            s_Bgm = Bind(KEY_BGM, balance.BgmVolumeDefault, 0f, 1f);
            s_Sfx = Bind(KEY_SFX, balance.SfxVolumeDefault, 0f, 1f);
        }

        // 저장된 값이 있으면 그걸, 없으면 CSV 기본값을. 이후 변경은 즉시 디스크로.
        private static ReactiveProperty<float> Bind(string _key, float _default, float _min, float _max)
        {
            float stored = Mathf.Clamp(PlayerPrefs.GetFloat(_key, _default), _min, _max);
            var prop = new ReactiveProperty<float>(stored);
            prop.Subscribe(v =>
            {
                PlayerPrefs.SetFloat(_key, Mathf.Clamp(v, _min, _max));
                PlayerPrefs.Save();
            });
            return prop;
        }
    }
}
