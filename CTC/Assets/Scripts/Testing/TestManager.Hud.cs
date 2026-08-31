// TestManager의 일부 — HUD가 **실제로 그린 글자**를 읽는다 (2026-08-10).
//
// Model 값만 보는 QA는 배선 누락을 못 잡는다(2026-08-09 실측: 볼륨은 바뀌는데 소리는 그대로였다).
// 그래서 여기서는 HUD 컴포넌트의 TMP 참조를 타고 들어가 화면에 올라간 문자열을 그대로 가져온다 —
// 참조가 안 꽂혔으면 빈 문자열이 나와 그 자리에서 실패한다.
// 개발 빌드에도 실린다(2026-08-31) — 빌드 E2E QA(wesqa)의 진입점이라서. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.UI;
using TMPro;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>QA: 상단 타이머 문구 — 2단계면 "깡패전 m:ss".</summary>
        public string TestGetTimerText() => HudText(FindFirstObjectByType<TimerHud>(), "m_Text");

        /// <summary>QA: 내 직업 HUD 문구(안 뜨면 빈 문자열).</summary>
        public string TestGetJobHudText() => HudText(FindFirstObjectByType<MouseJobHud>(), "m_Text");

        /// <summary>QA: 결과 패널 문구(안 떴으면 빈 문자열) — 승자가 셋인데 둘로 찍던 버그를 막는다.</summary>
        public string TestGetResultText() => HudText(FindFirstObjectByType<ResultHud>(), "m_Text");

        /// <summary>QA: 능력 바 — 슬롯마다 "키:이름:상태:남은초(올림)"를 '|'로 잇는다. 바가 꺼져 있으면 빈 문자열.</summary>
        public string TestGetAbilityBar()
        {
            var bar = FindFirstObjectByType<AbilityBar>();
            if (bar == null) return "";
            var views = bar.GetComponentsInChildren<AbilitySlotView>(false);
            var sb = new System.Text.StringBuilder();
            foreach (var v in views)
            {
                if (sb.Length > 0) sb.Append('|');
                sb.Append(v.Key).Append(':').Append(v.Name).Append(':').Append(v.State)
                  .Append(':').Append(Mathf.CeilToInt(v.SecondsShown));
            }
            return sb.ToString();
        }

        /// <summary>
        /// QA: 우상단 상태 클러스터 — "내HP | 재화 | 배지". 관전자는 클러스터가 꺼져 빈 칸이다.
        /// HUD 개편(2026-08-15)으로 구 Wallet/Combat 텍스트 대체. 고양이 HP는 쥐에게 안 보여준다.
        /// </summary>
        public string TestGetStatusText()
        {
            var hud = FindFirstObjectByType<StatusHud>();
            return HudText(hud, "m_MyHpText") + " | " + HudText(hud, "m_WalletText")
                 + " | " + HudText(hud, "m_BadgeText");
        }

        /// <summary>
        /// QA: 지금 이 클라이언트 화면에 켜져 있는 머리 위 편 표식 수.
        /// 고양이 클라에서는 0이어야 한다 — 편이 갈린 건 쥐들 사정이다.
        /// </summary>
        public int TestGetJobBadgeCount()
        {
            int count = 0;
            foreach (JobBadge badge in FindObjectsByType<JobBadge>(FindObjectsSortMode.None))
            {
                var text = badge.GetComponentInChildren<TextMeshPro>(true);
                if (text != null && text.gameObject.activeInHierarchy) count++;
            }
            return count;
        }

        // 컴포넌트가 들고 있는 TMP 참조를 그대로 읽는다 — 이름으로 GameObject를 찾으면
        // 참조가 안 꽂혀 있어도 글자가 보여 "배선됐다"고 착각한다.
        private static string HudText(Component _hud, string _field)
        {
            if (_hud == null) return "";

            FieldInfo field = _hud.GetType().GetField(
                _field, BindingFlags.NonPublic | BindingFlags.Instance);
            var text = field?.GetValue(_hud) as TMP_Text;
            if (text == null || !text.gameObject.activeInHierarchy) return "";
            return text.text;
        }
    }
}
#endif
