#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇이 지금 어디로 가려 하는지를 **게임 뷰에** 그린다(2026-08-18).
    ///
    /// `Debug.DrawLine`은 씬 뷰에만 나와 스크린샷으로 남길 수 없다. `LineRenderer`는 봇마다
    /// 오브젝트를 만들어 관리해야 한다. 그래서 `GL`로 직접 긋는다 — 내장 `Hidden/Internal-Colored`는
    /// 어느 환경에서나 있고, 오브젝트를 하나도 안 만든다.
    ///
    /// 그리는 것: 몸 → 다음 코너 → 그 다음 … → 목표(코너 사슬), 목표 자리에 십자,
    /// 몸에서 원본 목표까지 얇은 직선(길찾기가 돌아가는 정도가 눈에 보이게).
    /// 색: 고양이 주황 · 쥐 하늘 · 목표 흰색.
    /// </summary>
    public class BotPathDebug : MonoBehaviour
    {
        private const float LINE_HEIGHT = 0.35f;
        private const float CROSS_SIZE = 0.6f;

        private static readonly Color CAT_COLOR = new Color(1f, 0.55f, 0.3f, 0.95f);
        private static readonly Color MOUSE_COLOR = new Color(0.45f, 0.75f, 1f, 0.95f);
        private static readonly Color GOAL_COLOR = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color DIRECT_COLOR = new Color(1f, 1f, 1f, 0.18f);

        private Material m_Material;

        private void OnDestroy()
        {
            if (m_Material != null) Destroy(m_Material);
        }

        private void OnRenderObject()
        {
            BotLab lab = BotLab.Instance;
            if (lab != null && !lab.DrawPaths) return;

            TestBotManager bots = TestBotManager.Instance;
            if (bots == null || bots.Count == 0) return;

            EnsureMaterial();
            m_Material.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);

            foreach (BotAgent agent in bots.Agents)
            {
                if (agent == null || !agent.Alive) continue;
                DrawAgent(agent);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void EnsureMaterial()
        {
            if (m_Material != null) return;

            // 내장 컬러 셰이더 — 깊이를 무시해 벽 뒤 경로도 보인다(실험실이니 그게 낫다)
            var shader = Shader.Find("Hidden/Internal-Colored");
            m_Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m_Material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_Material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m_Material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m_Material.SetInt("_ZWrite", 0);
            m_Material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        private static void DrawAgent(BotAgent _agent)
        {
            BotNavigator nav = _agent.Nav;
            if (nav == null || !nav.IsMoving) return;

            Color color = _agent.Role == RoleId.Cat ? CAT_COLOR : MOUSE_COLOR;
            Vector3 from = Lift(_agent.Position);

            // 몸 → 원본 목표: 길찾기가 얼마나 돌아가는지가 이 얇은 선과의 차이로 보인다
            Line(from, Lift(nav.Goal), DIRECT_COLOR);

            for (int i = nav.CornerIndex; i < nav.CornerCount; i++)
            {
                Vector3 to = Lift(nav.CornerAt(i));
                Line(from, to, color);
                from = to;
            }

            Cross(Lift(nav.Goal), GOAL_COLOR);
        }

        private static void Line(Vector3 _a, Vector3 _b, Color _color)
        {
            GL.Color(_color);
            GL.Vertex(_a);
            GL.Vertex(_b);
        }

        private static void Cross(Vector3 _at, Color _color)
        {
            Line(_at + Vector3.left * CROSS_SIZE, _at + Vector3.right * CROSS_SIZE, _color);
            Line(_at + Vector3.back * CROSS_SIZE, _at + Vector3.forward * CROSS_SIZE, _color);
        }

        private static Vector3 Lift(Vector3 _p)
        {
            _p.y += LINE_HEIGHT;
            return _p;
        }
    }
}
#endif
