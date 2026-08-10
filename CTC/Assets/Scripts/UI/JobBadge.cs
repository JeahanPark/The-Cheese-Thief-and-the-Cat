using CTC.Runtime.Combat;
using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 쥐 머리 위의 편 표식 (2026-08-10) — 쥐 프리팹에 붙는다.
    ///
    /// 2단계는 **쥐끼리 편이 갈리는 판**인데, 그전까지는 화면에 아무 표시가 없어서
    /// 누구를 쏴야 하는지 알 수가 없었다. 이름을 외워 두는 게임이 아니다.
    ///
    /// 깡패 편(보스·부하)만 표시한다 — 나머지 쥐는 "표식이 없는 쪽"이라 자동으로 구분된다.
    /// **고양이에게는 안 보인다.** 편이 갈린 건 쥐들 사정이고, 고양이가 보스를 알아보면
    /// 우선 사냥으로 2단계 자체를 없앨 수 있다.
    /// </summary>
    public class JobBadge : MonoBehaviour
    {
        private const float HEIGHT = 1.15f;
        private const float FONT_SIZE = 4f;
        private static readonly Color COLOR_BOSS = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color COLOR_MINION = new Color(0.95f, 0.65f, 0.35f);

        private MouseJob m_Job;
        private TextMeshPro m_Text;
        private readonly SerialDisposable m_LocalSub = new SerialDisposable();

        private void Awake()
        {
            m_Job = GetComponent<MouseJob>();
            m_LocalSub.AddTo(this);
        }

        private void Start()
        {
            if (m_Job == null) return;

            // 표식은 처음 필요할 때 만든다 — 직업 없는 쥐가 대부분이라 미리 깔 이유가 없다
            m_Job.JobChanged.Subscribe(_ => Redraw()).AddTo(this);
            // 내가 고양이인지 쥐인지에 따라 보임이 갈린다 — 로컬 플레이어가 바뀌면 다시 판단한다
            LocalPlayerBinder.LocalPlayer.Subscribe(_ => Redraw()).AddTo(this);
        }

        private void Redraw()
        {
            bool show = m_Job != null && MouseJobLogic.IsGangSide(m_Job.Value) && LocalIsMouse();
            if (!show)
            {
                if (m_Text != null) m_Text.gameObject.SetActive(false);
                return;
            }

            if (m_Text == null) m_Text = Build();
            m_Text.gameObject.SetActive(true);

            bool boss = m_Job.Value == MouseJobId.Gang;
            m_Text.text = boss ? "깡패 보스" : "부하";
            m_Text.color = boss ? COLOR_BOSS : COLOR_MINION;
        }

        // 관전자(역할 없음)도 본다 — 이미 판을 다 보고 있어서 숨길 이유가 없다.
        private static bool LocalIsMouse()
        {
            NetworkObject local = LocalPlayerBinder.LocalPlayer.CurrentValue;
            if (local == null) return true;

            var tag = local.GetComponent<RoleTag>();
            return tag == null || tag.Role != RoleId.Cat;
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
