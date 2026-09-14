using CTC.Runtime.Match;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 깡패 쥐 모습(2026-09-14) — 2페이즈에 깡패 편이면 **두 발 이동 · 몽둥이 · 모자 · 화난 눈썹**을 켠다. 켤지만 정한다(규칙 7) —
    /// 보여 주기·변신 연출은 <see cref="GangLookVisual"/>.
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
        /// <summary>화난 눈썹 둘 — <c>GangBrow.L</c>·<c>GangBrow.R</c>(2026-09-14).</summary>
        public const string BROW_PREFIX = "GangBrow.";

        private MouseJob m_Job;
        private PlayerAnimationDriver m_Driver;
        private GangLookVisual m_Visual;

        private MouseJobId m_CurrentJob;
        private MatchPhase m_CurrentPhase;
        private bool m_Active;

        // 페이즈 구독이 **현재 값을 한 번 흘린 뒤**부터가 「지금 막 바뀐」 변화다. 그 전의 값(구독 순간의 현재 값·늦은 입장)은
        // 이미 그런 상태였던 것이라 변신 연출을 틀지 않는다 — 판 중간에 들어온 사람 화면에서 남의 쥐들이 일제히 변신하면 거짓말이다.
        private bool m_PhaseBound;

        public bool Active => m_Active;

        private void Awake()
        {
            m_Job = GetComponent<MouseJob>();
            m_Driver = GetComponent<PlayerAnimationDriver>();
            m_Visual = GetComponent<GangLookVisual>();
            if (m_Driver != null) m_Driver.Biped = false;   // 소품은 GangLookVisual이 제 Awake에서 숨긴다(Awake 순서를 믿지 않는다)
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
            m_PhaseBound = true;
        }

        private void Refresh()
        {
            bool active = GangLookLogic.Active(m_CurrentJob, m_CurrentPhase);
            if (active == m_Active) return;

            m_Active = active;
            if (m_Driver != null) m_Driver.Biped = active;
            if (m_Visual == null) return;

            // 깡패판이 **지금 막** 열렸다 → 변신(2026-09-14 사용자 요청). 이미 그 상태였거나 꺼질 때는 바로.
            if (active && m_PhaseBound) m_Visual.PlayTransform();
            else m_Visual.ShowInstant(active);
        }
    }
}
