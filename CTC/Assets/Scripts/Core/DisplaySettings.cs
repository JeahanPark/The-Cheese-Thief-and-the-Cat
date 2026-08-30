using System.Collections.Generic;
using R3;
using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 해상도·창모드 (옵션 팝업이 쓰는 Model, 2026-08-30).
    ///
    /// <see cref="UserSettings"/>와 **같은 두 층 구조**다 — 스트림(지금 화면)과 `PlayerPrefs`
    /// (저장된 값). 드롭다운을 바꾸면 화면이 즉시 바뀌고(미리보기), 디스크는 "확인"에서만 바뀐다.
    /// "취소"·ESC는 저장된 값을 다시 흘려 되돌린다.
    ///
    /// **적용을 여기서 직접 한다.** 볼륨은 `AudioVolumeFollower`가 씬마다 붙어 구독하지만,
    /// `Screen`은 씬이 아니라 프로세스에 하나뿐인 전역이다. 그걸 씬 컴포넌트에 맡기면
    /// 컴포넌트가 없는 씬에서 설정이 조용히 안 먹는다.
    /// </summary>
    public static class DisplaySettings
    {
        private const string KEY_WIDTH = "ctc.screen.w";
        private const string KEY_HEIGHT = "ctc.screen.h";
        private const string KEY_MODE = "ctc.screen.mode";

        private static ReactiveProperty<int> s_ResolutionIndex;
        private static ReactiveProperty<int> s_Mode;
        private static List<Vector2Int> s_Options;

        /// <summary>고를 수 있는 해상도들. 큰 것이 앞이다.</summary>
        public static IReadOnlyList<Vector2Int> Options
        {
            get { EnsureLoaded(); return s_Options; }
        }

        /// <summary><see cref="Options"/>의 몇 번째인가.</summary>
        public static ReactiveProperty<int> ResolutionIndex
        {
            get { EnsureLoaded(); return s_ResolutionIndex; }
        }

        /// <summary><see cref="WindowMode"/>를 int로 나른다 — 드롭다운이 int를 다루기 때문이다.</summary>
        public static ReactiveProperty<int> Mode
        {
            get { EnsureLoaded(); return s_Mode; }
        }

        /// <summary>
        /// 저장된 화면 설정을 켜자마자 적용한다. 씬보다 먼저 돌아야 첫 프레임부터 맞는 크기로 뜬다.
        /// 저장한 적이 없으면 **아무것도 안 바꾼다** — 빌드 설정 그대로 둔다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStored()
        {
            // 도메인 리로드를 끄고 플레이하면 옛 스트림이 남는다 — 매 진입마다 새로 읽는다.
            s_ResolutionIndex = null;
            s_Mode = null;
            s_Options = null;

            if (!PlayerPrefs.HasKey(KEY_WIDTH)) return;

            EnsureLoaded();
            ApplyNow();
        }

        /// <summary>지금 화면 설정을 디스크에 새긴다(옵션 창의 "확인").</summary>
        public static void Save()
        {
            EnsureLoaded();

            Vector2Int picked = Picked();
            PlayerPrefs.SetInt(KEY_WIDTH, picked.x);
            PlayerPrefs.SetInt(KEY_HEIGHT, picked.y);
            PlayerPrefs.SetInt(KEY_MODE, s_Mode.Value);
            PlayerPrefs.Save();
        }

        /// <summary>저장된 값으로 되돌린다(옵션 창의 "취소"·ESC). 저장한 적이 없으면 실행 시점의 값으로.</summary>
        public static void Revert()
        {
            EnsureLoaded();

            int width = PlayerPrefs.GetInt(KEY_WIDTH, Screen.width);
            int height = PlayerPrefs.GetInt(KEY_HEIGHT, Screen.height);
            int mode = PlayerPrefs.GetInt(KEY_MODE, (int)DisplayLogic.FromUnity(Screen.fullScreenMode));

            s_ResolutionIndex.Value = DisplayLogic.NearestIndex(s_Options, width, height);
            s_Mode.Value = mode;
            ApplyNow();
        }

        /// <summary>지금 고른 해상도.</summary>
        public static Vector2Int Picked()
        {
            EnsureLoaded();

            int index = DisplayLogic.ClampIndex(s_ResolutionIndex.Value, s_Options.Count);
            return s_Options.Count > 0 ? s_Options[index] : new Vector2Int(Screen.width, Screen.height);
        }

        private static void EnsureLoaded()
        {
            if (s_ResolutionIndex != null) return;

            s_Options = DisplayLogic.Options(AvailableResolutions());

            int width = PlayerPrefs.GetInt(KEY_WIDTH, Screen.width);
            int height = PlayerPrefs.GetInt(KEY_HEIGHT, Screen.height);
            int mode = PlayerPrefs.GetInt(KEY_MODE, (int)DisplayLogic.FromUnity(Screen.fullScreenMode));

            s_ResolutionIndex = new ReactiveProperty<int>(DisplayLogic.NearestIndex(s_Options, width, height));
            s_Mode = new ReactiveProperty<int>(mode);

            // 값이 바뀌면 곧바로 화면에 반영한다 — 보고 골라야 하는 설정이다.
            s_ResolutionIndex.Subscribe(_ => ApplyNow());
            s_Mode.Subscribe(_ => ApplyNow());
        }

        private static void ApplyNow()
        {
            Vector2Int picked = Picked();
            FullScreenMode mode = DisplayLogic.ToUnity((WindowMode)s_Mode.Value);

            if (Screen.width == picked.x && Screen.height == picked.y && Screen.fullScreenMode == mode) return;

            Screen.SetResolution(picked.x, picked.y, mode);
        }

        /// <summary>
        /// 에디터의 `Screen.resolutions`는 **비어 있다**(게임 뷰는 창이라 모니터 모드 목록이 없다).
        /// 그러면 드롭다운이 빈 채로 떠서 배치를 확인할 수 없으므로, 흔한 값들을 대신 넣는다.
        /// </summary>
        private static List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();

            Resolution[] found = Screen.resolutions;
            for (int i = 0; i < found.Length; i++)
            {
                list.Add(new Vector2Int(found[i].width, found[i].height));
            }

            if (list.Count > 0) return list;

            list.Add(new Vector2Int(3840, 2160));
            list.Add(new Vector2Int(2560, 1440));
            list.Add(new Vector2Int(1920, 1080));
            list.Add(new Vector2Int(1600, 900));
            list.Add(new Vector2Int(1366, 768));
            list.Add(new Vector2Int(1280, 720));
            return list;
        }
    }
}
