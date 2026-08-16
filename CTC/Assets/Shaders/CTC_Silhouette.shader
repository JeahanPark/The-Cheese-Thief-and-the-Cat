// 빛 스킬 실루엣 (v3.9) — 벽 너머로 보여야 하므로 깊이 검사를 끄고(ZTest Always) 맨 위(Overlay)에 그린다.
// URP Unlit에는 _ZTest 프로퍼티가 없어(2026-08-16 실측: SetInt("_ZTest")가 무시되고 렌더큐도 2000으로
// 되돌아갔다 → 실루엣이 벽 뒤에 가려져 스킬이 "안 보였다") 전용 셰이더를 둔다.
// SRP Batcher 호환 URP 언릿 최소형. 색은 _BaseColor 하나.
Shader "CTC/Silhouette"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.85, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Silhouette"
            ZTest Always
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
