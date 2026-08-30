# 「밤의 식료품 창고」 맵 아트 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gridbox 프로토타입 맵을 팬트리 테마(트리플래너 셰이더 + 절차 텍스처 + 팩 프리팹)로 교체하고 대형 120×120을 정식 최소 맵으로 승격한다.

**Architecture:** MapBuilder 레이아웃 두뇌는 불변. 재질만 교체(시맨틱 교체)하고, 비주얼 드레싱은 신규 Worker `MapDresser`가 빌드 후 얹는다. 콜라이더·네비그리드·수평 밸런스는 안 움직인다.

**Tech Stack:** Unity 6000.5.2f1 / URP 수제 HLSL 셰이더 / 절차 텍스처(AI 폴백) / Poly Universal Pack(URP판) / CSV 밸런스.

**근거 스펙:** `docs/superpowers/specs/2026-08-31-night-pantry-map-art-design.md`
**도안:** https://claude.ai/code/artifact/d6798b25-1ddc-4490-b4e5-cfb3f55c154b

**실측 전제 (2026-08-31 확인):**
- `MapBuilder` 벽 재질 슬롯은 `m_WallMaterial` 하나 — 경계·방 벽·복도 벽이 나눠 쓴다 → 슬롯 분리 필요
- `PlayerInfo.csv`: Cat 이속 7.8·발소리 12m / Mouse 6.0·14m. `BalanceInfo.csv`: MatchDurationSeconds 720
- Game.unity는 `NewSceneSetup.DefaultGameObjects` — 기본 조명 그대로
- 대형 풀은 이미 있다: `MapCandidatePool_Large.asset` (HalfExtent 60, 49방, 치즈 2세트)
- 카탈로그 배선: `RoomSceneBuilder.cs:277` `SetRefArray(catalog, "m_Layouts", LoadMapLayouts())`
- 셰이더 널 가드 필수(함정 6) — 마젠타가 조용히 구워진다

---

## Phase 1 — 팬트리로 보이게 (스크린샷 병목 해제)

### Task 1: 트리플래너 셰이더

**Files:**
- Create: `Assets/Shaders/CTC_MapTriplanar.shader`
- Test: `Assets/Scripts/Tests/EditMode/MapTriplanarShaderTests.cs`

- [ ] **Step 1: 셰이더 작성**

```hlsl
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
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _TileMeters;
                float _SkirtMeters;
                float4 _SkirtColor;
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

                #if defined(_ADDITIONAL_LIGHTS)   // 4단계의 앰버 빛웅덩이(포인트 라이트)가 여기로 들어온다
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    lighting += l.color * (saturate(dot(n, l.direction))
                        * l.distanceAttenuation * l.shadowAttenuation);
                }
                #endif

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
```

- [ ] **Step 2: 스모크 테스트 작성** — 셰이더가 임포트·컴파일되는지만 본다

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class MapTriplanarShaderTests
    {
        [Test]
        public void Shader_Exists_And_Has_Required_Properties()
        {
            Shader shader = Shader.Find("CTC/MapTriplanar");
            Assert.IsNotNull(shader, "CTC/MapTriplanar 셰이더가 없다 — 임포트 실패거나 이름이 틀렸다");

            var mat = new Material(shader);
            Assert.IsTrue(mat.HasProperty("_BaseMap"));
            Assert.IsTrue(mat.HasProperty("_TileMeters"));
            Assert.IsTrue(mat.HasProperty("_SkirtMeters"));
            Object.DestroyImmediate(mat);
        }
    }
}
```

- [ ] **Step 3: `u_editor_asset(action: refresh)` 후 EditMode 실행** — `Tools/CTC/Run EditMode Tests`, 369 통과 기대(+1). 콘솔에 셰이더 컴파일 에러 없어야 함
- [ ] **Step 4: 커밋** — `feat(map): 맵 박스용 트리플래너 셰이더 — 1 UV = 1m 월드 투영`

### Task 2: 절차 텍스처 로직 (순수, 이음새 없는 널빤지)

AI 텍스처를 기다리지 않는다 — 널빤지는 절차로 굽고, 나중에 같은 파일명의 AI PNG로 덮으면 자동 교체된다(베이커는 파일이 있으면 안 굽는다).

**Files:**
- Create: `Assets/Scripts/Map/PantryTextureLogic.cs` (runtime asm — 테스트 가능하게, RoomLogic 전례)
- Test: `Assets/Scripts/Tests/EditMode/PantryTextureLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트 먼저**

```csharp
using NUnit.Framework;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Tests
{
    public class PantryTextureLogicTests
    {
        [Test]
        public void PeriodicNoise_Wraps_At_Edges()
        {
            for (float v = 0.05f; v < 1f; v += 0.17f)
            {
                float left = PantryTextureLogic.PeriodicNoise(0f, v, 8, 42);
                float right = PantryTextureLogic.PeriodicNoise(1f, v, 8, 42);
                Assert.AreEqual(left, right, 1e-5f, $"v={v}에서 u=0과 u=1이 다르다 — 이음새가 생긴다");
            }
        }

        [Test]
        public void PlankColor_Wraps_Horizontally_And_Vertically()
        {
            var baseC = new Color(0.54f, 0.39f, 0.22f);
            for (float t = 0.03f; t < 1f; t += 0.13f)
            {
                Color l = PantryTextureLogic.PlankColor(0f, t, 8, 7, baseC);
                Color r = PantryTextureLogic.PlankColor(1f, t, 8, 7, baseC);
                Assert.AreEqual(l.r, r.r, 1e-4f);
                Color b = PantryTextureLogic.PlankColor(t, 0f, 8, 7, baseC);
                Color u = PantryTextureLogic.PlankColor(t, 1f, 8, 7, baseC);
                Assert.AreEqual(b.g, u.g, 1e-4f);
            }
        }

        [Test]
        public void PlankColor_Is_Deterministic()
        {
            var baseC = Color.white;
            Color a = PantryTextureLogic.PlankColor(0.37f, 0.61f, 10, 3, baseC);
            Color b = PantryTextureLogic.PlankColor(0.37f, 0.61f, 10, 3, baseC);
            Assert.AreEqual(a, b);
        }
    }
}
```

