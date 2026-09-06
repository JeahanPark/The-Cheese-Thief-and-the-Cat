using CTC.Runtime.Core;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Room
{
    /// <summary>
    /// 고양이 추첨 연출 4.2초 (기획 2026-08-26 화면 4). **보여 주기만 한다** —
    /// 누가 걸렸는지는 이미 서버가 정했고, 여기서는 그 결과를 재생할 뿐이다.
    ///
    /// 전원의 화면에서 같이 돈다. 본인 화면만 문구가 다르다.
    ///
    /// | 시각 | 하는 일 |
    /// |---|---|
    /// | 0.0s | 암전 — UI를 걷고 채움광을 끈다. 참가자와 스포트라이트만 남는다 |
    /// | 0.4s | 스포트라이트가 참가자 사이를 돈다 (easeOut로 감속) |
    /// | 1.8s | 뽑힌 사람 위에서 정지 · 빛이 좁아지고 카드가 커진다 |
    /// | 2.6s | 이름 공개 |
    /// | 3.8s | 검게 페이드 |
    /// | 4.2s | (감독이) 인게임 씬 로드 |
    ///
    /// 마지막 시각은 <see cref="RoomDirector"/>의 대기 시간과 **같아야 한다.** 어긋나면
    /// 연출이 덜 끝난 채 씬이 넘어가거나, 검은 화면이 필요 이상으로 오래 남는다.
    /// </summary>
    public class RoomDrawStage : MonoBehaviour
    {
        public const float TOTAL_SECONDS = 4.2f;

        private const float DIM_SECONDS = 0.4f;
        private const float ROULETTE_SECONDS = 1.4f;   // 0.4 → 1.8
        private const float LAND_SECONDS = 0.8f;       // 1.8 → 2.6
        private const float HOLD_SECONDS = 1.2f;       // 2.6 → 3.8
        private const float FADE_SECONDS = 0.4f;       // 3.8 → 4.2

        /// <summary>돌다가 멈추기까지 최소 몇 바퀴는 돈다 — 한 칸만 움직이면 뽑는 것처럼 안 보인다.</summary>
        private const int ROULETTE_LAPS = 3;

        private const float SPOT_ANGLE_WIDE = 78f;
        private const float SPOT_ANGLE_NARROW = 26f;

        [SerializeField] private RoomSession m_Session;

        [Header("걷어낼 것")]
        [SerializeField] private GameObject m_HudRoot;
        [SerializeField] private Light m_FillLight;

        [Header("무대")]
        [SerializeField] private Light m_Spotlight;
        [SerializeField] private float m_StandRadius = 5f;

        [Header("카드")]
        [SerializeField] private GameObject m_CardRoot;
        [SerializeField] private RectTransform m_Card;
        [SerializeField] private TMP_Text m_CardTitle;
        [SerializeField] private TMP_Text m_CardName;
        [SerializeField] private CanvasGroup m_FadeGroup;

        private bool m_Playing;

        private void Start()
        {
            if (m_CardRoot != null) m_CardRoot.SetActive(false);
            if (m_FadeGroup != null) m_FadeGroup.alpha = 0f;

            if (m_Session == null)
            {
                GameDebug.LogWarning("[Room] 추첨 연출에 RoomSession이 안 붙어 있다");
                return;
            }

            m_Session.CatDrawn.Subscribe(cat => Play(cat).Forget()).AddTo(this);
        }

        private async UniTask Play(ulong _cat)
        {
            if (m_Playing) return;
            m_Playing = true;

            var cancel = this.GetCancellationTokenOnDestroy();

            int count = m_Session.MemberCount;
            int target = m_Session.IndexOf(_cat);
            bool bot = CatDrawLogic.IsBot(_cat);
            if (!bot && (count <= 0 || target < 0))
            {
                // 목록이 흔들렸다 — 연출을 억지로 돌리기보다 결과만 띄운다.
                ShowCard(_cat);
                return;
            }

            Dim();
            await UniTask.Delay(Milli(DIM_SECONDS), cancellationToken: cancel);

            // 봇 고양이는 세워 둔 몸이 없어 비출 자리가 없다(2026-09-06) — 룰렛을 건너뛰고 카드만.
            if (!bot)
            {
                await Roulette(count, target, cancel);
                await Land(target, cancel);
            }
            else await UniTask.Delay(Milli(ROULETTE_SECONDS + LAND_SECONDS), cancellationToken: cancel);

            ShowCard(_cat);
            await UniTask.Delay(Milli(HOLD_SECONDS), cancellationToken: cancel);

            await Fade(cancel);
        }

        /// <summary>UI를 걷고 채움광을 끈다 — 방이 어두워져 스포트라이트만 남는다.</summary>
        private void Dim()
        {
            if (m_HudRoot != null) m_HudRoot.SetActive(false);
            if (m_FillLight != null) m_FillLight.enabled = false;
        }

        /// <summary>
        /// 참가자 사이를 돈다. **정확히 뽑힌 사람에서 멈춘다** — 총 걸음을
        /// `바퀴수 × 인원 + 목표까지의 거리`로 잡아 두면 감속 곡선이 어떻든 착지가 어긋나지 않는다.
        /// </summary>
        private async UniTask Roulette(int _count, int _target, System.Threading.CancellationToken _cancel)
        {
            if (m_Spotlight == null) return;

            int steps = ROULETTE_LAPS * _count + _target;
            float elapsed = 0f;

            while (elapsed < ROULETTE_SECONDS)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ROULETTE_SECONDS);

                int step = Mathf.Min(steps, Mathf.FloorToInt(EaseOutCubic(t) * steps));
                MoveSpotTo(step % _count);

                await UniTask.Yield(_cancel);
            }

            MoveSpotTo(_target);
        }

        /// <summary>빛을 좁혀 한 명만 남긴다. 카드는 이 뒤에 뜬다.</summary>
        private async UniTask Land(int _target, System.Threading.CancellationToken _cancel)
        {
            MoveSpotTo(_target);

            float elapsed = 0f;
            while (elapsed < LAND_SECONDS)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / LAND_SECONDS);

                if (m_Spotlight != null)
                {
                    m_Spotlight.spotAngle = Mathf.Lerp(SPOT_ANGLE_WIDE, SPOT_ANGLE_NARROW, EaseOutCubic(t));
                    m_Spotlight.innerSpotAngle = m_Spotlight.spotAngle * 0.5f;
                }

                await UniTask.Yield(_cancel);
            }
        }

        private void MoveSpotTo(int _slot)
        {
            if (m_Spotlight == null) return;

            Vector3 stand = RoomLogic.StandPosition(_slot, RoomLogic.MAX_PLAYERS, m_StandRadius);
            Transform tr = m_Spotlight.transform;
            tr.position = new Vector3(stand.x, tr.position.y, stand.z);
        }

        /// <summary>본인 화면은 문구가 다르다 — 나머지에게는 누가 걸렸는지만 알린다.</summary>
        private void ShowCard(ulong _cat)
        {
            if (m_CardRoot == null) return;

            var nm = NetworkManager.Singleton;
            bool mine = nm != null && nm.LocalClientId == _cat;

            int idx = m_Session != null ? m_Session.IndexOf(_cat) : -1;
            string who = idx >= 0
                ? RoomLogic.DisplayName(m_Session.GetMember(idx).Name.ToString(), idx)
                : CatDrawLogic.IsBot(_cat) ? "봇 고양이" : "누군가";

            if (m_CardTitle != null)
                m_CardTitle.text = mine ? "고양이는 바로 당신" : "고양이가 정해졌다";

            if (m_CardName != null)
                m_CardName.text = mine ? "쥐들이 창고를 털기 전에 잡아라" : who;

            m_CardRoot.SetActive(true);
            GrowCard().Forget();
        }

        // 카드가 툭 나타나면 결과가 아니라 오류처럼 보인다 — 살짝 커지며 들어온다.
        private async UniTaskVoid GrowCard()
        {
            if (m_Card == null) return;

            var cancel = this.GetCancellationTokenOnDestroy();
            const float GROW_SECONDS = 0.28f;

            float elapsed = 0f;
            while (elapsed < GROW_SECONDS)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / GROW_SECONDS);
                m_Card.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, EaseOutCubic(t));
                await UniTask.Yield(cancel);
            }
            m_Card.localScale = Vector3.one;
        }

        private async UniTask Fade(System.Threading.CancellationToken _cancel)
        {
            if (m_FadeGroup == null) return;

            float elapsed = 0f;
            while (elapsed < FADE_SECONDS)
            {
                elapsed += Time.deltaTime;
                m_FadeGroup.alpha = Mathf.Clamp01(elapsed / FADE_SECONDS);
                await UniTask.Yield(_cancel);
            }
            m_FadeGroup.alpha = 1f;
        }

        private static int Milli(float _seconds) => Mathf.RoundToInt(_seconds * 1000f);

        private static float EaseOutCubic(float _t)
        {
            float inv = 1f - _t;
            return 1f - inv * inv * inv;
        }
    }
}
