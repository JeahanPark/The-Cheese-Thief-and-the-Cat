using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 쥐의 총 모델 **둘**을 쏘는 동안만 보이게 한다 — 허리춤에 얹힌 것과 앞발에 든 것.
    ///
    /// 왜 둘인가: 총을 든 본은 앞다리(`leg.F.R`)라 **엉덩이에 닿을 수가 없다.** 하나만
    /// 발에 물려 놓고 렌더해 보니 「가슴 밑에서 들어 올린다」로만 읽혔다(2026-09-12).
    /// 그래서 몸통 본에 허리춤 총을 따로 두고, 앞발이 허리를 떠나는 프레임에
    /// **보이기를 바꿔치기한다**(<see cref="MouseGunVisualLogic.DRAW_SWAP_SECONDS"/>).
    /// 게임에서 오래 쓰는 수법이고 여기서는 정직하다 — 순간이동하는 것이 없고,
    /// 바꾸는 시점이 손과 허리가 가장 가까운 순간이라 눈이 이어서 읽는다.
    ///
    /// 왜 평소엔 둘 다 숨기는가(정보 설계 원칙): 이 게임은 **이미 있는 정보를 그림으로
    /// 바꿀 뿐, 없던 정보를 만들지 않는다.** 총이 상시로 보이면 고양이는 쥐가 한 발도 쏘기
    /// 전에 "저 쥐는 무장했다"를 알게 된다 — 원래 없던 정보다.
    /// 그래서 **티어(새총/기관총/긴 총열)가 달라도 모델은 하나다**: 다른 모델을 주면
    /// 「저 쥐가 몇 티어인가」가 새는데, 소리는 거리로 줄지만 보이는 모델은 안 줄어든다.
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

        [SerializeField] private GameObject m_Gun;          // 앞발(leg.F.R)에 든 총
        [SerializeField] private GameObject m_HolsteredGun; // 허리춤(body)에 얹힌 총

        /// <summary>순수 C# 객체라 `?.`를 써도 된다(규칙 9 예외).</summary>
        private CancellationTokenSource m_HolsterCts;
        private CancellationTokenSource m_DrawCts;

        /// <summary>뽑는 중 — 허리춤 총이 보이고 아직 손으로 안 넘어갔다.</summary>
        private bool m_Drawing;

        /// <summary>
        /// 총이 나와 있나. **뽑는 중도 포함한다** — 뽑기가 도는 0.125초 사이에 다음 발이
        /// 오면(기관총은 0.1초 간격이다) 뽑기를 처음부터 다시 돌리면 안 되기 때문이다.
        /// 「무장 상태인가」로 읽으면 맞다.
        /// </summary>
        public bool IsOut => m_Drawing || (m_Gun != null && m_Gun.activeSelf);

        /// <summary>허리춤 총이 지금 보이나 — QA·미리보기가 「바꿔치기가 실제로 돌았나」를 묻는 자리.</summary>
        public bool IsHolsteredShowing => m_HolsteredGun != null && m_HolsteredGun.activeSelf;

        private void Awake()
        {
            // 프리팹에 둘 다 비활성으로 굽지만, 누가 씬에서 켜 놨더라도 시작은 항상 숨김이다.
            Show(false, false);
        }

        /// <summary>
        /// 한 발 쐈다 — 총을 보이게 하고 수납 카운트다운을 **다시** 건다.
        /// 처음 쏘는 것이면 허리춤부터 보여 주고 <see cref="MouseGunVisualLogic.DRAW_SWAP_SECONDS"/>
        /// 뒤에 손으로 넘긴다.
        /// </summary>
        /// <returns>
        /// 부르기 **직전에** 이미 총이 나와 있었나(뽑는 중 포함). 호출부는 이 값으로 클립을 고른다
        /// (<see cref="MouseGunVisualLogic.ClipForShot"/>) — 나와 있었으면 반동만, 아니면 뽑기부터.
        /// </returns>
        public bool ShowForShot()
        {
            bool wasOut = IsOut;

            // 뽑는 중에 다음 발이 와도 뽑기를 다시 걸지 않는다 — 예약된 바꿔치기가 그대로 끝난다.
            if (!wasOut) StartDraw();

            RestartHolster();
            return wasOut;
        }

        /// <summary>수납을 즉시 — 죽음·역할 변경처럼 "손에 있을 리 없는" 상황용.</summary>
        public void HideNow()
        {
            CancelDraw();
            CancelHolster();
            Show(false, false);
        }

        private void OnDisable()
        {
            CancelDraw();
            CancelHolster();
        }

        /// <summary>
        /// 둘의 보이기를 **한 군데서만** 바꾼다. 따로따로 켜고 끄면 「둘 다 보임」이나
        /// 「둘 다 사라짐」이 조용히 생긴다 — 여기로 모으면 그게 구조적으로 불가능하다.
        /// </summary>
        private void Show(bool _holstered, bool _held)
        {
            if (m_HolsteredGun != null) m_HolsteredGun.SetActive(_holstered);
            if (m_Gun != null) m_Gun.SetActive(_held);
        }

        private void StartDraw()
        {
            CancelDraw();
            m_Drawing = true;
            Show(true, false); // 허리춤에 총이 보이고, 앞발은 아직 비어 있다

            m_DrawCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            DrawAsync(m_DrawCts.Token).Forget();
        }

        private async UniTaskVoid DrawAsync(CancellationToken _token)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(MouseGunVisualLogic.DRAW_SWAP_SECONDS), cancellationToken: _token);
            }
            catch (OperationCanceledException)
            {
                // 취소한 쪽(수납·비활성·파괴)이 이미 보이기를 정했다 — 여기서 손대면 덮어쓴다.
                return;
            }

            m_Drawing = false;
            if (this != null) Show(false, true); // 바꿔치기 — 이 순간이 곧 '뽑았다'
        }

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

        private void CancelDraw()
        {
            m_DrawCts?.Cancel();
            m_DrawCts?.Dispose();
            m_DrawCts = null;
            m_Drawing = false;
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

            // 수납 시각이 뽑기보다 늦다(2초 > 0.125초)지만, 그 사이에 비활성·재활성이 끼면
            // 뽑기가 아직 살아 있을 수 있다 — 끊고 나서 숨긴다.
            CancelDraw();

            // 파괴 중일 수 있다. Unity 객체는 `!= null`로만 검사한다(규칙 9).
            if (this != null) Show(false, false);
        }
    }
}
