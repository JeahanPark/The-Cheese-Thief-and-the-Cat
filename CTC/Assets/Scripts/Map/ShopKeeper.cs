using System.Threading;
using CTC.Runtime.Player;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 상점 점원(2026-09-19, 기획 `2026-09-19-shop-booth-design.md` §4) — 부스 창구 안의 쥐.
    /// 평소엔 서 있고, 철문이 닫히면 **움츠리고**(<c>Fear</c> · 콩알 눈), 누가 이 상점에서 사면 **끄덕인다**(<c>Clicked</c> · 반짝 눈).
    ///
    /// 네트워크가 없다 — 철문 복제값(<see cref="ShopShutter.ClosedChanged"/>)과 구매 이펙트(<c>FxId.BuyCoins</c> — 전 클라에서
    /// 이미 터지는 것)만 본다. 원반 안에서 터진 구매만 이 가게 것이다.
    /// 팩 쥐 모델을 **밤색 팔레트**로 칠해 쓴다(<c>ShopBoothBuilder</c>) — 플레이어 쥐(청록)와 한눈에 갈린다.
    /// </summary>
    public class ShopKeeper : MonoBehaviour
    {
        /// <summary>끄덕이는 시간 — 끝나면 다시 선다.</summary>
        private const float NOD_SECONDS = 0.9f;

        private const float CROSS_FADE = 0.15f;

        [SerializeField] private ShopShutter m_Shutter;
        [SerializeField] private Animator m_Animator;

        private int m_FaceLayer = -1;
        private bool m_Closed;
        private CancellationTokenSource m_NodCts;

        /// <summary>QA: 지금 움츠려 있나.</summary>
        public bool Cowering => m_Closed;

        private void Start()
        {
            if (m_Animator != null) m_FaceLayer = m_Animator.GetLayerIndex(FaceExpressionLogic.LAYER_NAME);
            if (m_Shutter != null)
            {
                m_Shutter.ClosedChanged += OnClosedChanged;
                m_Closed = m_Shutter.Closed;
            }
            Fx.FxPlayer.Played += OnFxPlayed;
            Pose(m_Closed);
        }

        private void OnDestroy()
        {
            if (m_Shutter != null) m_Shutter.ClosedChanged -= OnClosedChanged;
            Fx.FxPlayer.Played -= OnFxPlayed;
            m_NodCts?.Cancel();
            m_NodCts?.Dispose();
        }

        private void OnClosedChanged(bool _closed)
        {
            m_Closed = _closed;
            m_NodCts?.Cancel();
            Pose(_closed);
        }

        // 구매 이펙트가 **이 가게 원반 안**에서 터졌다 — 끄덕인다(닫혀 있으면 움츠린 채).
        private void OnFxPlayed(Fx.FxId _id, Vector3 _at)
        {
            if (_id != Fx.FxId.BuyCoins || m_Closed || m_Shutter == null) return;
            float radius = Info.InfoManager.Instance.Balance.ShopSpotRadius;
            if (!ShopSpotLogic.Contains(m_Shutter.transform.position, _at, radius)) return;

            m_NodCts?.Cancel();
            m_NodCts?.Dispose();
            m_NodCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            NodAsync(m_NodCts.Token).Forget();
        }

        private async UniTaskVoid NodAsync(CancellationToken _token)
        {
            Play(AnimationStateLogic.Clicked, FaceExpressionLogic.HAPPY);
            bool cancelled = await UniTask.Delay(Mathf.RoundToInt(NOD_SECONDS * 1000f), cancellationToken: _token)
                .SuppressCancellationThrow();
            if (cancelled) return;
            Pose(m_Closed);
        }

        private void Pose(bool _closed)
        {
            if (_closed) Play(AnimationStateLogic.Fear, FaceExpressionLogic.SHRINK);
            else Play(AnimationStateLogic.Idle, FaceExpressionLogic.DEFAULT);
        }

        private void Play(string _body, string _face)
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled) return;
            m_Animator.CrossFadeInFixedTime(_body, CROSS_FADE, 0);
            if (m_FaceLayer >= 0) m_Animator.CrossFadeInFixedTime(_face, CROSS_FADE, m_FaceLayer);
        }
    }
}
