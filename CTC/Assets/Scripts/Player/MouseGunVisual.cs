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
    /// **보이기는 알파로 밟는다**(2026-09-12). <c>SetActive</c>만으로 껐다 켜면 총이 한 프레임에
    /// 튀어나오고 한 프레임에 사라진다 — 눈이 그걸 「등장」이 아니라 **「팝」**으로 읽는다.
    /// 시간표는 <see cref="GunFadeLogic"/>이 갖고 있고, 여기서는 그 결과를
    /// <see cref="MaterialPropertyBlock"/>으로 밀어 넣기만 한다.
    /// <b>머티리얼을 인스턴스화하지 않는다</b>(<c>renderer.material</c>은 렌더러마다 사본을
    /// 만들어 새어 나간다) — 프로퍼티 블록은 사본 없이 렌더러별 값만 덮어쓴다.
    ///
    /// 전원이 본다 — <c>MousePopgun.TracerRpc</c>가 전 클라이언트에서 돌고, 여기엔
    /// 소유자 검사가 한 줄도 없다. 남의 쥐가 쏘는 것도 똑같이 보여야 한다.
    /// </summary>
    public class MouseGunVisual : MonoBehaviour
    {
        /// <summary>
        /// 마지막 발 뒤 총을 다시 넣기까지. **2초에서 1초로 줄였다**(2026-09-12).
        ///
        /// 2초가 왜 틀렸나: 발사 모션 잠금은 0.42초(<c>MousePopgun.SHOOT_ANIM_SECONDS</c>)이고
        /// 그 뒤 0.15초 크로스페이드로 <c>Idle_A</c>로 돌아가므로, 마지막 발로부터
        /// **약 0.57초면 쥐는 이미 네발로 서 있다.** 그 자세의 오른 앞다리는 곧게 아래를
        /// 향하는데 총은 **쏘는 포즈에 맞춰** 앞발에 구워져 있다(<c>MouseGunBaker</c>는
        /// 발사 순간의 본 회전을 역산해 총구를 눕힌다). 그래서 남은 시간 내내 총이
        /// **바닥을 겨누고 정강이에 묶인 것처럼** 보였다 — 2초 + 페이드아웃 0.3초면
        /// 그 그림이 **1.7초**나 간다. 플레이모드에서 실제로 그렇게 보였다.
        ///
        /// 왜 1초인가 — **가장 느린 무기의 쿨다운보다 길어야 한다.** `WeaponInfo.csv`가
        /// 티어 0·1을 0.8초, 티어 2·3을 0.15초로 준다. 0.15초(연사)만 덮으면 되는 게
        /// 아니라 0.8초도 덮어야 한다: 안 그러면 딱총·새총으로 계속 쏘는 동안 매 발
        /// 사이에 총을 넣었다 뽑는다. 그래서 **0.8초보다 커야** 하고,
        /// **0.8초 정확히는 최악의 값이다** — 수납 타이머와 다음 발이 같은 순간에 걸려
        /// 어느 쪽이 이길지 판마다 달라진다(총이 무작위로 깜빡인다).
        /// 1초면 0.2초(25%)의 여유가 남고, 「바닥을 겨누는」 구간은 1.7초 → 0.73초로 준다.
        ///
        /// CSV로 안 내린다 — 밸런스가 아니라 이 두 연출 상수(0.42·0.15)와
        /// 무기 쿨다운 사이의 **관계**에서 나온 값이다. 쿨다운을 바꾸면 여기도 같이 본다.
        /// </summary>
        private const float HOLSTER_DELAY_SECONDS = 1f;

        // URP Lit은 `_BaseColor`, 빌트인·Sprites 계열은 `_Color`를 본다. 둘 다 밀어 넣는다 —
        // 셰이더에 없는 이름을 프로퍼티 블록에 넣는 건 그냥 무시되므로 해가 없다.
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        [SerializeField] private GameObject m_Gun;          // 앞발(leg.F.R)에 든 총
        [SerializeField] private GameObject m_HolsteredGun; // 허리춤(body)에 얹힌 총

        /// <summary>순수 C# 객체라 `?.`를 써도 된다(규칙 9 예외).</summary>
        private CancellationTokenSource m_HolsterCts;
        private CancellationTokenSource m_DrawCts;

        /// <summary>
        /// 프로퍼티 블록도 **순수 C#**이다(<c>UnityEngine.Object</c> 파생이 아니다) —
        /// 규칙 9의 대상이 아니고, 한 장을 계속 돌려 쓴다.
        /// </summary>
        private MaterialPropertyBlock m_Block;

        private Renderer m_HeldRenderer;
        private Renderer m_HolsteredRenderer;

        // 머티리얼 원래 색. 알파만 갈아 끼우려면 RGB를 알아야 한다 — 흰색으로 덮으면 총이 하얘진다.
        private Color m_HeldBaseColor = Color.white;
        private Color m_HolsteredBaseColor = Color.white;

        private float m_HolsteredAlpha;
        private float m_HeldAlpha;

        /// <summary>뽑는 중 — 페이드인·바꿔치기가 아직 도는 중이다.</summary>
        private bool m_Drawing;

        /// <summary>넣는 중 — 든 총이 사라지는 페이드아웃을 타고 있다.</summary>
        private bool m_Holstering;

        /// <summary>
        /// 총이 나와 있나. **뽑는 중도 포함한다** — 뽑기가 도는 0.2초 사이에 다음 발이
        /// 오면(기관총은 0.1초 간격이다) 뽑기를 처음부터 다시 돌리면 안 되기 때문이다.
        /// 「무장 상태인가」로 읽으면 맞다.
        /// </summary>
        public bool IsOut => m_Drawing || (m_Gun != null && m_Gun.activeSelf);

        /// <summary>허리춤 총이 지금 보이나 — QA·미리보기가 「바꿔치기가 실제로 돌았나」를 묻는 자리.</summary>
        public bool IsHolsteredShowing => m_HolsteredGun != null && m_HolsteredGun.activeSelf;

        /// <summary>지금 든 총의 알파(0~1) — QA가 「반투명인 채로 굳지 않았나」를 묻는 자리.</summary>
        public float HeldAlpha => m_HeldAlpha;

        /// <summary>지금 허리춤 총의 알파(0~1).</summary>
        public float HolsteredAlpha => m_HolsteredAlpha;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_HeldRenderer = CacheRenderer(m_Gun, ref m_HeldBaseColor);
            m_HolsteredRenderer = CacheRenderer(m_HolsteredGun, ref m_HolsteredBaseColor);

            // 프리팹에 둘 다 비활성으로 굽지만, 누가 씬에서 켜 놨더라도 시작은 항상 숨김이다.
            Apply(0f, 0f);
        }

        /// <summary>
        /// 한 발 쐈다 — 총을 보이게 하고 수납 카운트다운을 **다시** 건다.
        /// 처음 쏘는 것이면 허리춤부터 스르르 띄우고 <see cref="MouseGunVisualLogic.DRAW_SWAP_SECONDS"/>
        /// 뒤에 손으로 크로스페이드한다.
        /// </summary>
        /// <returns>
        /// 부르기 **직전에** 이미 총이 나와 있었나(뽑는 중 포함). 호출부는 이 값으로 클립을 고른다
        /// (<see cref="MouseGunVisualLogic.ClipForShot"/>) — 나와 있었으면 반동만, 아니면 뽑기부터.
        /// </returns>
        public bool ShowForShot()
        {
            bool wasOut = IsOut;

            // 뽑는 중에 다음 발이 와도 뽑기를 다시 걸지 않는다 — 예약된 바꿔치기가 그대로 끝난다.
            if (!wasOut)
            {
                StartDraw();
            }
            else if (m_Holstering)
            {
                // 넣는 도중에 다음 발이 왔다. **여기서 되돌리지 않으면 총이 반투명인 채로 굳는다** —
                // 아래 RestartHolster는 페이드아웃을 끊기만 하고 알파를 되돌리지는 않는다.
                // 페이드로 되돌리지 않고 즉시 또렷하게 만드는 이유: 이 프레임에 반동 클립이 시작되므로
                // 총이 그때 이미 손에 또렷이 있어야 한다. 0.6 → 1은 눈에 「다시 그러쥐었다」로 읽힌다.
                CancelHolster();
                Apply(0f, 1f);
            }

            RestartHolster();
            return wasOut;
        }

        /// <summary>수납을 즉시 — 죽음·역할 변경처럼 "손에 있을 리 없는" 상황용. 페이드 없이 끊는다.</summary>
        public void HideNow()
        {
            CancelDraw();
            CancelHolster();
            Apply(0f, 0f);
        }

        private void OnDisable()
        {
            CancelDraw();
            CancelHolster();
        }

        /// <summary>
        /// 총 오브젝트의 렌더러와 **원래 색**을 한 번만 캐싱한다. 색을 여기서 읽어 두는 이유:
        /// 알파만 바꿔야 하는데 프로퍼티 블록은 색 한 덩어리로 덮으므로 RGB를 알아야 한다.
        /// </summary>
        private Renderer CacheRenderer(GameObject _gun, ref Color _baseColor)
        {
            if (_gun == null) return null;

            // 꺼진 채로 구워져 있으므로 비활성 포함으로 찾는다.
            Renderer renderer = _gun.GetComponentInChildren<Renderer>(true);
            if (renderer == null) return null;

            // sharedMaterial이다 — `material`은 사본을 만들어 샌다.
            Material material = renderer.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty(BASE_COLOR_ID)) _baseColor = material.GetColor(BASE_COLOR_ID);
                else if (material.HasProperty(COLOR_ID)) _baseColor = material.GetColor(COLOR_ID);
            }
            return renderer;
        }

        /// <summary>
        /// 둘의 알파를 **한 군데서만** 바꾼다. 따로따로 켜고 끄면 「둘 다 보임」이나
        /// 「둘 다 사라짐」이 조용히 생긴다 — 여기로 모으면 그게 구조적으로 불가능하다.
        /// </summary>
        private void Apply(float _holsteredAlpha, float _heldAlpha)
        {
            m_HolsteredAlpha = _holsteredAlpha;
            m_HeldAlpha = _heldAlpha;

            Push(m_HolsteredGun, m_HolsteredRenderer, m_HolsteredBaseColor, _holsteredAlpha);
            Push(m_Gun, m_HeldRenderer, m_HeldBaseColor, _heldAlpha);
        }

        /// <summary>
        /// 알파를 렌더러에 밀어 넣고, 완전히 투명하면 오브젝트를 끈다 —
        /// 안 보이는 총이 드로우콜과 그림자를 먹을 이유가 없다.
        /// **알파를 먼저 넣고 켠다**: 켠 다음에 넣으면 직전 알파로 한 프레임 그려진다.
        /// </summary>
        private void Push(GameObject _gun, Renderer _renderer, Color _baseColor, float _alpha)
        {
            bool render = GunFadeLogic.ShouldRender(_alpha);

            if (_renderer != null && m_Block != null && render)
            {
                _renderer.GetPropertyBlock(m_Block);
                Color tint = _baseColor;
                tint.a = _alpha;
                m_Block.SetColor(BASE_COLOR_ID, tint);
                m_Block.SetColor(COLOR_ID, tint);
                _renderer.SetPropertyBlock(m_Block);
            }

            if (_gun != null && _gun.activeSelf != render) _gun.SetActive(render);
        }

        private void StartDraw()
        {
            CancelDraw();
            m_Drawing = true;
            Apply(0f, 0f); // 0에서 자란다 — 이 순간 둘 다 꺼져 있고 IsOut은 m_Drawing이 떠받친다

            m_DrawCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            DrawAsync(m_DrawCts.Token).Forget();
        }

        /// <summary>
        /// 뽑기 전체 — 허리춤 페이드인 → 바꿔치기 시각까지 대기 → 크로스페이드.
        /// <see cref="m_Drawing"/>은 **크로스페이드가 끝날 때까지** 참으로 둔다:
        /// 크로스페이드 첫 프레임의 든 총 알파가 0이라 오브젝트가 아직 꺼져 있는데,
        /// 그 한 프레임에 다음 발이 오면 뽑기를 처음부터 다시 돌게 된다.
        /// </summary>
        private async UniTaskVoid DrawAsync(CancellationToken _token)
        {
            try
            {
                await FadeAsync(0f, 0f, 1f, 0f, GunFadeLogic.DRAW_FADE_IN_SECONDS, _token);

                // 페이드인(0.10초)이 바꿔치기(3/24 ≈ 0.125초)보다 짧다 — 다 나타난 뒤 잠깐 머문다.
                float rest = MouseGunVisualLogic.DRAW_SWAP_SECONDS - GunFadeLogic.DRAW_FADE_IN_SECONDS;
                if (rest > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(rest), cancellationToken: _token);
                }

                await CrossfadeAsync(_token);
            }
            catch (OperationCanceledException)
            {
                // 취소한 쪽(수납·비활성·파괴)이 이미 알파를 정했다 — 여기서 손대면 덮어쓴다.
                return;
            }

            m_Drawing = false;
            if (this != null) Apply(0f, 1f); // 바꿔치기 완료 — 이 순간이 곧 '뽑았다'
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
            m_Holstering = false;
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

                // 수납 시각이 뽑기보다 늦다(1초 > 0.2초)지만, 그 사이에 비활성·재활성이 끼면
                // 뽑기가 아직 살아 있을 수 있다 — 끊고 나서 넣는다.
                CancelDraw();
                m_Holstering = true;

                // **지금 알파에서** 0으로 내린다. 1에서 시작하면 뽑는 도중에 끼어들었을 때
                // 총이 한 번 진해졌다가 사라져 깜빡인다. 허리춤도 같이 내리는 이유:
                // 크로스페이드 도중에 끊겼다면 거기 반쯤 남아 있어 그대로 굳는다.
                await FadeAsync(m_HolsteredAlpha, m_HeldAlpha, 0f, 0f,
                    GunFadeLogic.HOLSTER_FADE_OUT_SECONDS, _token);
            }
            catch (OperationCanceledException)
            {
                // 다음 발이 카운트다운을 다시 걸었다 — 여기서 숨기면 연사 중에 총이 깜빡인다.
                return;
            }

            m_Holstering = false;

            // 파괴 중일 수 있다. Unity 객체는 `!= null`로만 검사한다(규칙 9).
            if (this != null) Apply(0f, 0f);
        }

        /// <summary>
        /// 두 알파를 각자의 시작값에서 목표값으로 민다. **첫 프레임을 기다리지 않고 바로 찍는다** —
        /// 한 프레임 늦으면 직전 상태가 그대로 한 번 더 그려진다.
        /// </summary>
        private async UniTask FadeAsync(
            float _fromHolstered, float _fromHeld, float _toHolstered, float _toHeld,
            float _seconds, CancellationToken _token)
        {
            float elapsed = 0f;
            while (true)
            {
                Apply(
                    GunFadeLogic.AlphaBetween(elapsed, _seconds, _fromHolstered, _toHolstered),
                    GunFadeLogic.AlphaBetween(elapsed, _seconds, _fromHeld, _toHeld));

                if (GunFadeLogic.IsComplete(elapsed, _seconds)) return;

                await UniTask.Yield(PlayerLoopTiming.Update, _token);
                elapsed += Time.deltaTime;
            }
        }

        /// <summary>
        /// 바꿔치기. <see cref="GunFadeLogic.CrossfadeAlphas"/>가 **합이 1인 한 쌍**을 주므로
        /// 「둘 다 진해져 총이 둘로 보이는」 순간도 「둘 다 옅어져 총이 사라지는」 순간도 없다.
        /// 그 불변식이 여기서 나눠 계산되지 않게 함수 하나로 받는다.
        /// </summary>
        private async UniTask CrossfadeAsync(CancellationToken _token)
        {
            float elapsed = 0f;
            while (true)
            {
                GunFadeLogic.CrossfadeAlphas(elapsed, GunFadeLogic.SWAP_CROSSFADE_SECONDS,
                    out float outgoing, out float incoming);
                Apply(outgoing, incoming);

                if (GunFadeLogic.IsComplete(elapsed, GunFadeLogic.SWAP_CROSSFADE_SECONDS)) return;

                await UniTask.Yield(PlayerLoopTiming.Update, _token);
                elapsed += Time.deltaTime;
            }
        }
    }
}
