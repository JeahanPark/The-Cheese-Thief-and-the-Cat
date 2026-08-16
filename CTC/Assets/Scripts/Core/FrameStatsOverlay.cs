using UnityEngine;

namespace CTC.Runtime.Core
{
    /// <summary>
    /// 좌상단 FPS · 프레임 타임 표시 — **개발용 빌드·에디터에서만**(2026-08-15).
    /// `GameDebug`·`DebugConsoleGate`·봇과 같은 기준(`DEVELOPMENT_BUILD`)이라 배포 빌드에는
    /// 컴포넌트가 붙어 있어도 아무것도 그리지 않는다.
    ///
    /// IMGUI로 그린다 — 캔버스 배선 없이 컴포넌트 하나로 끝나고, ConnectionUI(좌상단 IMGUI)와
    /// 같은 층이라 위치 조율이 쉽다. 값은 지수이동평균(EMA)이라 숫자가 매 프레임 튀지 않는다.
    /// </summary>
    public class FrameStatsOverlay : MonoBehaviour
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // ConnectionUI가 x 10~250을 쓴다 — 그 오른쪽에 붙인다
        private const float LEFT = 260f;
        private const float TOP = 10f;
        private const float SMOOTHING = 0.1f;   // 클수록 최신 프레임을 더 믿는다
        private const float WARN_MS = 20f;      // 50fps 아래로 떨어지면 노랗게 — 60 목표에서 눈에 띄는 선
        private const float BAD_MS = 33.4f;     // 30fps 아래는 빨갛게

        private float m_SmoothedDeltaSeconds;
        private GUIStyle m_Style;

        private void Update()
        {
            // unscaledDeltaTime — 일시정지·슬로모션과 무관하게 실제 렌더 간격을 잰다
            float dt = Time.unscaledDeltaTime;
            m_SmoothedDeltaSeconds = m_SmoothedDeltaSeconds <= 0f
                ? dt
                : Mathf.Lerp(m_SmoothedDeltaSeconds, dt, SMOOTHING);
        }

        private void OnGUI()
        {
            if (m_SmoothedDeltaSeconds <= 0f) return;

            if (m_Style == null)
            {
                m_Style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            }

            float ms = m_SmoothedDeltaSeconds * 1000f;
            float fps = 1f / m_SmoothedDeltaSeconds;
            m_Style.normal.textColor = ms >= BAD_MS ? new Color(1f, 0.35f, 0.3f)
                : ms >= WARN_MS ? new Color(1f, 0.85f, 0.3f)
                : new Color(0.6f, 1f, 0.6f);

            GUI.Label(new Rect(LEFT, TOP, 220f, 24f), $"{fps:0} fps  {ms:0.0} ms", m_Style);
        }
#endif
    }
}
