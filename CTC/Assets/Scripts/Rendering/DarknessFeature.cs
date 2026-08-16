using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CTC.Runtime.Rendering
{
    /// <summary>
    /// 어둠 스킬의 화면 후처리 (URP 렌더러 피처, 2026-08-16). 카메라 색을 `CTC/Darkness` 셰이더로
    /// 한 번 통과시킨다 — 픽셀 깊이로 월드 위치를 되살려 **내 쥐로부터의 거리**만큼 어둡게.
    ///
    /// 파라미터는 전역 셰이더 값(`_CTC_Dark*`)이라 피처는 재질만 들고, 켜고 끄는 것은
    /// `Strength`(정적)로 한다 — 0이면 패스를 아예 안 넣어 비용이 없다. 값을 세우는 쪽은
    /// `UI/CatSkillView`(보는 쪽 화면이라 각 클라이언트가 제 것만 세운다).
    ///
    /// 렌더러 에셋(PC_Renderer)에 한 번 등록돼 있어야 한다 — `Setup Networking Scene`이 등록한다.
    /// </summary>
    public class DarknessFeature : ScriptableRendererFeature
    {
        private static readonly int CENTER_ID = Shader.PropertyToID("_CTC_DarkCenter");
        private static readonly int PARAMS_ID = Shader.PropertyToID("_CTC_DarkParams");
        private static readonly int TINT_ID = Shader.PropertyToID("_CTC_DarkTint");

        /// <summary>0이면 꺼짐. `CatSkillView`가 0.5초에 걸쳐 올렸다 내린다.</summary>
        public static float Strength { get; private set; }

        [SerializeField] private Material m_Material;

        private DarknessPass m_Pass;

        /// <summary>보는 쪽이 매 프레임 세운다 — 내 쥐 위치·반경·먼 곳 밝기·강도.</summary>
        public static void Apply(Vector3 _center, float _nearMeters, float _farMeters, float _farBrightness,
            float _strength)
        {
            Strength = Mathf.Clamp01(_strength);
            Shader.SetGlobalVector(CENTER_ID, _center);
            Shader.SetGlobalVector(PARAMS_ID, new Vector4(_nearMeters, _farMeters, _farBrightness, Strength));
            Shader.SetGlobalVector(TINT_ID, new Vector4(0.55f, 0.62f, 0.85f, 1f));
        }

        public override void Create()
        {
            m_Pass = new DarknessPass(m_Material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents, // 세계는 다 그려졌고 UI는 아직
            };
            m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            if (m_Material == null || Strength <= 0.001f) return;
            if (_renderingData.cameraData.cameraType != CameraType.Game) return;
            _renderer.EnqueuePass(m_Pass);
        }

        private class DarknessPass : ScriptableRenderPass
        {
            private readonly Material m_Material;

            public DarknessPass(Material _material)
            {
                m_Material = _material;
            }

            public override void RecordRenderGraph(RenderGraph _renderGraph, ContextContainer _frameData)
            {
                var resources = _frameData.Get<UniversalResourceData>();
                if (!resources.cameraColor.IsValid()) return;

                TextureDesc desc = _renderGraph.GetTextureDesc(resources.cameraColor);
                desc.name = "_CTC_DarknessColor";
                desc.clearBuffer = false;
                TextureHandle destination = _renderGraph.CreateTexture(desc);

                var blit = new RenderGraphUtils.BlitMaterialParameters(resources.cameraColor, destination, m_Material, 0);
                _renderGraph.AddBlitPass(blit, "CTC Darkness");

                // 다음 패스(UI 등)가 어두워진 색 위에 그리게 카메라 색을 바꿔 단다
                resources.cameraColor = destination;
            }
        }
    }
}
