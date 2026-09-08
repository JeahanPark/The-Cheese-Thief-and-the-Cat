using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 프레임 간 위치 변화로 수평 속도를 추정해 Idle/Walk/Run을 CrossFade한다.
    /// 소유자·원격 모두 동일 로직으로 돌므로 애니메이션 네트워크 동기화가 필요 없다.
    /// (원격은 NetworkTransform 보간 위치를 그대로 사용 — 스무딩으로 지터 흡수)
    /// </summary>
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private float m_WalkThreshold = 0.1f;
        [SerializeField] private float m_RunThreshold = 3.5f;
        [SerializeField] private float m_CrossFadeSeconds = 0.15f;
        [SerializeField] private float m_SpeedSmoothing = 0.25f;

        private Animator _animator;
        private Vector3 _lastPosition;
        private float _smoothedSpeed;
        private string _currentState = "";
        private float _oneShotUntil;

        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            _lastPosition = transform.position;
            if (_animator == null)
                Debug.LogWarning($"[PlayerAnimationDriver] {name}: 자식에 Animator가 없다");
        }

        private void Update()
        {
            if (_animator == null || Time.deltaTime <= 0f) return;

            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;

            float rawSpeed = delta.magnitude / Time.deltaTime;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, m_SpeedSmoothing);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Observe();
#endif

            string state = AnimationStateLogic.FromSpeed(_smoothedSpeed, m_WalkThreshold, m_RunThreshold);
            if (Time.time < _oneShotUntil || state == _currentState) return;

            _currentState = state;
            _animator.CrossFadeInFixedTime(state, m_CrossFadeSeconds);
        }

        /// <summary>
        /// 원샷 상태(Attack 등) 재생. 지속시간 동안 이동 기반 전이를 잠그고,
        /// 만료 후엔 속도 상태가 달라진 순간 자연 복귀한다. 전투 RPC(전 클라)에서 호출.
        /// </summary>
        public void PlayOneShot(string _state, float _seconds)
        {
            if (_animator == null) return;
            _oneShotUntil = Time.time + _seconds;
            _currentState = _state;
            _animator.CrossFadeInFixedTime(_state, m_CrossFadeSeconds);
        }

        /// <summary>
        /// **애니메이터가 지금 실제로 들어가 있는 상태**의 이름(전이 중이면 목적지). Animator가
        /// 없으면 빈 문자열, 아는 이름이 아니면 <c>?해시</c>.
        ///
        /// <see cref="_currentState"/>(요청한 이름)와 **다른 것**이라는 게 요점이다.
        /// <c>CrossFadeInFixedTime</c>은 없는 상태 이름을 받아도 에러도 경고도 없이
        /// 아무 일도 안 한다 — 그래서 「무엇을 시켰나」가 아니라 「무엇이 돌고 있나」를 읽어야
        /// 철자 하나로 죽은 원샷을 잡을 수 있다(2026-09-09 감사 B-04).
        /// </summary>
        public string PlayingState
        {
            get
            {
                if (_animator == null) return "";

                const int LAYER = 0;
                if (_animator.layerCount <= LAYER) return "";
                if (_animator.IsInTransition(LAYER))
                {
                    string next = NameOf(_animator.GetNextAnimatorStateInfo(LAYER));
                    if (!string.IsNullOrEmpty(next)) return next;
                }
                return NameOf(_animator.GetCurrentAnimatorStateInfo(LAYER));
            }
        }

        /// <summary>이 드라이버가 **시킨** 상태 이름 — 실제로 걸렸는지는 <see cref="PlayingState"/>가 안다.</summary>
        public string RequestedState => _currentState;

        private static string NameOf(AnimatorStateInfo _info)
        {
            foreach (string name in AnimationStateLogic.All)
            {
                if (_info.IsName(name)) return name;
            }
            return "?" + _info.shortNameHash;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ===== QA 관측 (2026-09-09 감사 B-04 · C-23) =====
        //
        // 원샷은 0.35~0.6초만 살아 있다. wesqa가 "지금 뭐가 도나"를 한 번 물어보는 방식으로는
        // 왕복 지연에 그대로 놓친다 — 놓친 것과 안 걸린 것을 구별할 수 없으면 검사가 아니다.
        // 그래서 **매 프레임 애니메이터가 실제로 들어간 상태**를 여기 적어 둔다. 목록에 그 이름이
        // 없는 것이 곧 "조용히 죽었다"의 증거다.

        private const int OBSERVED_CAP = 48;
        private readonly List<string> m_ObservedStates = new List<string>();

        /// <summary>리셋 이후 애니메이터가 실제로 들어갔던 상태들(연속 중복은 접는다).</summary>
        public IReadOnlyList<string> ObservedStates => m_ObservedStates;

        public void ClearObservedStates() => m_ObservedStates.Clear();

        private void Observe()
        {
            string now = PlayingState;
            if (string.IsNullOrEmpty(now)) return;
            if (m_ObservedStates.Count > 0 && m_ObservedStates[m_ObservedStates.Count - 1] == now) return;
            if (m_ObservedStates.Count >= OBSERVED_CAP) m_ObservedStates.RemoveAt(0);
            m_ObservedStates.Add(now);
        }
#endif
    }
}