- [ ] **Step 2: EditMode 실행 → 컴파일 실패 확인** (PantryTextureLogic 없음)
- [ ] **Step 3: 구현**

```csharp
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 이음새 없는 널빤지 텍스처의 **순수 수학**. 에디터 베이커가 PNG로 굽는다.
    /// 모든 함수는 u·v ∈ [0,1]에서 주기적이다 — u=0과 u=1이 같은 값(트리플래너가 타일마다 반복).
    /// AI 텍스처(텍스처 프롬프트.md)가 오면 같은 파일명으로 덮어 교체한다 — 이 코드는 폴백이다.
    /// </summary>
    public static class PantryTextureLogic
    {
        /// <summary>정수 격자 해시 → [0,1). 시드 포함, 플랫폼 무관 결정적.</summary>
        public static float Hash(int _x, int _y, int _seed)
        {
            unchecked
            {
                uint h = (uint)(_x * 374761393 + _y * 668265263 + _seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>주기적 값 노이즈 — 격자를 _period로 감아 u=0과 u=1이 같은 값이 된다.</summary>
        public static float PeriodicNoise(float _u, float _v, int _period, int _seed)
        {
            float x = Mathf.Repeat(_u, 1f) * _period;
            float y = Mathf.Repeat(_v, 1f) * _period;
            int x0 = (int)x, y0 = (int)y;
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x1 = (x0 + 1) % _period, y1 = (y0 + 1) % _period;
            x0 %= _period; y0 %= _period;
            float a = Hash(x0, y0, _seed), b = Hash(x1, y0, _seed);
            float c = Hash(x0, y1, _seed), d = Hash(x1, y1, _seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>
        /// 가로 널빤지 색. _rows줄, 줄마다 이음이 어긋나고 널마다 밝기가 다르다.
        /// 평평한 알베도(그림자·하이라이트 없음) — 조명은 URP가 준다.
        /// </summary>
        public static Color PlankColor(float _u, float _v, int _rows, int _seed, Color _base)
        {
            float u = Mathf.Repeat(_u, 1f);
            float v = Mathf.Repeat(_v, 1f);

            float row = v * _rows;
            int rowIdx = (int)row % _rows;
            float inRow = row - (int)row;

            // 줄마다 세로 이음 위치가 어긋난다 (한 줄에 널 2장).
            // plankIdx는 %2로 감는다 — 안 감으면 u=0과 u=1에서 다른 널이 되어 이음새가 생긴다.
            float offset = Hash(rowIdx, 0, _seed);
            float plankPos = Mathf.Repeat(u * 2f + offset, 1f);
            int plankIdx = ((int)Mathf.Floor(u * 2f + offset)) % 2;

            // 널 판 밝기 + 저대비 결
            float tone = 0.82f + 0.30f * Hash(rowIdx * 7 + plankIdx, 3, _seed);
            float grain = 0.93f + 0.14f * PeriodicNoise(u, v, 24, _seed + 1);

            // 틈 — 널 경계만 살짝 어둡게 (베벨·AO 아님, 선 하나)
            float gap = 1f;
            if (inRow < 0.05f || inRow > 0.95f) gap = 0.72f;
            else if (plankPos < 0.02f || plankPos > 0.98f) gap = 0.78f;

            float k = tone * grain * gap;
            return new Color(_base.r * k, _base.g * k, _base.b * k, 1f);
        }
    }
}
```

- [ ] **Step 4: EditMode 실행 → 3개 테스트 통과 확인** (누계 372)
- [ ] **Step 5: 커밋** — `feat(map): 이음새 없는 널빤지 텍스처 수학 — AI 텍스처 폴백`

### Task 3: 텍스처 베이커 + 맵 머티리얼 생성기 (에디터 메뉴)

**Files:**
- Create: `Assets/Scripts/Editor/Setup/MapAssets.cs`
- Create: `Assets/Scripts/Editor/MapMaterialBaker.cs`
- Modify: `Assets/Scripts/Editor/PropTextureBinder.cs` — `ConfigureImport`를 `internal static`으로 (한 줄)

- [ ] **Step 1: MapAssets — 경로 단일 출처 (PropAssets 전례)**

```csharp
using UnityEditor;
using UnityEngine;

namespace CTC.Editor
{
    /// <summary>맵 테마 에셋의 경로 단일 출처. 굽는 쪽(MapMaterialBaker)과 쓰는 쪽(SceneComposer)이 같이 본다.</summary>
    internal static class MapAssets
    {
        public const string SHADER_NAME = "CTC/MapTriplanar";
        public const string TEX_DIR = "Assets/Materials/Map/Textures";
        public const string MAT_DIR = "Assets/Materials/Map";

        public const string FLOOR_TEX = TEX_DIR + "/T_PantryFloor.png";
        public const string WALL_TEX = TEX_DIR + "/T_PantryWall.png";
        public const string ROOM_FLOOR_TEX = TEX_DIR + "/T_PantryRoomFloor.png";

        public const string GROUND_MAT = MAT_DIR + "/M_MapGround.mat";
        public const string BOUNDARY_MAT = MAT_DIR + "/M_MapBoundary.mat";
        public const string ROOM_FLOOR_MAT = MAT_DIR + "/M_MapRoomFloor.mat";
        public const string ROOM_WALL_MAT = MAT_DIR + "/M_MapRoomWall.mat";
        public const string CORRIDOR_WALL_MAT = MAT_DIR + "/M_MapCorridorWall.mat";

        /// <summary>없으면 null + 경고 — 부르는 쪽이 Gridbox로 되돌아간다(마젠타 방지, 함정 6).</summary>
        public static Material LoadMaterial(string _path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (mat == null)
                Debug.LogWarning($"[MapAssets] 머티리얼 없음(Gridbox로 대체): {_path} — Tools/CTC/Build Map Materials를 먼저 돌려라");
            return mat;
        }
    }
}
```

- [ ] **Step 2: MapMaterialBaker — 메뉴 `Tools/CTC/Build Map Materials (팬트리)`**

