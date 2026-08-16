// 어둠 스킬 (v3.9, 2026-08-16 개편) — 쥐 화면 전체 후처리.
// 픽셀의 깊이로 월드 위치를 되살려 **내 쥐로부터의 거리**에 따라 어두워진다:
// 가까운 것은 그대로, 멀수록 검게(원작 랜턴 느낌). 화면 전체 균일 암전(예전)은 "그냥 필터"였다.
// 강도(_CTC_DarkParams.w)는 스크립트가 0.5초에 걸쳐 올렸다 내린다.
Shader "CTC/Darkness"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "CTC Darkness"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // 순서가 중요하다 — Blit.hlsl은 TEXTURE2D_X 매크로를 URP Core.hlsl에서 받는다
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // 스크립트가 매 프레임 세운다(전역) — 렌더러 피처가 재질을 안 들고 다녀도 되게
            float4 _CTC_DarkCenter;   // xyz = 내 쥐 월드 위치
            float4 _CTC_DarkParams;   // x = 밝은 반경(m) · y = 완전히 어두운 반경(m) · z = 먼 곳 밝기(0~1) · w = 강도(0~1)
            float4 _CTC_DarkTint;     // 어두운 곳 색조(푸른 밤)

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float strength = _CTC_DarkParams.w;
                if (strength <= 0.001) return color;

                float depth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float dist = distance(worldPos, _CTC_DarkCenter.xyz);

                // 하늘(깊이 끝)은 무조건 먼 곳
                #if UNITY_REVERSED_Z
                    if (depth <= 0.0001) dist = 1e6;
                #else
                    if (depth >= 0.9999) dist = 1e6;
                #endif

                float t = smoothstep(_CTC_DarkParams.x, _CTC_DarkParams.y, dist);
                float brightness = lerp(1.0, _CTC_DarkParams.z, t);

                // 곱은 **sRGB(감마) 공간**에서 — 선형에서 3%를 곱하면 눈에는 20%로 남는다(실측).
                // "먼 곳 밝기 3%"가 보이는 밝기 3%가 되게 감마로 갔다가 돌아온다.
                half3 srgb = LinearToSRGB(color.rgb);
                half3 dark = srgb * brightness;
                // 어두운 곳일수록 색조를 섞는다 — 회색이 아니라 밤이 되게
                dark = lerp(dark, dark * _CTC_DarkTint.rgb, t * 0.5);

                color.rgb = SRGBToLinear(lerp(srgb, dark, strength));
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
