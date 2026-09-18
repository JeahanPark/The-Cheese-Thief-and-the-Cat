// 용사 무적 테두리 (2026-09-19, 기획 hero-sword §5-2) — 몸 가장자리가 흰빛으로 빛난다(프레넬 림, 가산).
// 몸 SkinnedMeshRenderer의 머티리얼 칸 **맨 뒤에 잠깐 덧붙여** 같은 메시를 한 번 더 그린다(HeroInvincibleVisual).
// 깊이 검사는 켜 둔다 — 빛 스킬 실루엣(CTC_Silhouette, ZTest Always)과 달리 **벽 너머로 새지 않는다**.
// 세기는 머티리얼 칸별 프로퍼티 블록(_Intensity)으로 민다 — 켜짐·꺼짐 페이드.
Shader "CTC/HeroRim"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.97, 0.85, 1)
        _Power ("Rim Power", Float) = 3.2
        _Intensity ("Intensity", Float) = 1.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "HeroRim"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Power;
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewWS = GetWorldSpaceViewDir(pos.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewWS);
                half rim = pow(1.0h - saturate(dot(n, v)), _Power) * _Intensity;
                return half4(_BaseColor.rgb * rim, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
