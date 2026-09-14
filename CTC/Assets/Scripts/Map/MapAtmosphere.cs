using CTC.Runtime.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// 맵 분위기를 입힌다(2026-09-14) — 밤 하늘 · 전등 · 달빛 기둥 · 전역 후보정.
    /// 재료는 <see cref="MapAtmosphereProfile"/>(Resources), 호출은 <see cref="MapDresser"/>가 빛웅덩이를 놓을 때.
    ///
    /// **밤일 때만 입힌다** — 밤 판정은 <c>RenderSettings.fog</c>다. 씬 생성기가 본 게임에만 안개를 켜고
    /// BotLab(관찰용 낮 조명)은 끄므로, 실험실은 자동으로 빠진다.
    ///
    /// 전부 **비주얼 전용**이다. 빛의 자리·반경은 손대지 않는다(스텔스·봇 동선이 그 위에 서 있다).
    /// </summary>
    public static class MapAtmosphere
    {
        private const string VOLUME_NAME = "MapAtmosphereVolume";

        private static MapAtmosphereProfile s_Profile;
        private static bool s_Loaded;

        private static MapAtmosphereProfile Profile
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Loaded = true;
                    s_Profile = Resources.Load<MapAtmosphereProfile>(MapAtmosphereProfile.RESOURCE_PATH);
                    if (s_Profile == null)
                        GameDebug.LogWarning("[MapAtmosphere] 프로필이 없다 — Tools/CTC/Map/Build Atmosphere를 돌려라");
                }
                return s_Profile;
            }
        }

        public static bool IsNight => RenderSettings.fog;

        /// <summary>프로필(없으면 null) — 천장(<see cref="MapCeiling"/>)처럼 같은 재료 한 벌을 쓰는 곳이 읽는다.</summary>
        public static MapAtmosphereProfile LoadedProfile => Profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Profile = null;
            s_Loaded = false;
        }

        /// <summary>하늘·후보정·카메라 — 맵을 입힐 때마다 불러도 된다(이미 있으면 그대로).</summary>
        public static void ApplyScene()
        {
            MapAtmosphereProfile profile = Profile;
            if (profile == null || !IsNight) return;

            if (profile.NightSkybox != null && RenderSettings.skybox != profile.NightSkybox)
            {
                RenderSettings.skybox = profile.NightSkybox;
                DynamicGI.UpdateEnvironment();
            }

            if (profile.PostProfile != null && GameObject.Find(VOLUME_NAME) == null)
            {
                var go = new GameObject(VOLUME_NAME);
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = profile.PostProfile;
            }

            // 달빛·환경광 — 씬에 구운 값은 후보정 전 기준이라 어둡다. 판마다 같은 값으로 덮는다(어둠 스킬은 이 값을 기준으로 줄인다).
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in Object.FindObjectsByType<Light>())
                {
                    if (l.type == LightType.Directional) { sun = l; break; }
                }
            }
            if (sun != null) sun.intensity = profile.MoonIntensity;
            RenderSettings.ambientLight = profile.Ambient;

            EnablePostOn(Camera.main);
        }

        /// <summary>
        /// 빛웅덩이 하나에 전등(또는 달빛 기둥)을 단다. 광원은 호출부가 만든 그대로 받아 세기만 맞춘다.
        /// </summary>
        public static void DecorateLight(Light _light, bool _moon, float _radius, int _seed)
        {
            MapAtmosphereProfile profile = Profile;
            if (profile == null || _light == null || !IsNight) return;

            Transform at = _light.transform;
            GameObject prefab = _moon ? profile.MoonShaftPrefab : profile.LampPrefab;
            if (prefab == null) return;

            if (!_moon)
            {
                Vector3 p = at.position;
                at.position = new Vector3(p.x, profile.LampHeight - 0.35f, p.z);   // 전구 바로 아래
                _light.intensity = profile.LampLightIntensity;
            }

            GameObject fx = Object.Instantiate(prefab, at);
            fx.name = prefab.name;
            fx.transform.localPosition = _moon ? Vector3.zero : new Vector3(0f, 0.35f, 0f);
            fx.transform.localRotation = Quaternion.Euler(0f, (_seed & 359), 0f);

            // 빛줄기는 빛 반경에 맞춰 넓힌다 — 원뿔 메시는 바닥 반지름 1 m·높이 1 m 기준이다.
            Transform cone = fx.transform.Find("Cone");
            if (cone != null)
            {
                float height = at.position.y + (_moon ? 0f : 0.2f);
                float spread = Mathf.Clamp(_radius * (_moon ? 0.45f : 0.38f), 1.2f, 6f);
                cone.localScale = new Vector3(spread, height, spread);
            }

            if (!_moon && profile.FlickerOneIn > 0 && ((_seed & 0x7fffffff) % profile.FlickerOneIn) == 0)
            {
                var flicker = fx.GetComponent<LampFlicker>();
                if (flicker != null) flicker.Bind(_light, _seed);
            }
        }

        private static void EnablePostOn(Camera _camera)
        {
            if (_camera == null) return;
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }
    }
}