굽는 순서: ① PNG가 없으면 `PantryTextureLogic`으로 1024² 베이크 ② 임포트 설정(Repeat·mip·sRGB — `PropTextureBinder.ConfigureImport` 재사용) ③ 셰이더 로드 **널 가드, null이면 중단**(마젠타 금지) ④ 머티리얼 5종 생성/갱신(기존 파일은 프로퍼티만 갱신 — GUID 유지).

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
using CTC.Runtime.Map;

namespace CTC.Editor
{
    /// <summary>
    /// 팬트리 맵 텍스처·머티리얼 생성 — Tools > CTC > Build Map Materials (팬트리).
    /// 텍스처: PNG가 이미 있으면 안 굽는다(AI 텍스처가 덮을 자리). 없으면 절차 널빤지를 굽는다.
    /// 머티리얼: CTC/MapTriplanar 5종. 셰이더가 null이면 즉시 중단 — 마젠타가 구워지는 것(함정 6)보다 실패가 낫다.
    /// </summary>
    public static class MapMaterialBaker
    {
        private const int TEX_SIZE = 1024;

        [MenuItem("Tools/CTC/Build Map Materials (팬트리)")]
        public static void Build()
        {
            EnsureFolders();

            // 널마루 바닥 — 따뜻한 갈색, 굵은 널 6줄(트리플래너 2m 타일 → 널폭 33cm)
            BakeIfMissing(MapAssets.FLOOR_TEX, 6, 11, new Color(0.42f, 0.30f, 0.18f));
            // 판벽 — 한 톤 어둡고 차분하게, 좁은 널 8줄
            BakeIfMissing(MapAssets.WALL_TEX, 8, 23, new Color(0.36f, 0.27f, 0.17f));
            // 방 바닥 — 존 구분용으로 밝고 붉은 기
            BakeIfMissing(MapAssets.ROOM_FLOOR_TEX, 6, 37, new Color(0.48f, 0.33f, 0.20f));

            Shader shader = Shader.Find(MapAssets.SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[MapMaterials] 셰이더 없음: {MapAssets.SHADER_NAME} — 중단한다(마젠타 방지). " +
                               "리임포트 중이면 끝난 뒤 다시 돌려라");
                return;
            }

            var floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAssets.FLOOR_TEX);
            var wallTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAssets.WALL_TEX);
            var roomFloorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapAssets.ROOM_FLOOR_TEX);

            // 타일 2m: 널빤지 텍스처 한 장이 2m를 덮는다 → 널폭 25~33cm, 거대 세계 감각
            Save(MapAssets.GROUND_MAT, shader, floorTex, 2f, Color.white, 0f);
            Save(MapAssets.BOUNDARY_MAT, shader, wallTex, 2f, Color.white, 0.6f);       // 걸레받이 0.6m
            Save(MapAssets.ROOM_FLOOR_MAT, shader, roomFloorTex, 2f, Color.white, 0f);
            Save(MapAssets.ROOM_WALL_MAT, shader, wallTex, 2f, new Color(0.92f, 0.88f, 0.82f), 0f);
            Save(MapAssets.CORRIDOR_WALL_MAT, shader, wallTex, 1f, new Color(0.85f, 0.80f, 0.72f), 0f);

            AssetDatabase.SaveAssets();
            Debug.Log("[MapMaterials] 완료 — 텍스처 3종(있으면 유지) + 머티리얼 5종");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Map"))
                AssetDatabase.CreateFolder("Assets/Materials", "Map");
            if (!AssetDatabase.IsValidFolder(MapAssets.TEX_DIR))
                AssetDatabase.CreateFolder("Assets/Materials/Map", "Textures");
        }

        private static void BakeIfMissing(string _path, int _rows, int _seed, Color _base)
        {
            if (File.Exists(_path)) return;   // AI 텍스처가 이미 자리를 차지했다 — 건드리지 않는다

            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGB24, false);
            var pixels = new Color[TEX_SIZE * TEX_SIZE];
            for (int y = 0; y < TEX_SIZE; y++)
            {
                float v = (float)y / TEX_SIZE;
                for (int x = 0; x < TEX_SIZE; x++)
                    pixels[y * TEX_SIZE + x] = PantryTextureLogic.PlankColor((float)x / TEX_SIZE, v, _rows, _seed, _base);
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(_path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(_path);
            PropTextureBinder.ConfigureImport(_path);   // Repeat·mip·sRGB·aniso — 프롭과 같은 규약
            Debug.Log($"[MapMaterials] 절차 베이크: {_path} (AI 텍스처로 덮으면 자동 교체)");
        }

        private static void Save(string _path, Shader _shader, Texture2D _tex, float _tileMeters, Color _tint, float _skirt)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            bool fresh = mat == null;
            if (fresh) mat = new Material(_shader);
            else mat.shader = _shader;

            mat.SetTexture("_BaseMap", _tex);
            mat.SetColor("_BaseColor", _tint);
            mat.SetFloat("_TileMeters", _tileMeters);
            mat.SetFloat("_SkirtMeters", _skirt);

            if (fresh) AssetDatabase.CreateAsset(mat, _path);
            else EditorUtility.SetDirty(mat);

            if (mat.shader == null)   // 함정 6 이중 검사
                Debug.LogError($"[MapMaterials] {_path} 셰이더가 비었다 — 마젠타로 구워졌을 수 있다");
        }
    }
}
```

- [ ] **Step 3: `PropTextureBinder.ConfigureImport` 접근자 `private` → `internal`**
- [ ] **Step 4: refresh 후 메뉴 실행** — `u_editor_menu`로 `Tools/CTC/Build Map Materials (팬트리)`. 기대 로그: 텍스처 3종 베이크 + 머티리얼 5종
- [ ] **Step 5: EditMode 실행** — 372 유지. 커밋 — `feat(map): 팬트리 텍스처 베이커 + 맵 머티리얼 5종`

### Task 4: MapBuilder 재질 슬롯 분리 + 높이 상수

**Files:**
- Modify: `Assets/Scripts/Map/MapBuilder.cs:17-23` (높이 상수), `:33-38` (슬롯), `:206-220` (경계), `:350-390` (복도)

- [ ] **Step 1: 높이 상수 변경** — 도안 도판 2. 수평 치수는 절대 안 건드린다(콜라이더 높이만 — 네비그리드는 2D라 무관)

```csharp
private const float ROOM_WALL_HEIGHT = 5f;        // 3f → 5f: 가구 절벽 (도안 2026-08-31)
private const float CORRIDOR_WALL_HEIGHT = 2.4f;  // 1.6f → 2.4f: 크레이트 줄
private const float OBSTACLE_HEIGHT = 3f;          // 2.2f → 3f: 배럴
```

- [ ] **Step 2: 재질 슬롯 2개 추가** (기존 필드 아래)

```csharp
// 시맨틱 교체(2026-08-31) — 경계벽·복도 벽은 방 벽과 다른 옷을 입는다.
// 비어 있으면 m_WallMaterial로 되돌아간다(옛 씬 호환).
[SerializeField] private Material m_BoundaryMaterial;
[SerializeField] private Material m_CorridorWallMaterial;

