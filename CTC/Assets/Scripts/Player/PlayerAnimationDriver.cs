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
        [SerializeField] private float walkThreshold = 0.1f;
        [SerializeField] private float runThreshold = 3.5f;
        [SerializeField] private float crossFadeSeconds = 0.15f;
        [SerializeField] private float speedSmoothing = 0.25f;

        private Animator _animator;
        private Vector3 _lastPosition;
        private float _smoothedSpeed;
        private string _currentState = "";

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
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, speedSmoothing);

            string state = AnimationStateLogic.FromSpeed(_smoothedSpeed, walkThreshold, runThreshold);
            if (state == _currentState) return;

            _currentState = state;
            _animator.CrossFadeInFixedTime(state, crossFadeSeconds);
        }
    }
}
