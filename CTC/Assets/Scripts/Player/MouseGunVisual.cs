using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 쥐 앞발(`leg.F.R`)에 물린 총 모델을 **쏠 때만** 보이게 한다.
    ///
    /// 왜 평소엔 숨기는가(정보 설계 원칙): 이 게임은 **이미 있는 정보를 그림으로 바꿀 뿐,
    /// 없던 정보를 만들지 않는다.** 총이 상시로 보이면 고양이는 쥐가 한 발도 쏘기 전에
    /// "저 쥐는 무장했다"를 알게 된다 — 원래 없던 정보다. 쏜 뒤 잠깐만 보이는 것은
    /// 이미 들린 총성·궤적선이 알려 준 것을 그림으로 되풀이하는 것뿐이다.
    ///
    /// 그래서 **티어(새총/기관총/긴 총열)가 달라도 모델은 하나다.** 다른 모델을 주면
    /// 「저 쥐가 몇 티어인가」가 새로 새는데, 소리는 거리로 줄지만 보이는 모델은 안 줄어든다.
    ///
    /// 전원이 본다 — <c>MousePopgun.TracerRpc</c>가 전 클라이언트에서 돌고, 여기엔
    /// 소유자 검사가 한 줄도 없다. 남의 쥐가 쏘는 것도 똑같이 보여야 한다.
    /// </summary>
    public class MouseGunVisual : MonoBehaviour
    {
        /// <summary>
        /// 마지막 발 뒤 총을 다시 넣기까지. 연사(기관총 ~0.1초 간격)를 넉넉히 덮으면서도
        /// 교전이 끝나면 곧 사라져야 해서 2초다 — 이보다 길면 "무장했다"가 계속 새고,
        /// 짧으면 연사 중에 넣었다 뽑았다 한다.
        /// </summary>
        private const float HOLSTER_DELAY_SECONDS = 2f;

        [SerializeField] private GameObject m_Gun;

        /// <summary>순수 C# 객체라 `?.`를 써도 된다(규칙 9 예외).</summary>
        private CancellationTokenSource m_HolsterCts;

        /// <summary>지금 총이 손에 나와 있나.</summary>
        public bool IsOut => m_Gun != null && m_Gun.activeSelf;

        private void Awake()
        {
            // 프리팹에 비활성으로 구워 두지만, 누가 씬에서 켜 놨더라도 시작은 항상 숨김이다.
            if (m_Gun != null) m_Gun.SetActive(false);
        }

        /// <summary>
        /// 한 발 쐈다 — 총을 보이게 하고 수납 카운트다운을 **다시** 건다.
        /// </summary>
        /// <returns>
        /// 부르기 **직전에** 이미 총이 나와 있었나. 호출부는 이 값으로 클립을 고른다
        /// (<see cref="MouseGunVisualLogic.ClipForShot"/>) — 나와 있었으면 반동만, 아니면 뽑기부터.
        /// </returns>
        public bool ShowForShot()
        {
            bool wasOut = IsOut;

            if (m_Gun != null) m_Gun.SetActive(true);
            RestartHolster();

            return wasOut;
        }

        /// <summary>수납을 즉시 — 죽음·역할 변경처럼 "손에 있을 리 없는" 상황용.</summary>
        public void HideNow()
        {
            CancelHolster();
            if (m_Gun != null) m_Gun.SetActive(false);
        }

        private void OnDisable() => CancelHolster();

        // 연사 중에는 매 발이 이전 카운트다운을 끊고 다시 건다 — 코루틴의 StopCoroutine과 같은 뜻.
        private void RestartHolster()
        {
            CancelHolster();
            m_HolsterCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            HolsterAsync(m_HolsterCts.Token).Forget();
        }

        private void CancelHolster()
        {
            m_HolsterCts?.Cancel();
            m_HolsterCts?.Dispose();
            m_HolsterCts = null;
        }

        private async UniTaskVoid HolsterAsync(CancellationToken _token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(HOLSTER_DELAY_SECONDS), cancellationToken: _token);
            }
            catch (OperationCanceledException)
            {
                // 다음 발이 카운트다운을 다시 걸었다 — 여기서 숨기면 연사 중에 총이 깜빡인다.
                return;
            }

            // 파괴 중일 수 있다. Unity 객체는 `!= null`로만 검사한다(규칙 9).
            if (this != null && m_Gun != null) m_Gun.SetActive(false);
        }
    }
}
