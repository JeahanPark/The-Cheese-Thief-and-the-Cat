using System;
using System.Threading;
using CTC.Runtime.Match;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 용사 쥐 모습(2026-09-19) — 칼을 **쓸 때만** 앞발에 띄운다. 표를 밟는 **바로 그 순간**이면 1.5초 변신 연출
    /// (<see cref="HeroTransformLogic"/> — 엉덩이에서 칼을 뽑아 하늘을 찌른다)을 틀고 그동안 몸을 잠근다.
    /// 휘두를 때(<c>SwordSwing</c> 원샷이 걸리는 순간)는 <see cref="HeroSwordVisualLogic"/>의 시간표로 칼을 띄운다.
    ///
    /// **평소엔 칼이 안 보인다** — 네 발로 걸을 때 앞발에 칼을 들면 걸음마다 바닥·볼을 뚫는다(블렌더 렌더로 확인).
    /// 총·몽둥이와 같은 규칙이다. 늦게 들어온 사람에게도 연출 없이 빈손이다.
    ///
    /// **통신 0** — 변신은 복제값(<see cref="MouseJob.JobChanged"/>), 휘두르기는 전 클라가 받는 원샷
    /// (<see cref="PlayerAnimationDriver.OneShotStarted"/> — <c>MousePopgun.SwordSwingRpc</c>가 튼다)을 각자 본다.
    /// 서버(호스트)도 같은 컴포넌트를 돌려 변신 잠금을 같은 값으로 본다(<see cref="ActionLock"/>).
    ///
    /// 칼은 프리팹에 **꺼진 채로 구워져 있다**(<c>HeroPropBaker</c>) — 이름(<see cref="SWORD_NAME"/>)으로 찾는다.
    /// 방향은 리그에 손목이 없어 **LateUpdate에서 캐릭터 기준으로 덮어쓴다**(애니메이터가 뼈를 돌린 뒤라야 앞발을 따라간다).
    /// 알파는 <see cref="MaterialPropertyBlock"/>으로 민다 — 칼 머티리얼은 반투명으로 구워야 한다(불투명이면 URP가 알파를 무시한다).
    /// </summary>
    public class HeroLook : MonoBehaviour
    {
        public const string SWORD_NAME = "HeroSword";

        /// <summary>QA: 지금 도는 칼 시간표 이름.</summary>
        public const string MOTION_DRAW = "draw";
        public const string MOTION_SWING = "swing";

        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        private MouseJob m_Job;
        private PlayerAnimationDriver m_Driver;
        private Transform m_Frame;                       // 캐릭터 기준(Model — 애니메이터가 있는 곳, 없으면 루트)
        private Transform m_Sword;
        private Renderer[] m_SwordRenderers = Array.Empty<Renderer>();
        private Color[] m_SwordColors = Array.Empty<Color>();
        private MaterialPropertyBlock m_Block;           // 순수 C# — 규칙 9 대상 아님
        private CancellationTokenSource m_Cts;

        private bool m_Active;
        private bool m_Transforming;
        private bool m_Bound;                            // 구독 순간의 현재 값을 흘린 뒤부터가 「지금 막」 바뀐 것이다
        private bool m_Aiming;
        private Vector2 m_Aim;                           // (yaw, pitch) — LateUpdate가 칼에 입힌다
        private float m_SwordAlpha;
        private string m_Motion = "";

        /// <summary>용사인가.</summary>
        public bool Active => m_Active;
        /// <summary>변신 연출 중 — 이 동안 이동·공격·액티브가 잠긴다.</summary>
        public bool Transforming => m_Transforming;
        /// <summary>QA: 지금 칼 알파(0~1).</summary>
        public float SwordAlpha => m_SwordAlpha;
        /// <summary>QA: 지금 도는 칼 시간표(<see cref="MOTION_DRAW"/>·<see cref="MOTION_SWING"/>, 없으면 빈 문자열).</summary>
        public string Motion => m_Motion;

        /// <summary>그 몸이 용사 변신 중인가 — 컴포넌트가 없는 몸(고양이·강아지)은 거짓.</summary>
        public static bool TransformingOf(Component _body)
        {
            if (_body == null) return false;
            var look = _body.GetComponent<HeroLook>();
            return look != null && look.Transforming;
        }

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_Job = GetComponent<MouseJob>();
            m_Driver = GetComponent<PlayerAnimationDriver>();
            Transform model = transform.Find("Model");
            m_Frame = model != null ? model : transform;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != SWORD_NAME) continue;
                m_Sword = t;
                break;
            }
            CacheRenderers();
            Apply(0f);
        }

        private void Start()
        {
            if (m_Job != null) m_Job.JobChanged.Subscribe(OnJobChanged).AddTo(this);
            if (m_Driver != null) m_Driver.OneShotStarted += OnOneShot;
        }

        private void OnDestroy()
        {
            if (m_Driver != null) m_Driver.OneShotStarted -= OnOneShot;
        }

        private void OnDisable() => Cancel();

        private void OnJobChanged(MouseJobId _job)
        {
            // 판정의 주인은 Combat.HeroSwordLogic이다 — 같은 조건을 여기서 다시 쓰지 않는다.
            bool active = Combat.HeroSwordLogic.Active(_job, MatchPhase.Hunt);
            bool justBecame = active && !m_Active && m_Bound;
            m_Bound = true;
            if (active == m_Active) return;

            m_Active = active;
            if (justBecame) PlayTransform();
            else HideNow();
        }

        // 원샷이 걸리는 순간(전 클라). 휘두르기면 칼 시간표를 같은 박자로 틀고, 굳으면 칼을 거둔다.
        private void OnOneShot(string _state, float _seconds)
        {
            if (_state == AnimationStateLogic.Stunned)
            {
                HideNow();   // 맞아 굳었다 — 들던 칼도 변신도 거기서 끝난다(잠금은 스턴이 이어받는다)
                return;
            }
            if (_state != AnimationStateLogic.SwordSwing || m_Transforming) return;
            Run(MOTION_SWING, HeroSwordVisualLogic.SWING_SECONDS, HeroSwordVisualLogic.SwingAlpha,
                HeroSwordVisualLogic.SwingAim, false);
        }

        /// <summary>칼을 바로 거둔다(연출 없이).</summary>
        public void HideNow()
        {
            Cancel();
            m_Aiming = false;
            m_Motion = "";
            Apply(0f);
        }

        /// <summary>변신 — 하던 행동은 결과를 성공시키고, 총을 넣고, 클립을 틀고, 칼을 뽑아 든다.</summary>
        public void PlayTransform()
        {
            foreach (ICompletableAction action in GetComponents<ICompletableAction>()) action.CompleteNow();
            var gun = GetComponent<MouseGunVisual>();
            if (gun != null) gun.HideNow();

            if (m_Driver != null) m_Driver.PlayOneShot(AnimationStateLogic.HeroDraw, HeroTransformLogic.SECONDS);
            Run(MOTION_DRAW, HeroTransformLogic.SECONDS, HeroTransformLogic.SwordAlpha, HeroTransformLogic.SwordAim, true);
        }

        private void Run(string _motion, float _seconds, Func<float, float> _alpha, Func<float, Vector2> _aim, bool _lock)
        {
            Cancel();
            m_Motion = _motion;
            m_Transforming = _lock;
            m_Cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunAsync(_seconds, _alpha, _aim, m_Cts.Token).Forget();
        }

        private async UniTaskVoid RunAsync(float _seconds, Func<float, float> _alpha, Func<float, Vector2> _aim,
            CancellationToken _token)
        {
            float start = Time.time;
            while (true)
            {
                float elapsed = Time.time - start;
                m_Aim = _aim(elapsed);
                m_Aiming = true;
                Apply(_alpha(elapsed));
                if (elapsed >= _seconds) break;
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;   // 끊은 쪽이 이미 상태를 정했다
            }
            m_Transforming = false;
            m_Aiming = false;
            m_Motion = "";
            if (this != null) Apply(0f);
        }

        // 애니메이터가 뼈를 돌린 **뒤**에 칼 방향을 덮어쓴다 — 자리는 앞발(부모 뼈)을 따라가고 방향만 코드가 쥔다.
        private void LateUpdate()
        {
            if (!m_Aiming || m_Sword == null || !m_Sword.gameObject.activeSelf) return;
            m_Sword.rotation = m_Frame.rotation
                               * HeroSwordVisualLogic.LocalRotation(HeroSwordVisualLogic.Direction(m_Aim.x, m_Aim.y));
        }

        private void Cancel()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            m_Transforming = false;
        }

        // 알파를 먼저 넣고 켠다 — 켠 다음에 넣으면 직전 알파로 한 프레임 그려진다(GangLookVisual과 같다).
        private void Apply(float _alpha)
        {
            m_SwordAlpha = _alpha;
            bool render = GunFadeLogic.ShouldRender(_alpha);
            if (render)
            {
                for (int i = 0; i < m_SwordRenderers.Length; i++)
                {
                    Renderer r = m_SwordRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(m_Block);
                    Color tint = m_SwordColors[i];
                    tint.a = _alpha;
                    m_Block.SetColor(BASE_COLOR_ID, tint);
                    m_Block.SetColor(COLOR_ID, tint);
                    r.SetPropertyBlock(m_Block);
                }
            }
            if (m_Sword != null && m_Sword.gameObject.activeSelf != render) m_Sword.gameObject.SetActive(render);
        }

        // 칼은 날·자루 두 조각(머티리얼이 다르다) — 조각마다 제 색을 기억해 알파만 바꾼다.
        private void CacheRenderers()
        {
            if (m_Sword == null) return;
            m_SwordRenderers = m_Sword.GetComponentsInChildren<Renderer>(true);
            m_SwordColors = new Color[m_SwordRenderers.Length];
            for (int i = 0; i < m_SwordRenderers.Length; i++)
            {
                Color color = Color.white;
                Material m = m_SwordRenderers[i] != null ? m_SwordRenderers[i].sharedMaterial : null;
                if (m != null)
                {
                    if (m.HasProperty(BASE_COLOR_ID)) color = m.GetColor(BASE_COLOR_ID);
                    else if (m.HasProperty(COLOR_ID)) color = m.GetColor(COLOR_ID);
                }
                m_SwordColors[i] = color;
            }
        }
    }
}
