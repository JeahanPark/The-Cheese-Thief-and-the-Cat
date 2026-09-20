// TestManager의 일부 — 그림 진단 (2026-09-19)
// 개발 빌드에도 실린다 — 빌드에서만 깨지는 그림(마젠타)은 에디터로 재현이 안 된다. 배포 빌드에선 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>
        /// 경로(루트/이름)에 그 글자가 든 렌더러 전부의 머티리얼·셰이더·지원 여부 — 빌드에서만 마젠타로 뜨는 원인을 캔다.
        /// "RoomMouse(Clone)/Mesh_LOD0 [SkinnedMeshRenderer vis] M_Mouse_URP > Shader Graphs/SoftSurfaceGraph ok passes=6 | …".
        /// 빈 글자면 전부.
        /// </summary>
        public string TestDumpRenderers(string _nameContains)
        {
            var sb = new StringBuilder();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r == null) continue;
                string path = $"{r.transform.root.name}/{r.name}";
                if (!string.IsNullOrEmpty(_nameContains)
                    && path.IndexOf(_nameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

                sb.Append(path).Append(" [").Append(r.GetType().Name)
                  .Append(r.enabled ? "" : " off").Append(r.gameObject.activeInHierarchy ? "" : " inactive")
                  .Append(r.isVisible ? " vis" : "").Append("] ");
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) { sb.Append("NULLMAT; "); continue; }
                    Shader s = m.shader;
                    sb.Append(m.name).Append(" > ").Append(s != null ? s.name : "NULLSHADER")
                      .Append(s != null && !s.isSupported ? " UNSUPPORTED" : "")
                      .Append(" passes=").Append(m.passCount)
                      .Append(" kw=").Append(string.Join(",", m.shaderKeywords)).Append("; ");
                }
                sb.Append("| ");
            }
            return sb.ToString();
        }
    }
}
#endif