private Material BoundaryMat => m_BoundaryMaterial != null ? m_BoundaryMaterial : m_WallMaterial;
private Material CorridorWallMat => m_CorridorWallMaterial != null ? m_CorridorWallMaterial : m_WallMaterial;
```

- [ ] **Step 3: 사용처 교체** — `BuildGround`의 `Boundary_N/S/E/W` 4곳 `m_WallMaterial` → `BoundaryMat`, 복도 벽 2곳(`CorridorWall_*`, `CorridorEndCap`) → `CorridorWallMat`. `Wall_{r.Id}`(방 벽)와 `Obstacle`은 `m_WallMaterial` 유지
- [ ] **Step 4: 컴파일 확인 + EditMode 372 유지. 커밋** — `feat(map): 벽 재질 슬롯 분리 + 거대 세계 높이(5/2.4/3m)`

### Task 5: SceneComposer 배선 교체 + 씬 재생성

**Files:**
- Modify: `Assets/Scripts/Editor/Setup/SceneComposer.cs:48-53`

- [ ] **Step 1: Gridbox → 팬트리 배선** (등급 원반 3종은 기능색이라 유지)

```csharp
// 시맨틱 교체(2026-08-31) — 팬트리 머티리얼이 없으면 Gridbox로 되돌아간다(마젠타 방지).
Material roomWall = MapAssets.LoadMaterial(MapAssets.ROOM_WALL_MAT);
Material roomFloor = MapAssets.LoadMaterial(MapAssets.ROOM_FLOOR_MAT);
Material ground = MapAssets.LoadMaterial(MapAssets.GROUND_MAT);
Material boundary = MapAssets.LoadMaterial(MapAssets.BOUNDARY_MAT);
Material corridor = MapAssets.LoadMaterial(MapAssets.CORRIDOR_WALL_MAT);
SetRef(mapBuilder, "m_WallMaterial", roomWall != null ? roomWall : GridboxMaterial("Grey2"));
SetRef(mapBuilder, "m_FloorMaterial", roomFloor != null ? roomFloor : GridboxMaterial("Grey4"));
SetRef(mapBuilder, "m_GroundMaterial", ground != null ? ground : GridboxMaterial("Grey1"));
SetRef(mapBuilder, "m_BoundaryMaterial", boundary);
SetRef(mapBuilder, "m_CorridorWallMaterial", corridor);
SetRef(mapBuilder, "m_DiscHighMaterial", GridboxMaterial("Red"));      // 기능색 — 유지
SetRef(mapBuilder, "m_DiscMidMaterial", GridboxMaterial("Orange"));
SetRef(mapBuilder, "m_DiscLowMaterial", GridboxMaterial("Yellow"));
```

- [ ] **Step 2: `Tools/CTC/Setup Networking Scene` 실행** (머티리얼 베이크가 먼저 끝난 상태에서 — 함정 4·6)
- [ ] **Step 3: 육안 확인** — BotLab이 제일 빠르다: `Assets/Scenes/BotLab.unity` 플레이(호스트·맵 자동) → `u_screenshot`. 확인: ① `PROTOTYPE` 글자 없음 ② 널빤지가 박스 크기와 무관하게 이어짐 ③ 마젠타 0 ④ 경계벽 하단 걸레받이 톤
- [ ] **Step 4: EditMode 372 + 커밋** — `feat(map): 맵을 팬트리로 — Gridbox 배선 교체, 씬 재생성`
- [ ] **Step 5: 코드 다이어그램 갱신** (규칙 8) — MapBuilder 슬롯·MapMaterialBaker 추가

**여기까지가 Phase 1 — 스크린샷 병목 해제.**

---

## Phase 2 — 대형 승격 + 밸런스

### Task 6: 카탈로그 필터 로직 (순수)

**Files:**
- Create: `Assets/Scripts/Map/MapCatalogLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/MapCatalogLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트**

```csharp
using NUnit.Framework;
using CTC.Runtime.Map;

namespace CTC.Tests
{
    public class MapCatalogLogicTests
    {
        private static readonly float[] Extents = { 15f, 21f, 60f };   // 기본·확장·대형

        [Test]
        public void Official_Means_Large_Or_Bigger()
        {
            Assert.IsFalse(MapCatalogLogic.IsOfficial(15f));
            Assert.IsFalse(MapCatalogLogic.IsOfficial(21f));
            Assert.IsTrue(MapCatalogLogic.IsOfficial(60f));
        }

        [Test]
        public void Dev_Build_Shows_Everything()
        {
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[0], _devBuild: true));
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[2], _devBuild: true));
        }

        [Test]
        public void Release_Shows_Only_Official()
        {
            Assert.IsFalse(MapCatalogLogic.IsSelectable(Extents[0], _devBuild: false));
            Assert.IsFalse(MapCatalogLogic.IsSelectable(Extents[1], _devBuild: false));
            Assert.IsTrue(MapCatalogLogic.IsSelectable(Extents[2], _devBuild: false));
        }

        [Test]
        public void Default_Index_Is_First_Official()
        {
            Assert.AreEqual(2, MapCatalogLogic.DefaultIndex(Extents));
        }

        [Test]
        public void Default_Index_Falls_Back_To_Zero_When_Nothing_Official()
        {
            Assert.AreEqual(0, MapCatalogLogic.DefaultIndex(new[] { 15f, 21f }));
        }
    }
}
```

