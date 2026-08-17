using System;
using CTC.Runtime.Core;
using CTC.Runtime.Match;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 내 직업과 지금 이겨야 하는 조건 — MVC의 View (2026-08-10).
    ///
    /// **부하는 거절을 못 한다.** 다가온 깡패가 F를 누르면 그 순간 승리 조건이 통째로 바뀌는데,
    /// 그때까지 화면에는 아무 변화도 없었다 — 임명당한 사람이 자기가 편이 바뀐 걸 몰랐다.
    /// 규칙이 바뀌는 순간을 알려 주는 게 이 HUD의 전부다.
    ///
    /// HUD 개편(2026-08-15): 상시 표시 → **변화 순간 3초 팝업**. 상시로 두면 화면 상단을
    /// 문장이 계속 차지한다("상시는 얇게"). 짧은 상시 배지는 우상단 StatusHud가 맡고,
    /// 여기는 규칙이 바뀐 순간(직업 획득·임명·2단계 진입)에만 문장을 띄운다.
    ///
    /// 규칙 3(폴링 금지)대로 `MouseJob`·`PhaseDirector`의 스트림을 구독한다.
    /// 로컬 플레이어가 바뀌면 `SerialDisposable`이 이전 구독을 끊는다(`StatusHud`와 같은 꼴).
    /// </summary>
    public class MouseJobHud : MonoBehaviour
    {
        private const float POPUP_SECONDS = 3f;

        private static readonly Color COLOR_GANG = new Color(0.95f, 0.45f, 0.35f);
        private static readonly Color COLOR_HERO = new Color(0.55f, 0.85f, 1f);

        [SerializeField] private PhaseDirector m_Phase;
        [SerializeField] private TMP_Text m_Text;

        private readonly SerialDisposable m_JobSub = new SerialDisposable();
        private readonly SerialDisposable m_PopupTimer = new SerialDisposable();

        private MouseJobId m_Job = MouseJobId.None;
        private bool m_GangPhase;

        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_JobSub.AddTo(this);
            m_PopupTimer.AddTo(this);

            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
            if (m_Phase != null) m_Phase.PhaseChanged.Subscribe(OnPhaseChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            var job = _player != null ? _player.GetComponent<MouseJob>() : null;
            if (job == null)
            {
                // 고양이이거나 관전 중 — 표시할 게 없다
                m_JobSub.Disposable = null;
                m_Job = MouseJobId.None;
                Hide();
                return;
            }

            m_JobSub.Disposable = job.JobChanged.Subscribe(j =>
            {
                bool changed = j != m_Job;
                m_Job = j;
                if (changed) Popup(); // 구독 첫 값이 None이면 changed=false — 조용히 지나간다
            });
        }

        private void OnPhaseChanged(MatchPhase _phase)
        {
            bool gang = _phase == MatchPhase.Gang;
            bool changed = gang != m_GangPhase;
            m_GangPhase = gang;
            if (changed) Popup(); // 2단계 진입 — 같은 직업이라도 이겨야 하는 조건이 바뀌었다
        }

        private void Popup()
        {
            if (m_Job == MouseJobId.None)
            {
                Hide();
                return;
            }

            m_Text.gameObject.SetActive(true);
            m_Text.color = MouseJobLogic.IsGangSide(m_Job) ? COLOR_GANG : COLOR_HERO;
            m_Text.text = LabelOf(m_Job, m_GangPhase);
            m_PopupTimer.Disposable = Observable
                .Timer(TimeSpan.FromSeconds(POPUP_SECONDS), UnityTimeProvider.Update)
                .Subscribe(_ => m_Text.gameObject.SetActive(false));
        }

        private void Hide()
        {
            m_PopupTimer.Disposable = null;
            m_Text.gameObject.SetActive(false);
        }

        // 직업 이름만으로는 "그래서 뭘 해야 하나"가 안 나온다 — 지금 이겨야 하는 조건을 같이 적는다.
        private static string LabelOf(MouseJobId _job, bool _gangPhase)
        {
            switch (_job)
            {
                case MouseJobId.Hero:
                    return _gangPhase ? "용사쥐 — 깡패 팀을 쓰러뜨려라" : "용사쥐 — 몸이 단단하고 화력이 세다";
                case MouseJobId.Gang:
                    return _gangPhase ? "깡패 보스 — 나머지 쥐를 전부 쓰러뜨려라"
                                      : "깡패쥐 — F로 부하를 임명한다 (고양이가 죽으면 2단계)";
                case MouseJobId.Minion:
                    return _gangPhase ? "부하쥐 — 보스 편이다. 나머지 쥐를 쓰러뜨려라"
                                      : "부하쥐 — 보스 편이 됐다 (고양이가 죽으면 나머지 쥐와 싸운다)";
                default:
                    return "";
            }
        }
    }
}
