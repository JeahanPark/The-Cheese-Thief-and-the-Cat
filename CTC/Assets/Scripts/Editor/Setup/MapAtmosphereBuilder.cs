using System.Collections.Generic;
using System.IO;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CTC.Editor
{
    /// <summary>
    /// 맵 분위기 재료를 코드로 굽는다 — Tools > CTC > Map > Build Atmosphere (2026-09-14).
    /// 메시(갓·줄·빛줄기 원뿔)·머티리얼·먼지 입자·프리팹(전등·달빛 기둥)·밤 하늘·후보정 프로필 →
    /// `Resources/Map/MapAtmosphere.asset` 한 벌. 런타임은 <see cref="MapAtmosphere"/>가 읽는다.
    ///
    /// 전부 생성물이다(손으로 고칠 것이 없다) — 다시 돌리면 덮어쓴다. 값을 바꾸려면 여기 상수를 고치고 다시 돌린다.
    /// 설계: docs/superpowers/specs/2026-09-14-map-atmosphere-design.md
    /// </summary>
    public static class MapAtmosphereBuilder
    {
        private const string MESH_DIR = "Assets/Models/Atmosphere";
        private const string MAT_DIR = "Assets/Materials/Atmosphere";
        private const string FX_DIR = "Assets/Prefabs/Fx/Map";
        private const string RES_DIR = "Assets/Resources/Map";

        private static readonly Color AMBER = new Color(1f, 0.70f, 0.30f);
        private static readonly Color MOON = new Color(0.50f, 0.66f, 1f);
        private static readonly Color BULB_EMISSION = new Color(4f, 2.6f, 0.9f);   // HDR — 블룸 임계(1.0)를 넘어야 번진다

        [MenuItem("Tools/CTC/Map/Build Atmosphere (전등·하늘·후보정)")]
        public static void Build()
        {
            EnsureDir(MESH_DIR);
            EnsureDir(MAT_DIR);
            EnsureDir(FX_DIR);
            EnsureDir(RES_DIR);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Shader cone = Shader.Find("CTC/LightCone");
            Shader sky = Shader.Find("Skybox/Procedural");
            // 함정 6 — 대규모 리임포트 중엔 셰이더가 null로 온다. 마젠타를 굽느니 멈춘다.
            if (lit == null || particle == null || cone == null || sky == null)
            {
                Debug.LogError($"[MapAtmosphere] 셰이더를 못 찾았다 lit={lit} particle={particle} cone={cone} sky={sky} — 리임포트가 끝난 뒤 다시 돌려라");
                return;
            }

            Mesh lampCone = SaveMesh(Frustum("LightCone_Lamp", 0.10f, 1f, 1f, 36, false), "LightCone_Lamp");
            Mesh moonCone = SaveMesh(Frustum("LightCone_Moon", 0.55f, 1f, 1f, 36, false), "LightCone_Moon");
            Mesh shade = SaveMesh(Frustum("LampShade", 0.07f, 0.42f, 0.28f, 28, true), "LampShade");
            Mesh cord = SaveMesh(Frustum("LampCord", 0.012f, 0.012f, 3f, 8, false), "LampCord");

            Material shadeMat = LitMaterial(lit, "M_LampShade", new Color(0.16f, 0.20f, 0.17f), 0.55f, 0.45f, Color.black);
            Material cordMat = LitMaterial(lit, "M_LampCord", new Color(0.03f, 0.03f, 0.03f), 0f, 0.2f, Color.black);
            Material bulbMat = LitMaterial(lit, "M_LampBulb", new Color(1f, 0.92f, 0.75f), 0f, 0.8f, BULB_EMISSION);
            // 세기는 2026-09-14 창고 맵에서 튜너(MapAtmosphereTuner)로 맞춘 값이다.
            Material amberCone = ConeMaterial(cone, "M_LightCone_Amber", AMBER, 0.40f, 2.2f, 0.06f);
            Material moonConeMat = ConeMaterial(cone, "M_LightCone_Moon", MOON, 0.25f, 1.5f, 0.02f);
            Material dustMat = DustMaterial(particle, "M_Dust");
            Material skybox = NightSkybox(sky);

            GameObject lamp = BuildLamp(shade, cord, lampCone, shadeMat, cordMat, bulbMat, amberCone, dustMat);
            GameObject moonShaft = BuildMoonShaft(moonCone, moonConeMat, dustMat);
            VolumeProfile post = BuildPostProfile();

            string profilePath = $"{RES_DIR}/MapAtmosphere.asset";
            var profile = AssetDatabase.LoadAssetAtPath<MapAtmosphereProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MapAtmosphereProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            var so = new SerializedObject(profile);
            so.FindProperty("m_LampPrefab").objectReferenceValue = lamp;
            so.FindProperty("m_MoonShaftPrefab").objectReferenceValue = moonShaft;
            so.FindProperty("m_NightSkybox").objectReferenceValue = skybox;
            so.FindProperty("m_PostProfile").objectReferenceValue = post;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MapAtmosphere] 완료 → {profilePath}");
        }

        // ── 프리팹 ─────────────────────────────────────────────

        private static GameObject BuildLamp(Mesh _shade, Mesh _cord, Mesh _cone, Material _shadeMat, Material _cordMat,
            Material _bulbMat, Material _coneMat, Material _dustMat)
        {
            var root = new GameObject("FX_HangingLamp");

            // 메시 피벗은 위 가운데, 아래로 자란다 — 줄은 천장 쪽(3.26)에서 갓 꼭대기(0.26)까지, 갓은 0.28에서 입구(0)까지.
            MeshPart(root.transform, "Cord", _cord, _cordMat, new Vector3(0f, 3.26f, 0f), Vector3.one);
            MeshPart(root.transform, "Shade", _shade, _shadeMat, new Vector3(0f, 0.28f, 0f), Vector3.one);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(sphere);
            MeshRenderer bulb = MeshPart(root.transform, "Bulb", sphereMesh, _bulbMat, new Vector3(0f, 0.02f, 0f), Vector3.one * 0.17f);

            MeshRenderer cone = MeshPart(root.transform, "Cone", _cone, _coneMat, new Vector3(0f, 0.02f, 0f), new Vector3(2f, 4f, 2f));
            cone.gameObject.transform.localRotation = Quaternion.identity;

            Dust(root.transform, _dustMat, new Vector3(0f, -1.2f, 0f), false);

            var flicker = root.AddComponent<LampFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("m_Bulb").objectReferenceValue = bulb;
            so.FindProperty("m_BulbEmission").colorValue = BULB_EMISSION;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, "FX_HangingLamp");
        }

        private static GameObject BuildMoonShaft(Mesh _cone, Material _coneMat, Material _dustMat)
        {
            var root = new GameObject("FX_MoonShaft");
            MeshPart(root.transform, "Cone", _cone, _coneMat, Vector3.zero, new Vector3(4f, 6f, 4f));
            Dust(root.transform, _dustMat, new Vector3(0f, -3f, 0f), true);
            return SavePrefab(root, "FX_MoonShaft");
        }

        private static MeshRenderer MeshPart(Transform _parent, string _name, Mesh _mesh, Material _mat, Vector3 _pos, Vector3 _scale)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            go.transform.localPosition = _pos;
            go.transform.localScale = _scale;
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;   // 광원 대부분이 그림자 없음 — 달빛에만 괜히 걸린다
            mr.receiveShadows = false;
            return mr;
        }

        /// <summary>빛 속에 떠다니는 먼지 — 느리고 드물게. 입자 상한이 곧 비용 상한이다.</summary>
        private static void Dust(Transform _parent, Material _mat, Vector3 _pos, bool _wide)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(_parent, false);
            go.transform.localPosition = _pos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.9f, 0.7f, 0.55f);
            main.maxParticles = _wide ? 30 : 22;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;

            var emission = ps.emission;
            emission.rateOverTime = _wide ? 4f : 3f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = _wide ? new Vector3(4f, 6f, 4f) : new Vector3(2.2f, 2.6f, 2.2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static GameObject SavePrefab(GameObject _root, string _name)
        {
            string path = $"{FX_DIR}/{_name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(_root, path);
            Object.DestroyImmediate(_root);
            return prefab;
        }

        // ── 머티리얼 ────────────────────────────────────────────

        private static Material LitMaterial(Shader _shader, string _name, Color _base, float _metal, float _smooth, Color _emission)
        {
            Material mat = LoadOrCreate(_shader, _name);
            mat.SetColor("_BaseColor", _base);
            mat.SetFloat("_Metallic", _metal);
            mat.SetFloat("_Smoothness", _smooth);
            if (_emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", _emission);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ConeMaterial(Shader _shader, string _name, Color _color, float _intensity, float _edge, float _topFade)
        {
            Material mat = LoadOrCreate(_shader, _name);
            mat.SetColor("_BaseColor", _color);
            mat.SetFloat("_Intensity", _intensity);
            mat.SetFloat("_EdgePower", _edge);
            mat.SetFloat("_TopFade", _topFade);
            mat.SetFloat("_Shimmer", 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material DustMaterial(Shader _shader, string _name)
        {
            Material mat = LoadOrCreate(_shader, _name);
            mat.SetTexture("_BaseMap", SoftDotTexture());
            mat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.75f, 1f));
            // 가산 투명(URP Particles/Unlit 표준 설정)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);   // Additive
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material NightSkybox(Shader _shader)
        {
            Material mat = LoadOrCreate(_shader, "M_NightSky");
            mat.SetFloat("_SunDisk", 0f);   // 태양 원반 없음 — 달빛(방향광)이 해처럼 박히면 밤이 깨진다
            mat.DisableKeyword("_SUNDISK_SIMPLE");
            mat.DisableKeyword("_SUNDISK_HIGH_QUALITY");
            mat.EnableKeyword("_SUNDISK_NONE");
            mat.SetFloat("_SunSize", 0f);
            mat.SetFloat("_AtmosphereThickness", 0.55f);
            mat.SetColor("_SkyTint", new Color(0.10f, 0.14f, 0.28f));
            mat.SetColor("_GroundColor", new Color(0.015f, 0.018f, 0.025f));
            mat.SetFloat("_Exposure", 0.30f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(Shader _shader, string _name)
        {
            string path = $"{MAT_DIR}/{_name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(_shader) { name = _name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != _shader)
            {
                mat.shader = _shader;
            }
            return mat;
        }

        private static Texture2D SoftDotTexture()
        {
            string path = $"{MAT_DIR}/T_SoftDot.png";
            if (!File.Exists(path))
            {
                const int SIZE = 64;
                var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
                var px = new Color32[SIZE * SIZE];
                for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = (x + 0.5f) / SIZE * 2f - 1f, dy = (y + 0.5f) / SIZE * 2f - 1f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.2f);
                    px[y * SIZE + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ── 후보정 ─────────────────────────────────────────────

        /// <summary>
        /// 밤 창고 톤: ACES로 대비를 세우고, 블룸은 HDR 전구·빛줄기만 번지게(임계 1.0), 그림자는 남색·빛은 앰버로 가른다.
        /// ACES가 중간톤을 눌러 전체가 어두워지므로 노출을 조금 올린다 — 어둠 스킬의 "안 보임"은 DarknessFeature가 따로 만든다.
        /// </summary>
        private static VolumeProfile BuildPostProfile()
        {
            string path = $"{RES_DIR}/MapAtmospherePost.asset";
            AssetDatabase.DeleteAsset(path);   // 구성요소 서브에셋이 남지 않게 통째로 새로
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1.0f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(1.7f);   // ACES가 중간톤을 누른다 — 튜너로 맞춘 값
            color.contrast.Override(10f);
            color.saturation.Override(10f);

            var split = Add<SplitToning>(profile);
            // 처음엔 진한 남색(0.36,0.46,0.78)이었다 — 화면 전체가 파랗게 죽었다(2026-09-14 스크린샷). 은은하게.
            split.shadows.Override(new Color(0.47f, 0.50f, 0.60f));
            split.highlights.Override(new Color(1f, 0.80f, 0.55f));
            split.balance.Override(0f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Add<T>(VolumeProfile _profile) where T : VolumeComponent
        {
            T component = _profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, _profile);
            return component;
        }

        // ── 메시 ─────────────────────────────────────────────

        /// <summary>
        /// 원뿔대(위 반지름 → 아래 반지름, 피벗=위 가운데, 아래로 _height). uv.y 0=위·1=아래(빛줄기 셰이더가 쓴다).
        /// _doubleSided면 안쪽 면도 만든다(전등 갓 속이 보여야 한다).
        /// </summary>
        private static Mesh Frustum(string _name, float _rTop, float _rBottom, float _height, int _segments, bool _doubleSided)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            float slope = (_rBottom - _rTop) / Mathf.Max(_height, 1e-4f);

            for (int i = 0; i <= _segments; i++)
            {
                float a = i / (float)_segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 normal = new Vector3(dir.x, slope, dir.z).normalized;
                v.Add(dir * _rTop);
                v.Add(dir * _rBottom + Vector3.down * _height);
                n.Add(normal);
                n.Add(normal);
                uv.Add(new Vector2(i / (float)_segments, 0f));
                uv.Add(new Vector2(i / (float)_segments, 1f));
            }
            for (int i = 0; i < _segments; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tris.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
            }

            if (_doubleSided)
            {
                int offset = v.Count;
                for (int i = 0; i < offset; i++)
                {
                    v.Add(v[i]);
                    n.Add(-n[i]);
                    uv.Add(uv[i]);
                }
                int count = tris.Count;
                for (int i = 0; i < count; i += 3)
                    tris.AddRange(new[] { tris[i] + offset, tris[i + 2] + offset, tris[i + 1] + offset });
            }

            var mesh = new Mesh { name = _name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>함정 4 — 기존 메시 에셋을 CopySerialized로 덮지 않는다. Clear 후 Mesh API로 다시 채운다.</summary>
        private static Mesh SaveMesh(Mesh _fresh, string _name)
        {
            string path = $"{MESH_DIR}/{_name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(_fresh, path);
                return _fresh;
            }
            existing.Clear();
            existing.SetVertices(_fresh.vertices);
            existing.SetNormals(_fresh.normals);
            existing.SetUVs(0, _fresh.uv);
            existing.SetTriangles(_fresh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(_fresh);
            return existing;
        }

        private static void EnsureDir(string _path)
        {
            if (AssetDatabase.IsValidFolder(_path)) return;
            string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
        }
    }
}