- [ ] **Step 2: 실행 → 컴파일 실패 확인**
- [ ] **Step 3: 구현**

```csharp
namespace CTC.Runtime.Map
{
    /// <summary>
    /// 어떤 맵이 정식인가의 순수 판정(2026-08-31 결정) — 대형(HalfExtent 60, 120×120)부터 정식.
    /// 작은 맵은 개발 빌드에서만 고를 수 있다. 레이아웃 배열 자체는 안 건드린다 —
    /// 인덱스가 MatchHandoff를 타고 씬을 넘어가므로 순서가 흔들리면 안 된다.
    /// </summary>
    public static class MapCatalogLogic
    {
        public const float MIN_OFFICIAL_HALF_EXTENT = 60f;

        public static bool IsOfficial(float _halfExtent) => _halfExtent >= MIN_OFFICIAL_HALF_EXTENT;

        public static bool IsSelectable(float _halfExtent, bool _devBuild)
            => _devBuild || IsOfficial(_halfExtent);

        /// <summary>첫 정식 맵의 인덱스. 하나도 없으면 0(개발 상황 보호).</summary>
        public static int DefaultIndex(float[] _halfExtents)
        {
            if (_halfExtents == null) return 0;
            for (int i = 0; i < _halfExtents.Length; i++)
                if (IsOfficial(_halfExtents[i])) return i;
            return 0;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인 (누계 377). 커밋** — `feat(map): 정식 맵 판정 — 대형부터, 작은 맵은 개발 전용`

### Task 7: 카탈로그·대기실 배선

**Files:**
- Modify: `Assets/Scripts/Room/RoomMapCatalog.cs` — `IsSelectableAt`·`DefaultIndex` 추가
- Modify: `Assets/Scripts/Room/RoomMapView.cs` — 선택 불가 맵 버튼 숨김
- Modify: `Assets/Scripts/Room/RoomDirector.cs` — 서버가 방을 열 때 기본 맵 지정

- [ ] **Step 1: RoomMapCatalog에 추가**

```csharp
/// <summary>이 맵을 지금 빌드에서 고를 수 있나 — 정식(대형~)이거나 개발 빌드.</summary>
public bool IsSelectableAt(int _index)
{
    MapLayout layout = LayoutAt(_index);
    return layout != null && MapCatalogLogic.IsSelectable(layout.HalfExtent, Debug.isDebugBuild);
}

/// <summary>새 방의 기본 맵 — 첫 정식 맵.</summary>
public int DefaultIndex()
{
    var extents = new float[Count];
    for (int i = 0; i < Count; i++)
    {
        MapLayout l = LayoutAt(i);
        extents[i] = l != null ? l.HalfExtent : 0f;
    }
    return MapCatalogLogic.DefaultIndex(extents);
}
```

(`LayoutAt`은 private — 이 메서드들은 같은 클래스 안에 넣는다.)

- [ ] **Step 2: RoomMapView** — 버튼 생성/갱신 루프에서 `if (!m_Catalog.IsSelectableAt(i))`면 그 버튼 `SetActive(false)` 후 continue (정확한 루프 위치는 실행 시 그 파일에서 확인)
- [ ] **Step 3: 서버 기본 맵** — RoomDirector의 서버 초기화 지점(`OnNetworkSpawn`의 `IsServer` 분기)에서 `m_Session.ServerSetMapIndex(m_Catalog.DefaultIndex())` 한 번. RoomDirector에 카탈로그 참조가 없으면 `[SerializeField] RoomMapCatalog m_Catalog` 추가 + RoomSceneBuilder에서 SetRef
- [ ] **Step 4: MPPM 실확인** — 새 방 기본이 대형인지, 에디터(dev)에선 3종 다 보이는지 (`Debug.isDebugBuild`가 에디터에서 true인 것까지가 사양)
- [ ] **Step 5: EditMode 377 + 커밋** — `feat(room): 새 방 기본 맵 = 대형 — 작은 맵은 개발 빌드 전용`

### Task 8: 밸런스 CSV 패스

**Files:**
- Modify: `Assets/Resources/CSVInfo/PlayerInfo.csv`
- Modify: `Assets/Resources/CSVInfo/BalanceInfo.csv`

- [ ] **Step 1: 초기값 기입** (120×120 기준 — 실플레이로 재조정 전제, 준선형 상향)

| 값 | 지금 | 변경 |
|---|---|---|
| Cat MoveSpeed | 7.8 | **8.6** |
| Mouse MoveSpeed | 6.0 | **6.6** |
| Cat FootstepRangeMeters / Sneak | 12 / 3 | **20 / 4** |
| Mouse FootstepRangeMeters / Sneak | 14 / 4 | **22 / 6** |
| MatchDurationSeconds | 720 | **900** |

나머지(대시·설치 사거리·요새 HP·수입)는 안 건드린다 — 근접 전투 스케일은 맵 크기와 무관.

- [ ] **Step 2: `Tools/CTC/Convert CSV → Info` 실행**
- [ ] **Step 3: `Tools/CTC/Setup Networking Scene` 재실행** — PlayerInfo는 프리팹에 굽힌다(CLAUDE.md)
- [ ] **Step 4: MPPM 3인 실플레이** (스팀 끄고) — 대형 맵 1판: 고양이가 쥐를 따라잡는가 / 발소리로 존재는 느끼되 정확 위치는 모르는가 / 900s가 지루하지 않은가. 어긋나면 CSV만 고치고 3부터 반복
- [ ] **Step 5: wesqa v36·v37·v38 재확인** — 핸드오프 3번 항목 겸사: `MatchHandoff.FromRoom` false면 옛 흐름 그대로인지
- [ ] **Step 6: 커밋** — `balance(map): 대형 120×120 기준 — 이속·발소리·매치 시간 준선형 상향`

---

## Phase 3 — 가구·소품 (팩 임포트 + MapDresser)

### Task 9: Poly Universal Pack 선별 임포트

**Files:**
- Import: `C:/Users/user/AppData/Roaming/Unity/Asset Store-5.x/polyperfect/3D ModelsProps/Poly Universal Pack.unitypackage`
- Modify: `docs/design-vault/3자 에셋 라이선스.md`, 확인: `Legal/Third-Party Notices.txt`

- [ ] **Step 1: 전체 임포트** — `AssetDatabase.ImportPackage(경로, false)` (비대화형). 임포트가 크니 **끝날 때까지 기다린 뒤** 다음 단계 (함정 6 — 리임포트 중 에셋 생성 금지)
- [ ] **Step 2: 내장 URP 패치 임포트** — `Assets/polyperfect/Poly Universal Pack/Poly Universal Pack - URP.unitypackage`를 같은 방법으로 (기본 머티리얼은 빌트인용 — URP 패치 없이는 마젠타)
- [ ] **Step 3: 불용 카테고리 삭제** — `- Prefabs`·`- Meshes`·`- Animations`·`- Demo Scenes`에서 Cars·Racetrack·Guns·People·Movie Set·Primeval·Christmas·Easter·Halloween·Valentine·4July·Steampunk 삭제(단, Steampunk의 shelf-wall-victorian-a/b 2종은 선반 후보라 남긴다). **유지: Farm·Fantasy·Food·Nature·Survival·Tools·Modular Parts·`- Materials`·`- Textures`** (팩은 아틀라스 공유 — Materials/Textures는 통째로 유지)
- [ ] **Step 4: 육안 확인** — 궤짝·배럴 프리팹 하나를 빈 씬에 놓고 마젠타 아닌지 (`unity-scanner read`로 머티리얼 배선도 확인)
- [ ] **Step 5: 라이선스 기록** — `3자 에셋 라이선스.md`에 polyperfect 행 추가 (Asset Store 표준 EULA, 고지 의무 없음, 소스 재배포 금지)
- [ ] **Step 6: 커밋** (에셋만 별도 커밋 — 용량 큼) — `chore(assets): Poly Universal Pack 선별 임포트 — 팬트리 가구·소품`

### Task 10: 드레싱 로직 (순수)

**Files:**
- Create: `Assets/Scripts/Map/MapDressLogic.cs`
- Test: `Assets/Scripts/Tests/EditMode/MapDressLogicTests.cs`

- [ ] **Step 1: 실패하는 테스트**

```csharp
using NUnit.Framework;
using CTC.Runtime.Map;

