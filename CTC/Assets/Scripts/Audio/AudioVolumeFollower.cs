using System;
using CTC.Runtime.Core;
using R3;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 붙어 있는 `AudioSource`가 옵션 창의 볼륨을 따라가게 한다 (2026-08-01).
    ///
    /// `SfxPlayer`는 원샷을 코드로 만들면서 볼륨을 곱하면 되지만, **루프 소스**(발소리·미끼
    /// 유인음·앰비언스·BGM)는 생성기가 프리팹·씬에 볼륨을 구워 넣어 두고 아무도 안 만진다.
    /// 그대로 두면 "효과음 볼륨을 줄였는데 제일 큰 소리(발소리)만 안 줄어드는" 결과가 난다.
    ///
    /// Awake에서 **구워진 볼륨을 설계값으로 기억**하고, 이후 `설계값 × 사용자 볼륨`을 쓴다.
    /// 그래서 밸런스로 정한 소리 크기 비율(쥐 0.6 / 미끼 0.6 / BGM 0.35…)은 그대로 유지된다.
    ///
    /// **카테고리를 나중에 줘도 된다** (2026-09-08 수정). 예전에는 `Awake`가 한 번만 구독해서,
    /// 코드로 세우는 재생기(`SceneBgm` → `BgmDirector`)처럼 `AddComponent` **뒤에**
    /// `SetCategory(Bgm)`를 부르는 경로가 통째로 늦었다 — `AddComponent`가 그 자리에서 `Awake`를
    /// 돌려 이미 `Sfx`를 구독한 뒤였다. 배경음이 효과음 슬라이더를 따라가는데도 **에러가 없어서**
    /// 「설계 0.35 × sfx 0.7 = 0.245」가 몇 달을 조용히 서 있었다(wesqa qa_v37 실측).
    /// 지금은 <see cref="SetCategory"/>가 구독을 갈아 끼우고, <see cref="Attach"/>가 순서를 아예 없앤다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVolumeFollower : MonoBehaviour
    {
        public enum Category { Sfx, Bgm }

        [SerializeField] private Category m_Category = Category.Sfx;

        private AudioSource m_Source;
        private float m_DesignVolume;
        private bool m_Bound;

        // 순수 C# 구독 핸들 — 규칙 9(`?.` 금지)는 UnityEngine.Object 파생에만 해당한다
        private IDisposable m_Link;

        /// <summary>지금 따라가는 카테고리 — QA·테스트가 배선을 확인한다.</summary>
        public Category Kind => m_Category;

        /// <summary>
        /// 카테고리를 정한다. **언제 불러도 늦지 않다** — 아직 구독 전이면 지금 구독하고,
        /// 이미 구독했으면 갈아 끼운다. `Awake` 순서에 기대지 않는 것이 이 고침의 요점이다.
        ///
        /// 씬·프리팹에 구워 두는 경로(`EditorAssetUtil.AddVolumeFollower`)는 이 함수를 안 쓴다 —
        /// `SerializedObject`로 필드를 직접 쓴다. 그래서 여기서 구독을 시작해도 에디터에 안 샌다.
        /// </summary>
        public void SetCategory(Category _category)
        {
            m_Category = _category;
            Bind();
        }

        /// <summary>
        /// 코드로 재생기를 세우는 경로 전용 — 컴포넌트를 붙이면서 카테고리까지 준다.
        /// 「붙이고 나서 고친다」는 순서 자체를 부르는 쪽에서 없애려고 있다.
        /// </summary>
        public static AudioVolumeFollower Attach(GameObject _go, Category _category)
        {
            if (_go == null) return null;
            var follower = _go.AddComponent<AudioVolumeFollower>();
            follower.SetCategory(_category);
            return follower;
        }

        private void Awake() => Bind();

        private void OnDestroy()
        {
            m_Link?.Dispose();
            m_Link = null;
        }

        /// <summary>
        /// 지금 카테고리를 구독한다. **설계 볼륨은 딱 한 번**만 기억한다 —
        /// 다시 잡으면 `설계값 × 사용자 볼륨`을 설계값으로 오해해 슬라이더를 움직일수록 소리가 작아진다.
        /// </summary>
        private void Bind()
        {
            if (!m_Bound)
            {
                m_Source = GetComponent<AudioSource>();
                m_DesignVolume = m_Source != null ? m_Source.volume : 1f;
                m_Bound = true;
            }

            m_Link?.Dispose();

            ReactiveProperty<float> setting =
                m_Category == Category.Bgm ? UserSettings.BgmVolume : UserSettings.SfxVolume;

            m_Link = setting.Subscribe(v =>
            {
                if (m_Source != null) m_Source.volume = m_DesignVolume * v;
            }).AddTo(this);
        }
    }
}
