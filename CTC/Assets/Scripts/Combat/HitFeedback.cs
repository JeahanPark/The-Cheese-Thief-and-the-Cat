using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 피격 피드백(전 클라 로컬 연출). Health의 HP 감소를 감지해
    /// Hit 모션(PlayerAnimationDriver 있으면)과 렌더러 적색 플래시를 재생한다.
    /// 플레이어·요새·미끼 공용 — 사망(despawn)은 제외.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        [SerializeField] private float m_FlashSeconds = 0.15f;
        [SerializeField] private Color m_FlashColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float m_HitAnimSeconds = 0.4f;

        // 소리를 대상 타입으로 고르는 게 기본이지만, 벽감처럼 "요새로 들리면 안 되는" 것이 있다.
        // 쥐에게 요새 파괴음은 도망 신호이고 벽감 파괴음은 달려오라는 신호라 정반대다(2026-08-09).
        [SerializeField] private bool m_OverrideSfx;
        [SerializeField] private Audio.SfxId m_HitSfx = Audio.SfxId.FortressHit;
        [SerializeField] private Audio.SfxId m_BreakSfx = Audio.SfxId.FortressBreak;
        [SerializeField] private float m_SfxMaxDistance;   // 0이면 SfxPlayer 기본값
        // 타격음만 따로. 0이면 위 값을 쓴다 — 파괴음과 요구하는 행동이 달라서 갈라 놓는다.
        [SerializeField] private float m_HitSfxMaxDistance;

        private Health m_Health;
        private Renderer[] m_Renderers;
        private MaterialPropertyBlock m_Block;
        private CancellationTokenSource m_FlashCts;

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Block = new MaterialPropertyBlock();
            if (m_Health != null) m_Health.HpChanged += OnHpChanged;
        }

        private void OnDestroy()
        {
            if (m_Health != null) m_Health.HpChanged -= OnHpChanged;
            m_FlashCts?.Cancel();
            m_FlashCts?.Dispose();
        }

        private void OnHpChanged(int _previous, int _current)
        {
            if (_current >= _previous)
            {
                // 회복 틱(요새 거점 +10/s) — 스폰 초기화(0→Max)는 제외
                if (_previous > 0 && _current > _previous && GetComponent<CharacterController>() != null)
                {
                    Audio.SfxPlayer.PlayAt(Audio.SfxId.HealTick, transform.position, 0.5f);
                }
                return;
            }

            bool isPlayer = GetComponent<CharacterController>() != null;
            RoleTag tag = GetComponent<RoleTag>();
            bool isCat = tag != null && tag.Role == Lobby.RoleId.Cat;

            // 사망/파괴 — despawn과 무관하게 소리가 남도록 SfxPlayer가 독립 GO로 재생
            if (_current <= 0)
            {
                PlaySfx(m_OverrideSfx ? m_BreakSfx
                    : !isPlayer ? Audio.SfxId.FortressBreak
                    : isCat ? Audio.SfxId.CatDeath : Audio.SfxId.MouseDeath);
                return;
            }

            PlaySfx(m_OverrideSfx ? m_HitSfx
                : !isPlayer ? Audio.SfxId.FortressHit
                : isCat ? Audio.SfxId.CatHurt : Audio.SfxId.MouseHurt);

            var driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.AnimationStateLogic.Hit, m_HitAnimSeconds);
            }

            RestartFlash();
        }

        /// <summary>
        /// 들리는 거리는 정보 설계다. **타격음과 파괴음은 요구하는 행동이 다르다** —
        /// 타격음은 "누가 뭘 부수는 중"(20m)이고 파괴음은 "지금 달려와"(25m)다.
        /// 거리 필드가 하나뿐이라 벽감 타격음이 파괴음 거리를 쓰고 있었다(2026-08-10 발견).
        /// </summary>
        private void PlaySfx(Audio.SfxId _id)
        {
            bool isHit = m_OverrideSfx ? _id == m_HitSfx : _id == Audio.SfxId.FortressHit;
            float max = isHit && m_HitSfxMaxDistance > 0f ? m_HitSfxMaxDistance : m_SfxMaxDistance;

            if (max > 0f) Audio.SfxPlayer.PlayAt(_id, transform.position, 1f, max);
            else Audio.SfxPlayer.PlayAt(_id, transform.position);
        }

        // 연타로 맞으면 이전 플래시를 끊고 다시 시작한다(코루틴 StopCoroutine과 같은 의미).
        private void RestartFlash()
        {
            m_FlashCts?.Cancel();
            m_FlashCts?.Dispose();
            m_FlashCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            FlashAsync(m_FlashCts.Token).Forget();
        }

        private async UniTaskVoid FlashAsync(CancellationToken _token)
        {
            SetTint(true);
            // 취소(연타·파괴)되면 여기서 빠져나가고 아래 복구는 finally가 맡는다
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(m_FlashSeconds), cancellationToken: _token);
            }
            finally
            {
                if (this != null) SetTint(false);
            }
        }

        // MaterialPropertyBlock — 머티리얼 인스턴스화 없이 렌더러 단위 틴트 (URP _BaseColor/레거시 _Color 겸용)
        private void SetTint(bool _on)
        {
            foreach (Renderer r in m_Renderers)
            {
                if (r == null) continue;

                m_Block.Clear();
                if (_on)
                {
                    m_Block.SetColor(BASE_COLOR_ID, m_FlashColor);
                    m_Block.SetColor(COLOR_ID, m_FlashColor);
                }
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
