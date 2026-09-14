using CTC.Runtime.Match;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 깡패 쥐 모습(2026-09-14) — 2페이즈에 깡패 편이면 **두 발 이동 · 몽둥이 · 모자 · 살짝 화난 눈**을 켠다. 그것만 한다(규칙 7).
    /// 설계: docs/superpowers/specs/2026-09-14-gang-mouse-look-design.md
    ///
    /// **통신 0.** 복제값 둘(<see cref="MouseJob.JobChanged"/> · <see cref="PhaseDirector.PhaseChanged"/>)을 전 클라가 각자 구독한다 —
    /// 늦게 들어온 사람도 현재 값이 흘러 맞는 모습이 된다. 공격은 <c>MousePopgun</c>이 같은 조건으로 몽둥이로 바꾼다.
    ///
    /// 몽둥이·모자는 프리팹에 **꺼진 채로 구워져 있다**(<c>GangLookBaker</c>). 이름으로 찾는다.
    /// </summary>
    public class GangLook : MonoBehaviour
    {
        public const string CLUB_NAME = "GangClub";
        public const string HAT_NAME = "GangHat";

        private MouseJob m_Job;
        private PlayerAnimationDriver m_Driver;
        private GangAngryFace m_Face;
        private GameObject m_Club;
        private GameObject m_Hat;

        private MouseJobId m_CurrentJob;
        private MatchPhase m_CurrentPhase;
        private bool m_Active;

        public bool Active => m_Active;

        private void Awake()
        {
            m_Job = GetComponent<MouseJob>();
            m_Driver = GetComponent<PlayerAnimationDriver>();
            m_Face = GetComponent<GangAngryFace>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == CLUB_NAME) m_Club = t.gameObject;
                else if (t.name == HAT_NAME) m_Hat = t.gameObject;
            }
            Apply(false);
        }

        private void Start()
        {
            if (m_Job == null) return;
            m_Job.JobChanged.Subscribe(_job => { m_CurrentJob = _job; Refresh(); }).AddTo(this);
            BindPhaseAsync().Forget();
        }

        // PhaseDirector는 인게임 씬의 NetworkObject라 이 몸보다 늦게 설 수 있다 — 설 때까지 기다렸다 한 번 구독한다.
        private async UniTaskVoid BindPhaseAsync()
        {
            PhaseDirector phase = null;
            var token = destroyCancellationToken;
            while (phase == null)
            {
                phase = FindAnyObjectByType<PhaseDirector>();
                if (phase != null && phase.IsSpawned) break;
                phase = null;
                bool cancelled = await UniTask.Delay(500, cancellationToken: token).SuppressCancellationThrow();
                if (cancelled) return;
            }
            phase.PhaseChanged.Subscribe(_phase => { m_CurrentPhase = _phase; Refresh(); }).AddTo(this);
        }

        private void Refresh()
        {
            Apply(GangLookLogic.Active(m_CurrentJob, m_CurrentPhase));
        }

        private void Apply(bool _active)
        {
            m_Active = _active;
            if (m_Driver != null) m_Driver.Biped = _active;
            if (m_Club != null) m_Club.SetActive(_active);
            if (m_Hat != null) m_Hat.SetActive(_active);
            if (m_Face != null) m_Face.enabled = _active;
        }
    }
}
