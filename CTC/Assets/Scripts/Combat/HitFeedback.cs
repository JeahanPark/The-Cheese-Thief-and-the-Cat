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
                Audio.SfxPlayer.PlayAt(
                    !isPlayer ? Audio.SfxId.FortressBreak
                    : isCat ? Audio.SfxId.CatDeath : Audio.SfxId.MouseDeath,
                    transform.position);
                return;
            }

            Audio.SfxPlayer.PlayAt(
                !isPlayer ? Audio.SfxId.FortressHit
                : isCat ? Audio.SfxId.CatHurt : Audio.SfxId.MouseHurt,
                transform.position);

            var driver = GetComponent<Player.PlayerAnimationDriver>();
            if (driver != null)
            {
                driver.PlayOneShot(Player.AnimationStateLogic.Hit, m_HitAnimSeconds);
            }

            RestartFlash();
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
