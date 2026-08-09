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
    /// 같은 값을 본다. (ReactiveProperty는 순수 C#이라 규칙 9의 `?.` 금지 대상이 아니다)
    ///
    /// **미리듣기 / 확인 / 취소** (2026-08-09 개정). 값은 두 층이다 —
    /// 스트림(지금 들리는 값)과 `PlayerPrefs`(저장된 값). 슬라이더를 끌면 스트림만 바뀌어
    /// 소리·감도가 즉시 따라오고, 디스크는 <see cref="Save"/>를 부를 때만 바뀐다.
    /// <see cref="Revert"/>는 저장된 값을 다시 흘려 미리듣기를 되돌린다.
    /// 그래서 baseline을 따로 안 들고 있어도 된다 — **저장된 값이 곧 baseline**이다.
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

        /// <summary>
        /// CSV 기본값으로 되돌린다(옵션 창의 "기본값" 버튼).
        /// 다른 슬라이더 조작과 같은 급 — **미리듣기일 뿐이라 저장되지 않는다.**
        /// </summary>
        public static void ResetToDefaults()
        {
            Info.BalanceInfo balance = Info.InfoManager.Instance.Balance;
            EnsureLoaded();
            MouseSensitivity.Value = balance.MouseSensitivityDefault;
            BgmVolume.Value = balance.BgmVolumeDefault;
            SfxVolume.Value = balance.SfxVolumeDefault;
        }

        /// <summary>지금 들리는 값을 디스크에 새긴다(옵션 창의 "확인").</summary>
        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetFloat(KEY_SENSITIVITY, s_Sensitivity.Value);
            PlayerPrefs.SetFloat(KEY_BGM, s_Bgm.Value);
            PlayerPrefs.SetFloat(KEY_SFX, s_Sfx.Value);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 값으로 미리듣기를 되돌린다(옵션 창의 "취소"·ESC).
        /// 저장한 적이 없으면 CSV 기본값으로 — `EnsureLoaded`와 같은 규칙이다.
        /// </summary>
        public static void Revert()
        {
            Info.BalanceInfo balance = Info.InfoManager.Instance.Balance;
            EnsureLoaded();
            s_Sensitivity.Value = Stored(KEY_SENSITIVITY, balance.MouseSensitivityDefault,
                MIN_SENSITIVITY, MAX_SENSITIVITY);
            s_Bgm.Value = Stored(KEY_BGM, balance.BgmVolumeDefault, 0f, 1f);
            s_Sfx.Value = Stored(KEY_SFX, balance.SfxVolumeDefault, 0f, 1f);
        }

        private static void EnsureLoaded()
        {
            if (s_Sensitivity != null) return;

            Info.BalanceInfo balance = Info.InfoManager.Instance.Balance;
            s_Sensitivity = new ReactiveProperty<float>(
                Stored(KEY_SENSITIVITY, balance.MouseSensitivityDefault, MIN_SENSITIVITY, MAX_SENSITIVITY));
            s_Bgm = new ReactiveProperty<float>(Stored(KEY_BGM, balance.BgmVolumeDefault, 0f, 1f));
            s_Sfx = new ReactiveProperty<float>(Stored(KEY_SFX, balance.SfxVolumeDefault, 0f, 1f));
        }

        // 저장된 값이 있으면 그걸, 없으면 CSV 기본값을.
        private static float Stored(string _key, float _default, float _min, float _max)
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(_key, _default), _min, _max);
        }
    }
}
