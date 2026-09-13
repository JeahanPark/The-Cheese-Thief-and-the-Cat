using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 원샷 동작이 도는 동안 얼굴 표정을 바꿨다가 기본 얼굴로 돌려놓는다 — **그것만** 한다(규칙 7).
    ///
    /// 무엇을 켤지는 <see cref="FaceExpressionLogic"/>이 정한다. 이 클래스는 <see cref="PlayerAnimationDriver"/>의
    /// 원샷 사건을 듣고 팩 컨트롤러의 <c>Shapekey</c> 레이어에 크로스페이드만 건다.
    ///
    /// **통신 0.** 원샷을 부르는 쪽이 이미 전원에게 RPC로 펴 주므로 각 클라가 제 화면에서 건다.
    ///
    /// 눈동자·깜빡임(<see cref="EyeBlendShapes"/>)과 **안 싸운다**: 표정 클립은 <c>eyes.happy</c> 같은
    /// 자기 키를 쓰고, 시선·감김 키는 <see cref="EyeLook"/>이 <c>LateUpdate</c>에서 애니메이터 **다음에** 덮는다.
    /// </summary>
    public class FaceExpression : MonoBehaviour
    {
        private const float FADE_SECONDS = 0.08f;

        private PlayerAnimationDriver m_Driver;
        private Animator m_Animator;
        private int m_Layer = -1;
        private CancellationTokenSource m_ReturnCts;
        private string m_Requested = FaceExpressionLogic.DEFAULT;

        private void Awake()
        {
            m_Driver = GetComponent<PlayerAnimationDriver>();
            m_Animator = GetComponentInChildren<Animator>();
            if (m_Animator != null) m_Layer = m_Animator.GetLayerIndex(FaceExpressionLogic.LAYER_NAME);

            if (m_Driver == null || m_Layer < 0)
            {
                // 표정 레이어가 없는 컨트롤러(개 등)면 조용히 빠진다. 쥐·고양이인데 여기 오면 팩이 바뀐 것이다.
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (m_Driver != null) m_Driver.OneShotStarted += OnOneShotStarted;
        }

        private void OnDisable()
        {
            if (m_Driver != null) m_Driver.OneShotStarted -= OnOneShotStarted;
            CancelReturn();
        }

        private void OnOneShotStarted(string _state, float _seconds)
        {
            string face = FaceExpressionLogic.ForOneShot(_state);
            if (face == null || m_Animator == null) return;

            Show(face);
            CancelReturn();
            m_ReturnCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ReturnAsync(_seconds, m_ReturnCts.Token).Forget();
        }

        private async UniTaskVoid ReturnAsync(float _seconds, CancellationToken _token)
        {
            bool cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(_seconds), cancellationToken: _token)
                .SuppressCancellationThrow();
            if (cancelled) return;
            Show(FaceExpressionLogic.DEFAULT);
        }

        private void Show(string _face)
        {
            if (m_Animator == null) return;
            m_Requested = _face;
            m_Animator.CrossFadeInFixedTime(_face, FADE_SECONDS, m_Layer);
        }

        private void CancelReturn()
        {
            m_ReturnCts?.Cancel();
            m_ReturnCts?.Dispose();
            m_ReturnCts = null;
        }

        /// <summary>
        /// QA 전용 — 표정 레이어가 **실제로 들어가 있는** 상태 이름(전이 중이면 목적지).
        /// 시킨 값(<see cref="m_Requested"/>)을 돌려주면 철자 하나로 죽은 표정이 초록으로 통과한다.
        /// 모르는 상태면 <c>?해시</c>, 레이어가 없으면 빈 문자열.
        /// </summary>
        public string DevPlayingExpression()
        {
            if (m_Animator == null || m_Layer < 0) return "";
            AnimatorStateInfo info = m_Animator.IsInTransition(m_Layer)
                ? m_Animator.GetNextAnimatorStateInfo(m_Layer)
                : m_Animator.GetCurrentAnimatorStateInfo(m_Layer);
            foreach (string name in FaceExpressionLogic.Used)
            {
                if (info.IsName(name)) return name;
            }
            return "?" + info.shortNameHash;
        }

        /// <summary>QA 전용 — 마지막으로 시킨 표정.</summary>
        public string DevRequestedExpression => m_Requested;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 표정도 원샷만큼만 산다(0.35~0.6초) — 한 번 물어보는 QA는 왕복 지연에 놓친다.
        // PlayerAnimationDriver.ObservedStates와 같은 방식으로 **실제로 들어간** 표정을 적어 둔다.
        private const int OBSERVED_CAP = 24;
        private readonly List<string> m_ObservedFaces = new List<string>();

        public IReadOnlyList<string> DevObservedFaces => m_ObservedFaces;

        public void DevClearObservedFaces() => m_ObservedFaces.Clear();

        private void Update()
        {
            string now = DevPlayingExpression();
            if (string.IsNullOrEmpty(now)) return;
            if (m_ObservedFaces.Count > 0 && m_ObservedFaces[m_ObservedFaces.Count - 1] == now) return;
            if (m_ObservedFaces.Count >= OBSERVED_CAP) m_ObservedFaces.RemoveAt(0);
            m_ObservedFaces.Add(now);
        }
#endif
    }
}