namespace CTC.Tests
{
    public class MapDressLogicTests
    {
        [Test]
        public void TileCount_Floors_And_Never_Negative()
        {
            Assert.AreEqual(5, MapDressLogic.TileCount(10f, 2f));
            Assert.AreEqual(4, MapDressLogic.TileCount(9.5f, 2f));
            Assert.AreEqual(0, MapDressLogic.TileCount(1.2f, 2f));
            Assert.AreEqual(0, MapDressLogic.TileCount(-3f, 2f));
        }

        [Test]
        public void TileOffset_Centers_Leftover()
        {
            Assert.AreEqual(0.75f, MapDressLogic.TileOffset(9.5f, 2f, 4), 1e-5f);
            Assert.AreEqual(0f, MapDressLogic.TileOffset(10f, 2f, 5), 1e-5f);
        }

        [Test]
        public void Pick_Is_Deterministic_And_In_Range()
        {
            for (int slot = 0; slot < 50; slot++)
            {
                int a = MapDressLogic.Pick(slot, 7, 1234);
                int b = MapDressLogic.Pick(slot, 7, 1234);
                Assert.AreEqual(a, b);
                Assert.That(a, Is.InRange(0, 6));
            }
        }

        [Test]
        public void Pick_Handles_Zero_Variants()
        {
            Assert.AreEqual(0, MapDressLogic.Pick(3, 0, 99));
        }
    }
}
```

- [ ] **Step 2: 실행 → 실패 확인**
- [ ] **Step 3: 구현**

```csharp
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>벽 세그먼트에 가구를 몇 개, 어디에, 어떤 변종으로 놓는가의 순수 수학. 시드 결정적 — 전 클라이언트 동일.</summary>
    public static class MapDressLogic
    {
        public static int TileCount(float _length, float _unitWidth)
            => _unitWidth <= 0f ? 0 : Mathf.Max(0, Mathf.FloorToInt(_length / _unitWidth));

        /// <summary>자투리를 양끝에 반씩 — 가운데 정렬 시작 오프셋.</summary>
        public static float TileOffset(float _length, float _unitWidth, int _count)
            => (_length - _count * _unitWidth) * 0.5f;

        /// <summary>슬롯 번호 → 변종 인덱스. PantryTextureLogic.Hash 재사용으로 플랫폼 무관.</summary>
        public static int Pick(int _slot, int _variants, int _seed)
        {
            if (_variants <= 0) return 0;
            return (int)(PantryTextureLogic.Hash(_slot, 17, _seed) * _variants) % _variants;
        }
    }
}
```

- [ ] **Step 4: 통과 확인 (누계 381). 커밋** — `feat(map): 드레싱 배치 수학 — 시드 결정적`

### Task 11: MapDresser Worker + 배선

**Files:**
- Create: `Assets/Scripts/Map/MapDresser.cs`
- Modify: `Assets/Scripts/Map/MapBuilder.cs` — `public Transform BuiltRoot => m_Root != null ? m_Root.transform : null;` 추가
- Modify: `Assets/Scripts/Map/MapDirector.cs:73-77` — `RebuildMap()` 끝에 드레싱 호출
- Modify: `Assets/Scripts/Editor/Setup/SceneComposer.cs` — MapDresser 부착 + 프리팹 배선
- Modify: `Assets/Scripts/Editor/Setup/MapAssets.cs` — 팩 프리팹 경로 상수 추가

- [ ] **Step 1: MapDresser** — 이름 접두로 세그먼트를 찾아 비주얼만 얹는다

```csharp
using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 빌드 뒤에 가구 옷을 입히는 Worker(규칙 6) — 콜라이더·네비그리드는 절대 안 건드린다.
    /// 서버·클라이언트 모두 로컬로 돈다(비주얼 전용). 시드 = 맵 인덱스라 전원이 같은 그림을 본다.
    /// 프리팹 배열이 비어 있으면 아무 것도 안 한다 — 팩 미임포트 시 단색 백킹으로 후퇴.
    /// </summary>
    public class MapDresser : MonoBehaviour
    {
        private const float UNIT_WIDTH = 2f;          // 가구 한 칸 폭(m)
        private const string DRESS_ROOT = "MapDress";

        [SerializeField] private MapBuilder m_Builder;
        [SerializeField] private GameObject[] m_ShelfUnits;     // 방 벽 앞 — 선반
        [SerializeField] private GameObject[] m_CrateUnits;     // 복도 벽 — 궤짝
        [SerializeField] private GameObject[] m_ObstacleUnits;  // 장애물 — 배럴·호박·궤짝 (밀가루 포대는 팩에 없다 — 필요해지면 SdfMesher로)
        [SerializeField] private float m_UnitScale = 1f;        // 팩 스케일 보정(사람 크기 → 거대 세계)

        public void Dress(int _seed)
        {
            Transform root = m_Builder != null ? m_Builder.BuiltRoot : null;
            if (root == null) return;

            Transform old = root.Find(DRESS_ROOT);
            if (old != null) Destroy(old.gameObject);

            var dressRoot = new GameObject(DRESS_ROOT).transform;
            dressRoot.SetParent(root, false);

            // 스냅샷 — 루프 중 dressRoot가 자라나므로 먼저 세그먼트 목록을 뜬다
            var segments = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root)
                if (child != dressRoot) segments.Add(child);

            foreach (Transform child in segments)
            {
                string n = child.name;
                if (n.StartsWith("Wall_"))
                    TileAlong(child, m_ShelfUnits, dressRoot, _seed);
                else if (n.StartsWith("CorridorWall") || n.StartsWith("CorridorEndCap"))
                    TileAlong(child, m_CrateUnits, dressRoot, _seed + 1);
                else if (n.StartsWith("Obstacle"))
                    ReplaceVisual(child, m_ObstacleUnits, dressRoot, _seed + 2);
            }
        }

        /// <summary>벽 박스의 긴 축을 따라 가구를 타일링 — 양면에 붙인다.</summary>
        private void TileAlong(Transform _wall, GameObject[] _units, Transform _parent, int _seed)
        {
            if (_units == null || _units.Length == 0) return;

            Vector3 s = _wall.localScale;
            bool xLong = s.x >= s.z;
            float length = xLong ? s.x : s.z;
            float thick = xLong ? s.z : s.x;

            int count = MapDressLogic.TileCount(length, UNIT_WIDTH);
            if (count == 0) return;
            float start = MapDressLogic.TileOffset(length, UNIT_WIDTH, count) - length * 0.5f + UNIT_WIDTH * 0.5f;

            Vector3 axis = _wall.rotation * (xLong ? Vector3.right : Vector3.forward);
            Vector3 face = _wall.rotation * (xLong ? Vector3.forward : Vector3.right);
            int wallKey = Mathf.RoundToInt(_wall.position.x * 7f + _wall.position.z * 13f);

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = _units[MapDressLogic.Pick(i * 31 + wallKey, _units.Length, _seed)];
                if (prefab == null) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pos = _wall.position + axis * (start + i * UNIT_WIDTH)
                                + face * (side * (thick * 0.5f + 0.05f));
                    pos.y = 0f;
                    var go = Instantiate(prefab, pos, Quaternion.LookRotation(face * side), _parent);
                    go.name = $"{prefab.name}_{i}";
                    go.transform.localScale *= m_UnitScale;
                    StripPhysics(go);
                }
            }
        }

        /// <summary>장애물: 박스 렌더러를 끄고 배럴 비주얼로 교체. 콜라이더는 박스가 계속 든다.</summary>
        private void ReplaceVisual(Transform _obstacle, GameObject[] _units, Transform _parent, int _seed)
        {
            if (_units == null || _units.Length == 0) return;

            var renderer = _obstacle.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;

            int key = Mathf.RoundToInt(_obstacle.position.x * 7f + _obstacle.position.z * 13f);
            GameObject prefab = _units[MapDressLogic.Pick(key, _units.Length, _seed)];
            if (prefab == null) return;

            Vector3 pos = _obstacle.position;
            pos.y = 0f;
            float yaw = PantryTextureLogic.Hash(key, 5, _seed) * 360f;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), _parent);
            go.transform.localScale *= m_UnitScale;
            StripPhysics(go);
        }

        /// <summary>팩 프리팹의 콜라이더·리지드바디 제거 — 물리는 원본 박스의 몫.</summary>
        private static void StripPhysics(GameObject _go)
        {
            foreach (var c in _go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in _go.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
        }
    }
}
```

주의: 시드에 `GetInstanceID()`를 쓰지 않는다 — 인스턴스 ID는 클라이언트마다 다르다.
위치 기반 `wallKey`라야 전원이 같은 그림을 본다.

- [ ] **Step 2: MapDirector.RebuildMap 끝에** (규칙 9 — `if` 검사)

```csharp
if (m_Dresser != null) m_Dresser.Dress(m_MapIndex.Value);
```

`[SerializeField] private MapDresser m_Dresser;` 필드 추가.

- [ ] **Step 3: MapAssets에 팩 프리팹 경로 상수** — 임포트 후 실제 경로를 확인해 채운다(예: `Assets/polyperfect/Poly Universal Pack/- Prefabs/Farm/Crates Farm/crate-wood-big.prefab`). 선반 3종·궤짝 3종·배럴/자루 4종
- [ ] **Step 4: SceneComposer** — `mapBuilder` 옆에 `AddComponent<MapDresser>()`, `SetRef(dresser, "m_Builder", mapBuilder)`, `SetRefArray`로 프리팹 3벌 배선, MapDirector에 `SetRef(mapDirector, "m_Dresser", dresser)`
- [ ] **Step 5: 씬 재생성 → BotLab 육안 + `u_screenshot`** — 크기 위화감이 있으면 `m_UnitScale`로 보정 (팩은 사람 스케일 — 2~3배가 거대 세계에 맞을 것)
- [ ] **Step 6: wesqa v36 1회** (MonoBehaviour 재배치 — 필수) + EditMode 381 + 커밋 — `feat(map): MapDresser — 벽 타일링·배럴 교체, 시드 결정적`
- [ ] **Step 7: 코드 다이어그램 갱신** (규칙 8)

---

## Phase 4 — 조명 + 촬영

### Task 12: 밤 조명

**Files:**
- Modify: `Assets/Scripts/Editor/Setup/SceneComposer.cs` — 기본 라이트 교체 + RenderSettings
- Modify: `Assets/Scripts/Map/MapDresser.cs` — 빛웅덩이 포인트 라이트

- [ ] **Step 1: SceneComposer** — `NewSceneSetup.DefaultGameObjects`의 Directional Light를 찾아 재설정 (IntroSceneBuilder 값 참조, 단 맵이 넓어 안개는 옅게)

```csharp
// 밤 조명(2026-08-31) — 인트로와 같은 밤, 다른 밀도. 달빛은 겨우 보일 만큼만.
Light sun = Object.FindFirstObjectByType<Light>();
if (sun != null)
{
    sun.color = new Color(0.55f, 0.65f, 0.85f);   // 딤 블루 달빛
    sun.intensity = 0.35f;
    sun.transform.rotation = Quaternion.Euler(55f, 35f, 0f);
    sun.shadows = LightShadows.Soft;
}
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.09f);
RenderSettings.fog = true;
RenderSettings.fogMode = FogMode.ExponentialSquared;
RenderSettings.fogColor = new Color(0.010f, 0.014f, 0.020f);
RenderSettings.fogDensity = 0.012f;   // 인트로 0.16은 실내 근경용 — 120m 맵에선 12m 앞이 안 보인다
```

- [ ] **Step 2: MapDresser에 빛웅덩이** — `Dress()` 마지막에 방 몇 개당 하나씩 앰버 포인트 라이트. 방 중심 목록이 필요 — `MapBuilder`에 `public IReadOnlyList<Vector3> RoomCenters` 추가(BuildRoom에서 채움). 값: `color #E8B535 (0.91,0.71,0.21), intensity 2.2, range 9f, y=4f`, 3방당 1개(`MapDressLogic.Pick(roomIdx, 3, _seed) == 0`). **URP 어셋의 per-object 라이트 한도(기본 4) 확인** — 부족하면 Additional Lights per pixel 한도를 8로
- [ ] **Step 3: 셰이더 확인** — 트리플래너의 `_ADDITIONAL_LIGHTS` 분기가 웅덩이를 받는지 BotLab 육안
- [ ] **Step 4: 밝기 실플레이** — 어두워서 게임이 안 되면 ambient만 올린다(0.05→0.08). **고양이 어둠 스킬(CatDark*)과의 간섭 확인** — CTC_Darkness가 화면을 더 누르므로 스킬 중 최저 시인성을 본다
- [ ] **Step 5: EditMode + 커밋** — `feat(map): 밤 조명 — 딤 블루 달빛 + 앰버 빛웅덩이`

