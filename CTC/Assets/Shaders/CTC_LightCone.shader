// 전등 빛줄기(2026-09-14 맵 분위기) — 전등 밑으로 퍼지는 가짜 볼류메트릭 원뿔.
// 진짜 볼류메트릭은 비싸고 URP에 기본이 없다. 원뿔 메시 하나를 가산 합성으로 그리고
//   · 위(전등)에서 아래로 갈수록 옅어지고(uv.y)
//   · 가장자리(시선과 면이 비스듬한 곳)에서 사라지게(프레넬 반대) 해서 "빛이 공기에 걸린" 느낌만 낸다.
// 깊이를 쓰지 않는다(ZWrite Off) — 캐릭터가 빛줄기 안에 들어가도 가려지지 않는다.
// SRP Batcher 호환 최소형.
Shader "CTC/LightCone"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1, 0.72, 0.3, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.6
        _EdgePower ("Edge Softness", Range(0.5, 8)) = 2.2
        _TopFade ("Top Fade (0=wide)", Range(0, 1)) = 0.08
        _Shimmer ("Dust Shimmer", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "LightCone"
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _EdgePower;
                half _TopFade;
                half _Shimmer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // uv.y: 0 = 전등(위) · 1 = 바닥(아래)
                half down = saturate(IN.uv.y);
                half fall = (1.0 - down) * (1.0 - down);
                half top = smoothstep(0.0, max(_TopFade, 1e-3), down);

                // 면이 시선을 정면으로 볼수록 진하다 — 원뿔 실루엣 가장자리는 사라진다
                half facing = abs(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                half edge = pow(saturate(facing), _EdgePower);

                // 떠다니는 먼지처럼 아주 약하게 일렁인다(월드 좌표라 전등마다 다르게)
                half t = _Time.y;
                half shimmer = 1.0 + _Shimmer * (sin(IN.positionWS.y * 3.1 + t * 0.9 + IN.positionWS.x) * 0.5
                                               + sin(IN.positionWS.z * 2.3 - t * 0.6) * 0.5);

                half a = fall * top * edge * shimmer * _Intensity;
                half3 col = _BaseColor.rgb * a;
                col = MixFogColor(col, half3(0, 0, 0), IN.fogFactor);   // 가산이라 안개 속에선 0으로 녹인다
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
