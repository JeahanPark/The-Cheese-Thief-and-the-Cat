using CTC.Runtime.Core;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 옵션 창 — 마우스 감도 · 배경음 · 효과음 · **해상도 · 창모드** (2026-08-01 · 화면 2026-08-30).
    ///
    /// MVC: Model은 `UserSettings`(PlayerPrefs + R3), 여기는 **표시와 입력 전달만** 한다.
    /// 슬라이더를 움직이면 곧바로 Model에 쓰고, Model이 바뀌면 슬라이더·숫자가 따라온다.
    /// 카메라·오디오는 이 창을 전혀 모른 채 Model만 본다.
    ///
    /// **미리듣기 / 확인 / 취소** (2026-08-09). 끄는 즉시 소리·감도가 바뀌지만 그건 미리듣기고,
    /// 디스크에 남는 건 "확인"을 눌렀을 때뿐이다. 소리는 들어 봐야 고를 수 있으니 즉시 반영하고,
    /// 되돌릴 길은 남겨 둔다.
    ///
    /// 되돌리기를 <see cref="Close"/> 한 곳에 몰아넣은 이유: 창을 닫는 길이 셋(취소·ESC·씬 종료)이라
    /// 길마다 따로 처리하면 하나를 빠뜨린다. "확인"은 저장부터 하므로 이어지는 되돌리기가 무효타다 —
    /// **닫히고 나면 언제나 저장된 값 = 지금 들리는 값**이라는 불변식이 공짜로 선다.
    ///
    /// 멀티플레이라 **게임을 멈추지 않는다**(`Time.timeScale` 안 건드림). 대신 커서를 풀고
    /// `UiFocus.ModalOpen`을 세워 조작을 막는다 — 열어 둔 채로 죽을 수 있다는 뜻이라,
    /// 창을 크게 만들지 않고 화면을 다 가리지도 않는다.
    /// </summary>
    public class OptionsPopup : MonoBehaviour
    {
        [SerializeField] private GameObject m_Panel;
        [SerializeField] private Slider m_SensitivitySlider;
        [SerializeField] private Slider m_BgmSlider;
        [SerializeField] private Slider m_SfxSlider;
        [SerializeField] private TMP_Text m_SensitivityValue;
        [SerializeField] private TMP_Text m_BgmValue;
        [SerializeField] private TMP_Text m_SfxValue;
        [Header("화면")]
        [SerializeField] private TMP_Text m_ResolutionValue;
        [SerializeField] private Button m_ResolutionPrev;
        [SerializeField] private Button m_ResolutionNext;
        [SerializeField] private TMP_Text m_ModeValue;
        [SerializeField] private Button m_ModePrev;
        [SerializeField] private Button m_ModeNext;

        [Header("버튼")]
        [SerializeField] private Button m_ResetButton;
        [SerializeField] private Button m_ConfirmButton;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Button m_QuitButton;

        public bool IsOpen => m_Panel != null && m_Panel.activeSelf;

        private void Start()
        {
            // Start인 이유: UserSettings가 InfoManager(CSV)를 읽으므로 다른 Awake보다 뒤여야 안전하다.
            BindSlider(m_SensitivitySlider, m_SensitivityValue, UserSettings.MouseSensitivity,
                UserSettings.MIN_SENSITIVITY, UserSettings.MAX_SENSITIVITY, "0.00");
            BindSlider(m_BgmSlider, m_BgmValue, UserSettings.BgmVolume, 0f, 1f, "0%");
            BindSlider(m_SfxSlider, m_SfxValue, UserSettings.SfxVolume, 0f, 1f, "0%");

            BindCycler(m_ResolutionPrev, m_ResolutionNext, m_ResolutionValue,
                DisplaySettings.ResolutionIndex, () => DisplaySettings.Options.Count, ResolutionText);
            BindCycler(m_ModePrev, m_ModeNext, m_ModeValue,
                DisplaySettings.Mode, () => System.Enum.GetValues(typeof(WindowMode)).Length,
                i => DisplayLogic.ModeLabel((WindowMode)i));

            if (m_ResetButton != null) m_ResetButton.OnClickAsObservable().Subscribe(_ => OnClickReset()).AddTo(this);
            if (m_ConfirmButton != null) m_ConfirmButton.OnClickAsObservable().Subscribe(_ => OnClickConfirm()).AddTo(this);
            if (m_CloseButton != null) m_CloseButton.OnClickAsObservable().Subscribe(_ => OnClickCancel()).AddTo(this);
            if (m_QuitButton != null) m_QuitButton.OnClickAsObservable().Subscribe(_ => OnClickQuit()).AddTo(this);

            if (m_Panel != null) m_Panel.SetActive(false);
        }

        public void Open()
        {
            if (m_Panel == null || m_Panel.activeSelf) return;
            m_Panel.SetActive(true);
            UiFocus.SetModalOpen(true);
        }

        /// <summary>창을 닫는 유일한 문 — 확정하지 않은 미리듣기는 여기서 되돌아간다.</summary>
        public void Close()
        {
            if (m_Panel == null || !m_Panel.activeSelf) return;
            UserSettings.Revert();
            DisplaySettings.Revert();
            m_Panel.SetActive(false);
            UiFocus.SetModalOpen(false);
        }

        private void OnDisable()
        {
            // 씬 전환·파괴로 창이 사라져도 모달 플래그가 남으면 조작이 영영 막힌다
            if (UiFocus.ModalOpen) UiFocus.SetModalOpen(false);
        }

        /// <summary>
        /// 화면은 CSV 기본값이 없다 — 기기마다 다르기 때문이다. 대신 **가장 안전한 조합**으로 간다:
        /// 이 모니터의 최대 해상도 + 테두리 없는 창(알탭이 빠르고 스팀 오버레이가 제일 잘 뜬다).
        /// </summary>
        private void OnClickReset()
        {
            UserSettings.ResetToDefaults();
            DisplaySettings.ResolutionIndex.Value = 0;   // 목록은 큰 것이 앞이다
            DisplaySettings.Mode.Value = (int)WindowMode.Borderless;
        }

        // 저장이 먼저다 — 뒤따르는 Close()의 되돌리기가 방금 저장한 값을 다시 읽어 무효타가 된다
        private void OnClickConfirm()
        {
            UserSettings.Save();
            DisplaySettings.Save();
            Close();
        }

        private void OnClickCancel() => Close();
        private void OnClickQuit() => EscapeRouter.QuitGame();

        private static string ResolutionText(int _index)
        {
            System.Collections.Generic.IReadOnlyList<Vector2Int> options = DisplaySettings.Options;
            if (options.Count == 0) return "-";

            Vector2Int r = options[DisplayLogic.ClampIndex(_index, options.Count)];
            return DisplayLogic.ResolutionLabel(r.x, r.y);
        }

        /// <summary>
        /// ◀ 값 ▶ 한 줄. **드롭다운을 안 쓴 이유**: TMP_Dropdown은 템플릿·뷰포트·아이템까지
        /// 손으로 지어야 하는데, 이 프로젝트는 UI를 전부 코드로 굽는다. 고를 것이 서너 개뿐이라
        /// 화살표 두 개가 짓기도 쓰기도 간단하다.
        ///
        /// 양 끝에서 돈다 — 끝에서 막히면 "고장 났나" 싶어 한 번 더 누르게 된다.
        /// </summary>
        private void BindCycler(
            Button _prev, Button _next, TMP_Text _label,
            ReactiveProperty<int> _model, System.Func<int> _count, System.Func<int, string> _format)
        {
            if (_model == null) return;

            if (_prev != null) _prev.OnClickAsObservable().Subscribe(_ => Step(_model, _count, -1)).AddTo(this);
            if (_next != null) _next.OnClickAsObservable().Subscribe(_ => Step(_model, _count, +1)).AddTo(this);

            _model.Subscribe(v =>
            {
                if (_label != null) _label.text = _format(v);
            }).AddTo(this);
        }

        private static void Step(ReactiveProperty<int> _model, System.Func<int> _count, int _delta)
        {
            int count = _count();
            if (count <= 0) return;

            int next = (_model.Value + _delta) % count;
            if (next < 0) next += count;
            _model.Value = next;
        }

        // 양방향: 슬라이더 → Model, Model → 슬라이더·숫자.
        // Model에서 되돌아온 값을 다시 슬라이더에 쓰지만, Slider는 같은 값 대입 시
        // onValueChanged를 안 쏘므로 무한루프가 나지 않는다.
        private void BindSlider(
            Slider _slider, TMP_Text _label, ReactiveProperty<float> _model,
            float _min, float _max, string _format)
        {
            if (_slider == null) return;

            _slider.minValue = _min;
            _slider.maxValue = _max;

            // **순서가 중요하다.** R3의 OnValueChangedAsObservable는 구독 즉시 현재 값을 한 번 흘린다.
            // 슬라이더를 저장값으로 먼저 맞춰 두지 않으면 그 첫 방출(=0)이 Model을 덮어써
            // 창을 열자마자 감도·볼륨이 최소값으로 초기화된다(2026-08-01 실측).
            _slider.SetValueWithoutNotify(Mathf.Clamp(_model.Value, _min, _max));
            _slider.OnValueChangedAsObservable().Subscribe(v => _model.Value = v).AddTo(this);

            _model.Subscribe(v =>
            {
                _slider.SetValueWithoutNotify(v);
                if (_label != null) _label.text = _format == "0%" ? $"{Mathf.RoundToInt(v * 100f)}%" : v.ToString(_format);
            }).AddTo(this);
        }
    }
}