### Task 13: 스토어 스크린샷

**Files:**
- Create: `Assets/Scripts/Editor/MapPhotoBooth.cs` (임시 카메라 직접 렌더 — 씬뷰 캡처는 갱신 안 됨, CLAUDE.md)
- Output: `docs/store-assets/screenshots/`

- [ ] **Step 1: MapPhotoBooth** — 메뉴 `Tools/CTC/Dev/맵 사진 (1920×1080)`: 플레이 중 임시 카메라를 방 안 3곳 + 부감 1곳에 세워 `RenderTexture`로 렌더 → PNG 저장 (PropShowcase의 렌더 코드 재사용)
- [ ] **Step 2: 4장 촬영** — ① 선반 골목의 쥐 시점 ② 치즈 지점 앞 ③ 크레이트 복도 ④ 부감. `04-ingame-mouse-GREYBOX.png`를 교체하고 GREYBOX 접미 제거
- [ ] **Step 3: `screenshots/README.md` 갱신 + 커밋** — `docs(store): 인게임 스크린샷 — 그레이박스 졸업`

### Task 14: 마무리

- [ ] **치즈 원반 리스킨** (스펙 교체표의 마지막 줄) — `MapBuilder.CreateGradeDisc`가 만드는 원반 위에 자체 쐐기 메시(`PropAssets.CHEESE_MESH` 경로의 런타임판 — `Resources` 이동 없이 SceneComposer가 MapBuilder에 `[SerializeField] Mesh m_CheeseVisualMesh`로 배선)를 등급색 틴트로 얹는다. 원반 색·판정은 그대로 — 보이는 것만
- [ ] EditMode 전체(381) + wesqa v36·v37·v38
- [ ] 개발용 빌드로 대형 맵 FrameStats — 프롭 13k 폴리 감량(핸드오프 4번)이 여기서 필요해지면 별도 작업으로
- [ ] `코드 다이어그램.md` 최종 반영 + `handoff-active.md` 갱신
- [ ] 커밋 — `docs: 맵 아트 완료 — 다이어그램·핸드오프`

---

## 실행 시 주의 (전부 실측된 함정)

1. **머티리얼 베이크는 리임포트가 끝난 뒤** — `new Material(null)`은 조용히 마젠타(함정 6). 팩 임포트 직후가 제일 위험
2. **MPPM 클론이 떠 있으면 MCP 브리지 먹통** — 클론 닫고 `tools/focus-unity.ps1`
3. **스팀 켜져 있으면 MPPM 3인 불가** — 끈다
4. **PlayerInfo CSV 변경 = 씬 재생성 필수**
5. **`TestManager` 타입 분리 금지** — wesqa 전멸
6. 씬 재생성 순서는 `handoff-active.md`의 8단계 그대로 (메시 빌더 → 머티리얼 → Setup Networking → Room → Lobby → Intro)
