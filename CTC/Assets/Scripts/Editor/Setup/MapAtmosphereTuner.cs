using System.IO;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CTC.Editor
{
    /// <summary>
    /// 분위기 수치를 **플레이를 멈추지 않고** 맞춘다 — Tools > CTC > Dev > Map Atmosphere 수치 적용 (2026-09-14).
    ///
    /// 왜 있나: 색·밝기는 눈으로 여러 번 맞춰야 하는데, 생성기 상수를 고치면 컴파일 → 플레이 종료 → 매치 재세팅(MPPM 3인)이
    /// 매번 돈다. 여기서는 `%TEMP%/ctc-atmo.json`을 읽어 **후보정 프로필·머티리얼 에셋과 지금 씬의 광원**에 바로 쓴다.
    /// 에셋에 쓰므로 맞춘 값은 남는다. 확정되면 <see cref="MapAtmosphereBuilder"/> 상수에도 옮겨 적는다(재생성하면 덮인다).
    /// </summary>
    public static class MapAtmosphereTuner
    {
        [System.Serializable]
        private class Values
        {
            public float exposure = 0.55f;
            public float contrast = 14f;
            public float saturation = 8f;
            public Color splitShadows = new Color(0.36f, 0.46f, 0.78f);
            public Color splitHighlights = new Color(1f, 0.76f, 0.46f);
            public float splitBalance = -15f;
            public float bloomIntensity = 0.9f;
            public float bloomThreshold = 1f;
            public float vignette = 0.3f;
            public float lampIntensity = 7f;
            public float coneIntensity = 0.32f;
            public float moonConeIntensity = 0.2f;
            public float skyExposure = 0.22f;
            public float sunIntensity = -1f;       // <0이면 안 건드림
            public Color ambient = new Color(-1f, 0f, 0f);   // r<0이면 안 건드림
            public float fogDensity = -1f;
        }

        public static string JsonPath => Path.Combine(Path.GetTempPath(), "ctc-atmo.json");

        [MenuItem("Tools/CTC/Dev/Map Atmosphere 수치 적용 (ctc-atmo.json)")]
        public static void Apply()
        {
            if (!File.Exists(JsonPath))
            {
                File.WriteAllText(JsonPath, JsonUtility.ToJson(new Values(), true));
                Debug.Log($"[AtmoTuner] 기본값을 적었다 — 고친 뒤 다시 돌려라: {JsonPath}");
                return;
            }

            var v = JsonUtility.FromJson<Values>(File.ReadAllText(JsonPath));
            var profile = Resources.Load<MapAtmosphereProfile>(MapAtmosphereProfile.RESOURCE_PATH);
            if (profile == null)
            {
                Debug.LogError("[AtmoTuner] 프로필이 없다 — Build Atmosphere부터");
                return;
            }

            VolumeProfile post = profile.PostProfile;
            if (post != null)
            {
                if (post.TryGet(out ColorAdjustments color))
                {
                    color.postExposure.Override(v.exposure);
                    color.contrast.Override(v.contrast);
                    color.saturation.Override(v.saturation);
                }
                if (post.TryGet(out SplitToning split))
                {
                    split.shadows.Override(v.splitShadows);
                    split.highlights.Override(v.splitHighlights);
                    split.balance.Override(v.splitBalance);
                }
                if (post.TryGet(out Bloom bloom))
                {
                    bloom.intensity.Override(v.bloomIntensity);
                    bloom.threshold.Override(v.bloomThreshold);
                }
                if (post.TryGet(out Vignette vignette)) vignette.intensity.Override(v.vignette);
                EditorUtility.SetDirty(post);
            }

            SetFloat("Assets/Materials/Atmosphere/M_LightCone_Amber.mat", "_Intensity", v.coneIntensity);
            SetFloat("Assets/Materials/Atmosphere/M_LightCone_Moon.mat", "_Intensity", v.moonConeIntensity);
            SetFloat("Assets/Materials/Atmosphere/M_NightSky.mat", "_Exposure", v.skyExposure);

            var so = new SerializedObject(profile);
            so.FindProperty("m_LampLightIntensity").floatValue = v.lampIntensity;
            if (v.sunIntensity >= 0f) so.FindProperty("m_MoonIntensity").floatValue = v.sunIntensity;
            if (v.ambient.r >= 0f) so.FindProperty("m_Ambient").colorValue = v.ambient;
            so.ApplyModifiedPropertiesWithoutUndo();

            int lamps = 0;
            if (Application.isPlaying)
            {
                foreach (Light light in Object.FindObjectsByType<Light>())
                {
                    if (light.type == LightType.Directional && v.sunIntensity >= 0f) light.intensity = v.sunIntensity;
                    if (!light.name.StartsWith("LightPool_") || light.GetComponentInChildren<LampFlicker>(true) == null) continue;
                    light.intensity = v.lampIntensity;
                    lamps++;
                }
                if (v.ambient.r >= 0f) RenderSettings.ambientLight = v.ambient;
                if (v.fogDensity >= 0f) RenderSettings.fogDensity = v.fogDensity;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AtmoTuner] 적용 — 노출 {v.exposure} · 전등 {lamps}개 = {v.lampIntensity}");
        }

        private static void SetFloat(string _path, string _prop, float _value)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (mat == null) return;
            mat.SetFloat(_prop, _value);
            EditorUtility.SetDirty(mat);
        }
    }
}
