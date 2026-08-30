using System;
using CTC.Runtime.Core;
using CTC.Runtime.Match;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 쥐 머리 위의 편 표식 (2026-08-10, 규칙 개편 2026-08-14) — 쥐 프리팹에 붙는다.
    ///
    /// 2단계는 **쥐끼리 편이 갈리는 판**인데, 화면에 표시가 없으면 누구를 쏴야 하는지
    /// 알 수가 없다. 이름을 외워 두는 게임이 아니다.
    ///
    /// 노출 규칙(2026-08-14):
    /// - **깡패팀(보스·부하)에게만 보인다.** 다른 쥐·고양이·관전자는 못 본다 —
    ///   편이 갈린 건 깡패팀의 비밀이고, 관전자는 채팅이 산 쥐에게 가서 누수 경로가 된다.
    /// - **보스 표식은 첫 임명 전까지만.** 임명을 시작한 보스는 숨어서 세를 불린다.
    /// - **부하 표식은 임명 순간 잠깐 팝업**(3초) 후 사라진다 — 팀원끼리 "쟤가 우리 편이 됐다"를
    ///   확인하는 용도지, 상시 네임태그가 아니다.
    /// </summary>
    public class JobBadge : MonoBehaviour
    {
        private const float HEIGHT = 1.15f;
        private const float FONT_SIZE = 4f;
        private const float MINION_POPUP_SECONDS = 3f;
        private static readonly Color COLOR_BOSS = UiPalette.Bad;
        private static readonly Color COLOR_MINION = UiPalette.Warn;

        private MouseJob m_Job;
        private GangRecruit m_Recruit;
        private TextMeshPro m_Text;
        private bool m_MinionPopupExpired;
        private readonly SerialDisposable m_LocalSub = new SerialDisposable();

        private void Awake()
        {
            m_Job = GetComponent<MouseJob>();
            m_Recruit = GetComponent<GangRecruit>();
            m_LocalSub.AddTo(this);
        }

        private void Start()
        {
            if (m_Job == null) return;

            // 표식은 처음 필요할 때 만든다 — 직업 없는 쥐가 대부분이라 미리 깔 이유가 없다
            m_Job.JobChanged.Subscribe(OnJobChanged).AddTo(this);
            if (m_Recruit != null) m_Recruit.HasRecruitedChanged.Subscribe(_ => Redraw()).AddTo(this);

            // 보는 쪽 조건이 "깡패팀인가"라서, 로컬 플레이어가 바뀔 때도
            // **로컬 플레이어의 직업이 바뀔 때도** 다시 판단해야 한다 —
            // 내가 부하로 임명되는 순간 팀원들의 표식이 보이기 시작해야 한다.
            LocalPlayerBinder.LocalPlayer.Subscribe(local =>
            {
                m_LocalSub.Disposable = null;
                if (local != null)
                {
                    var viewerJob = local.GetComponent<MouseJob>();
                    if (viewerJob != null)
                    {
                        m_LocalSub.Disposable = viewerJob.JobChanged.Subscribe(_ => Redraw());
                    }
                }
                Redraw();
            }).AddTo(this);
        }

        private void OnJobChanged(MouseJobId _job)
        {
            if (_job == MouseJobId.Minion)
            {
                m_MinionPopupExpired = false;
                ExpireMinionPopup().Forget();
            }
            Redraw();
        }

        // 팝업은 보는 클라이언트마다 제 시계로 끝난다 — 서버가 끄라고 말해 줄 필요가 없다.
        private async UniTask ExpireMinionPopup()
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(MINION_POPUP_SECONDS),
                cancellationToken: this.GetCancellationTokenOnDestroy());
            m_MinionPopupExpired = true;
            Redraw();
        }

        private void Redraw()
        {
            bool boss = m_Job != null && m_Job.Value == MouseJobId.Gang;
            bool minion = m_Job != null && m_Job.Value == MouseJobId.Minion;

            bool show = ViewerIsGangSide()
                        && (boss
                            ? m_Recruit == null || !m_Recruit.HasRecruited   // 첫 임명 전까지만
                            : minion && !m_MinionPopupExpired);              // 임명 팝업 3초

            if (!show)
            {
                if (m_Text != null) m_Text.gameObject.SetActive(false);
                return;
            }

            if (m_Text == null) m_Text = Build();
            m_Text.gameObject.SetActive(true);
            m_Text.text = boss ? "깡패 보스" : "부하";
            m_Text.color = boss ? COLOR_BOSS : COLOR_MINION;
        }

        // **깡패팀만 본다.** 관전자도 못 본다 — 관전 채팅이 살아있는 쥐에게 가므로
        // 관전자가 보면 그 정보가 비깡패 쥐에게 샌다(qa_v37: 관전자 채팅은 쥐에게 보인다).
        private static bool ViewerIsGangSide()
        {
            NetworkObject local = LocalPlayerBinder.LocalPlayer.CurrentValue;
            if (local == null) return false;

            var job = local.GetComponent<MouseJob>();
            return job != null && job.IsGangSide;
        }

        private TextMeshPro Build()
        {
            var go = new GameObject("JobBadge");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, HEIGHT, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = FONT_SIZE;
            tmp.alignment = TextAlignmentOptions.Center;
            go.AddComponent<Cameras.FaceCamera>();
            return tmp;
        }
    }
}
