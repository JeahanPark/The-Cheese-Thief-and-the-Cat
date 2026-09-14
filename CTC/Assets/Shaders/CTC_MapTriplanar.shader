// 맵 박스 전용 트리플래너 — 유니티 큐브는 면당 UV가 0~1이라 120m 벽에 텍스처가
// 한 장 늘어난다(함정 5 계열). 월드 좌표를 UV로 써서 1 UV = _TileMeters 미터로 통일한다.
// SdfMesher.PlanarUvs와 같은 규약이라 프롭과 톤이 맞는다.
Shader "CTC/MapTriplanar"
{
    Properties
    {
        _BaseMap("Albedo (1 tile = _TileMeters)", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _TileMeters("Meters per tile", Float) = 1.0
        _SkirtMeters("Skirt height (0 = off)", Float) = 0.0
        _SkirtColor("Skirt tint", Color) = (0.45,0.36,0.28,1)
        _FillLight("Fill light (0 = off)", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            // Forward+(PC 렌더러) — 클러스터 키워드가 없으면 GetAdditionalLightsCount()가 0을 준다
            #pragma multi_compile_fragment _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _TileMeters;
                float _SkirtMeters;
                float4 _SkirtColor;
                float4 _FillLight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 SampleTriplanar(float3 _posWS, float3 _nWS)
            {
                float3 w = pow(abs(_nWS), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                float t = max(_TileMeters, 0.01);
                half3 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _posWS.zy / t).rgb;
                half3 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _posWS.xz / t).rgb;
                half3 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, _posWS.xy / t).rgb;
                return cx * w.x + cy * w.y + cz * w.z;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half3 albedo = SampleTriplanar(i.positionWS, n) * _BaseColor.rgb;

                // 걸레받이 — 수직면의 지면 근처를 어두운 톤으로. 0이면 꺼짐(경계벽 전용)
                if (_SkirtMeters > 0.0 && abs(n.y) < 0.5 && i.positionWS.y < _SkirtMeters)
                    albedo *= _SkirtColor.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = SampleSH(n);
                lighting += mainLight.color
                    * (saturate(dot(n, mainLight.direction)) * mainLight.shadowAttenuation);

                // 앰버 빛웅덩이(포인트 라이트)가 여기로 들어온다.
                // **Forward+에선 _ADDITIONAL_LIGHTS 키워드가 안 켜진다**(클러스터 키워드만 켜진다) — 이 조건이
                // _ADDITIONAL_LIGHTS 하나뿐이라 PC 렌더러에서 바닥·벽이 점광원을 통째로 못 받았다.
                // 궤짝(URP Lit)만 따뜻하고 바닥은 캄캄했던 이유(2026-09-14 분위기 패스에서 잡힘).
                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
                // LIGHT_LOOP 매크로는 Forward+에선 클러스터를, Forward에선 단순 루프를 편다.
                // 매크로가 inputData를 이름으로 참조하므로 두 필드는 반드시 채운다.
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    lighting += l.color * (saturate(dot(n, l.direction))
                        * l.distanceAttenuation * l.shadowAttenuation);
                LIGHT_LOOP_END
                #endif

                // 채움광 — 천장(2026-09-14)처럼 **빛이 거의 안 닿는 면**이 밤에 완전히 꺼지지 않게 한 겹 얹는다.
                // 밤 환경광(SH)은 아래를 향한 면에 거의 0이라 천장 판·들보가 새까만 구멍으로 보였다. 기본 0이라 바닥·벽은 그대로다.
                lighting += _FillLight.rgb;

                half3 color = MixFog(albedo * lighting, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half4 DepthFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
