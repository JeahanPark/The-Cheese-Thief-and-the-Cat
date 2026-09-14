using System;
using System.Collections.Generic;
using System.IO;
using CTC.Runtime.Fx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 기술 이펙트를 코드로 굽는다 — Tools > CTC > Fx > Build Skill Effects (2026-09-14).
    /// 입자 텍스처(점·링·불꽃줄·연기·별·할퀴기)·머티리얼·프리팹 22개 → `Resources/Fx/FxCatalog.asset`.
    ///
    /// 전부 생성물이다. 모양을 바꾸려면 아래 <see cref="Define"/>의 수치를 고치고 다시 돌린다.
    /// 규칙: **한 번 터지고 끝난다**(오라 없음 — 기획서 §11), 입자 수는 이펙트당 ≤ 40.
    /// </summary>
    public static class FxPrefabBuilder
    {
        private const string TEX_DIR = "Assets/Materials/Fx";
        private const string PREFAB_DIR = "Assets/Prefabs/Fx/Skill";
        private const string RES_DIR = "Assets/Resources/Fx";

        private enum Tex { Dot, Ring, Streak, Smoke, Star, Slash }
        private enum Blend { Additive, Alpha }

        private sealed class Layer
        {
            public string Name = "Layer";
            public Tex Texture = Tex.Dot;
            public Blend BlendMode = Blend.Additive;
            public int Burst = 8;
            public Vector2 Life = new Vector2(0.3f, 0.5f);
            public Vector2 Speed = new Vector2(1f, 2f);
            public Vector2 Size = new Vector2(0.1f, 0.2f);
            public Color Color = Color.white;
            public Color EndColor = new Color(1f, 1f, 1f, 0f);
            public ParticleSystemShapeType Shape = ParticleSystemShapeType.Sphere;
            public float Radius = 0.05f;
            public float Angle = 25f;
            public Vector3 ShapeEuler = Vector3.zero;
            public float Gravity;
            public ParticleSystemRenderMode Mode = ParticleSystemRenderMode.Billboard;
            public float LengthScale = 2.5f;
            public float SizeFrom = 1f;
            public float SizeTo = 1f;
            public bool RandomSpin;
            public float Drag;
            public Vector3 Offset = Vector3.zero;
            public float Delay;
        }

        [MenuItem("Tools/CTC/Fx/Build Skill Effects (기술 이펙트)")]
        public static void Build()
        {
            EnsureDir(TEX_DIR);
            EnsureDir(PREFAB_DIR);
            EnsureDir(RES_DIR);

            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particle == null)   // 함정 6
            {
                Debug.LogError("[FxBuilder] 입자 셰이더를 못 찾았다 — 리임포트가 끝난 뒤 다시");
                return;
            }

            var textures = new Dictionary<Tex, Texture2D>();
            foreach (Tex t in Enum.GetValues(typeof(Tex))) textures[t] = BakeTexture(t);

            string catalogPath = $"{RES_DIR}/FxCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<FxCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FxCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            var so = new SerializedObject(catalog);
            SerializedProperty prefabs = so.FindProperty("m_Prefabs");
            prefabs.arraySize = FxCatalog.SIZE;

            int built = 0;
            foreach (FxId id in Enum.GetValues(typeof(FxId)))
            {
                Layer[] layers = Define(id);
                if (layers == null) continue;
                GameObject prefab = BuildPrefab(id, layers, particle, textures);
                prefabs.GetArrayElementAtIndex((int)id).objectReferenceValue = prefab;
                built++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FxBuilder] 이펙트 {built}개 → {catalogPath}");
        }

        // ── 이펙트 정의 ─────────────────────────────────────────

        private static readonly Color DUST = new Color(0.55f, 0.50f, 0.42f, 0.55f);
        private static readonly Color CHEESE = new Color(1f, 0.85f, 0.35f, 1f);
        private static readonly Color FIRE = new Color(1f, 0.48f, 0.12f, 1f);
        private static readonly Color WIND = new Color(0.62f, 1f, 0.88f, 0.9f);
        private static readonly Color LIGHT = new Color(1f, 0.97f, 0.78f, 1f);
        private static readonly Color DARK = new Color(0.46f, 0.26f, 0.72f, 1f);

        private static Color A(Color _c, float _a) => new Color(_c.r, _c.g, _c.b, _a);

        private static Layer Ring(Color _c, float _sizeTo, float _life, float _delay = 0f) => new Layer
        {
            Name = "Ring", Texture = Tex.Ring, Burst = 1, Life = new Vector2(_life, _life), Speed = Vector2.zero,
            Size = new Vector2(1f, 1f), SizeFrom = 0.15f, SizeTo = _sizeTo, Color = _c, EndColor = A(_c, 0f),
            Mode = ParticleSystemRenderMode.HorizontalBillboard, Offset = new Vector3(0f, 0.08f, 0f), Delay = _delay,
        };

        private static Layer Flash(Color _c, float _size, float _life = 0.12f, float _y = 0.5f) => new Layer
        {
            Name = "Flash", Texture = Tex.Dot, Burst = 1, Life = new Vector2(_life, _life), Speed = Vector2.zero,
            Size = new Vector2(_size, _size), SizeFrom = 0.6f, SizeTo = 1.2f, Color = _c, EndColor = A(_c, 0f),
            Offset = new Vector3(0f, _y, 0f),
        };

        private static Layer[] Define(FxId _id)
        {
            switch (_id)
            {
                case FxId.MuzzleFlash:
                    return new[]
                    {
                        new Layer { Name = "Star", Texture = Tex.Star, Burst = 1, Life = new Vector2(0.06f, 0.08f), Speed = Vector2.zero,
                            Size = new Vector2(0.35f, 0.45f), Color = new Color(1f, 0.92f, 0.55f), EndColor = new Color(1f, 0.6f, 0.2f, 0f), RandomSpin = true },
                        new Layer { Name = "Sparks", Texture = Tex.Streak, Burst = 6, Life = new Vector2(0.1f, 0.18f), Speed = new Vector2(3f, 6f),
                            Size = new Vector2(0.03f, 0.05f), Color = new Color(1f, 0.85f, 0.4f), Shape = ParticleSystemShapeType.Cone, Angle = 16f,
                            Mode = ParticleSystemRenderMode.Stretch, LengthScale = 4f },
                    };
                case FxId.BulletImpact:
                    return new[]
                    {
                        new Layer { Name = "Puff", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 5, Life = new Vector2(0.25f, 0.4f),
                            Speed = new Vector2(0.4f, 1.1f), Size = new Vector2(0.12f, 0.22f), SizeTo = 1.8f, Color = new Color(0.85f, 0.8f, 0.7f, 0.55f), RandomSpin = true },
                        new Layer { Name = "Sparks", Texture = Tex.Streak, Burst = 5, Life = new Vector2(0.1f, 0.2f), Speed = new Vector2(2f, 4f),
                            Size = new Vector2(0.025f, 0.04f), Color = new Color(1f, 0.8f, 0.35f), Mode = ParticleSystemRenderMode.Stretch, LengthScale = 3f, Gravity = 1f },
                    };
                case FxId.PlaceDust:
                    return new[]
                    {
                        new Layer { Name = "Dust", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 16, Life = new Vector2(0.6f, 0.95f),
                            Speed = new Vector2(1.5f, 2.6f), Size = new Vector2(0.5f, 0.9f), SizeTo = 1.7f, Color = DUST,
                            Shape = ParticleSystemShapeType.Circle, Radius = 0.9f, ShapeEuler = new Vector3(90f, 0f, 0f), Drag = 3f, RandomSpin = true,
                            Offset = new Vector3(0f, 0.15f, 0f) },
                        Ring(new Color(0.8f, 0.72f, 0.55f, 0.35f), 5f, 0.45f),
                    };
                case FxId.FortressPush:
                    return new[] { Ring(new Color(1f, 0.85f, 0.55f, 0.9f), 9f, 0.45f), Ring(new Color(1f, 0.75f, 0.4f, 0.5f), 6f, 0.35f, 0.08f) };
                case FxId.ThornSpark:
                    return new[]
                    {
                        new Layer { Name = "Sparks", Texture = Tex.Streak, Burst = 14, Life = new Vector2(0.15f, 0.3f), Speed = new Vector2(3f, 6f),
                            Size = new Vector2(0.03f, 0.06f), Color = new Color(1f, 0.55f, 0.25f), Mode = ParticleSystemRenderMode.Stretch, LengthScale = 3f, Gravity = 0.8f },
                        Flash(new Color(1f, 0.6f, 0.3f, 0.9f), 0.8f, 0.1f, 0f),
                    };
                case FxId.StickySplat:
                    return new[]
                    {
                        new Layer { Name = "Goo", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 12, Life = new Vector2(0.4f, 0.6f),
                            Speed = new Vector2(1.5f, 3f), Size = new Vector2(0.12f, 0.24f), Color = new Color(0.62f, 0.8f, 0.24f, 0.95f),
                            Shape = ParticleSystemShapeType.Hemisphere, ShapeEuler = new Vector3(-90f, 0f, 0f), Radius = 0.1f, Gravity = 2.5f, RandomSpin = true },
                        Ring(new Color(0.62f, 0.85f, 0.3f, 0.45f), 2.4f, 0.4f),
                    };
                case FxId.DashDust:
                    return new[]
                    {
                        new Layer { Name = "Dust", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 9, Life = new Vector2(0.4f, 0.6f),
                            Speed = new Vector2(1f, 2.2f), Size = new Vector2(0.22f, 0.4f), SizeTo = 1.9f, Color = DUST,
                            Shape = ParticleSystemShapeType.Cone, Angle = 30f, ShapeEuler = new Vector3(0f, 180f, 0f), Drag = 2f, RandomSpin = true,
                            Offset = new Vector3(0f, 0.12f, 0f) },
                        new Layer { Name = "Lines", Texture = Tex.Streak, Burst = 6, Life = new Vector2(0.12f, 0.2f), Speed = new Vector2(4f, 6f),
                            Size = new Vector2(0.02f, 0.03f), Color = new Color(1f, 1f, 1f, 0.6f), Shape = ParticleSystemShapeType.Cone, Angle = 8f,
                            ShapeEuler = new Vector3(0f, 180f, 0f), Mode = ParticleSystemRenderMode.Stretch, LengthScale = 6f, Offset = new Vector3(0f, 0.3f, 0f) },
                    };
                case FxId.BuyCoins:
                    return new[]
                    {
                        new Layer { Name = "Coins", Texture = Tex.Dot, Burst = 9, Life = new Vector2(0.5f, 0.7f), Speed = new Vector2(2f, 3.5f),
                            Size = new Vector2(0.09f, 0.14f), Color = CHEESE, Shape = ParticleSystemShapeType.Cone, Angle = 35f,
                            ShapeEuler = new Vector3(-90f, 0f, 0f), Gravity = 3f },
                        new Layer { Name = "Twinkle", Texture = Tex.Star, Burst = 3, Life = new Vector2(0.3f, 0.45f), Speed = new Vector2(0.5f, 1f),
                            Size = new Vector2(0.18f, 0.26f), SizeTo = 0.2f, Color = new Color(1f, 0.95f, 0.7f), RandomSpin = true },
                    };
                case FxId.EatCrumbs:
                    return new[]
                    {
                        new Layer { Name = "Crumbs", Texture = Tex.Dot, BlendMode = Blend.Alpha, Burst = 5, Life = new Vector2(0.35f, 0.5f),
                            Speed = new Vector2(0.6f, 1.4f), Size = new Vector2(0.04f, 0.07f), Color = CHEESE, EndColor = A(CHEESE, 0.4f),
                            Shape = ParticleSystemShapeType.Hemisphere, ShapeEuler = new Vector3(-90f, 0f, 0f), Gravity = 3f },
                    };
                case FxId.CheesePickup:
                    return new[]
                    {
                        new Layer { Name = "Stars", Texture = Tex.Star, Burst = 6, Life = new Vector2(0.35f, 0.5f), Speed = new Vector2(0.6f, 1.6f),
                            Size = new Vector2(0.12f, 0.2f), SizeTo = 0.2f, Color = new Color(1f, 0.92f, 0.45f), RandomSpin = true },
                        Flash(new Color(1f, 0.85f, 0.35f, 0.7f), 0.7f, 0.15f, 0f),
                    };
                case FxId.HeroBurst:
                    return new[]
                    {
                        Ring(new Color(1f, 0.85f, 0.35f, 0.9f), 6f, 0.55f),
                        new Layer { Name = "Rise", Texture = Tex.Streak, Burst = 22, Life = new Vector2(0.6f, 0.9f), Speed = new Vector2(2.5f, 5f),
                            Size = new Vector2(0.04f, 0.07f), Color = new Color(1f, 0.88f, 0.4f), Shape = ParticleSystemShapeType.Cone, Angle = 22f,
                            ShapeEuler = new Vector3(-90f, 0f, 0f), Radius = 0.35f, Mode = ParticleSystemRenderMode.Stretch, LengthScale = 3f, Drag = 1f },
                        Flash(new Color(1f, 0.9f, 0.5f, 0.8f), 1.6f, 0.3f),
                    };
                case FxId.GangSmoke:
                    return new[]
                    {
                        new Layer { Name = "Smoke", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 16, Life = new Vector2(0.8f, 1.2f),
                            Speed = new Vector2(0.6f, 1.6f), Size = new Vector2(0.5f, 1f), SizeTo = 2f, Color = new Color(0.34f, 0.26f, 0.40f, 0.75f),
                            Radius = 0.3f, Drag = 1.5f, RandomSpin = true, Offset = new Vector3(0f, 0.4f, 0f) },
                        new Layer { Name = "Embers", Texture = Tex.Dot, Burst = 14, Life = new Vector2(0.6f, 1f), Speed = new Vector2(1.2f, 3f),
                            Size = new Vector2(0.07f, 0.12f), Color = new Color(0.9f, 0.5f, 1f), Offset = new Vector3(0f, 0.4f, 0f) },
                        Ring(new Color(0.75f, 0.4f, 1f, 0.7f), 5f, 0.5f),
                    };
                case FxId.ClubSmash:
                    return new[]
                    {
                        new Layer { Name = "Dust", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 10, Life = new Vector2(0.35f, 0.6f),
                            Speed = new Vector2(1.2f, 2.4f), Size = new Vector2(0.25f, 0.45f), SizeTo = 1.8f, Color = DUST,
                            Shape = ParticleSystemShapeType.Circle, Radius = 0.25f, ShapeEuler = new Vector3(90f, 0f, 0f), Drag = 3f, RandomSpin = true,
                            Offset = new Vector3(0f, 0.1f, 0f) },
                        new Layer { Name = "Stars", Texture = Tex.Star, Burst = 5, Life = new Vector2(0.35f, 0.5f), Speed = new Vector2(1.5f, 3f),
                            Size = new Vector2(0.14f, 0.22f), SizeTo = 0.2f, Color = new Color(1f, 0.95f, 0.6f), Shape = ParticleSystemShapeType.Hemisphere,
                            ShapeEuler = new Vector3(-90f, 0f, 0f), Gravity = 1.5f, RandomSpin = true, Offset = new Vector3(0f, 0.3f, 0f) },
                        Ring(new Color(1f, 0.9f, 0.7f, 0.6f), 3f, 0.3f),
                    };
                case FxId.ClawSlash:
                    return new[]
                    {
                        new Layer { Name = "Slash", Texture = Tex.Slash, Burst = 3, Life = new Vector2(0.24f, 0.32f), Speed = Vector2.zero,
                            Size = new Vector2(1.2f, 1.6f), SizeFrom = 0.7f, SizeTo = 1.2f, Color = new Color(1.6f, 1.5f, 1.4f), EndColor = new Color(1f, 0.35f, 0.25f, 0f),
                            Shape = ParticleSystemShapeType.Box, Radius = 0.2f, RandomSpin = true },
                    };
                case FxId.AdvanceFire: return AdvanceBurst(FIRE);
                case FxId.AdvanceWind: return AdvanceBurst(WIND);
                case FxId.AdvanceLight: return AdvanceBurst(LIGHT);
                case FxId.AdvanceDark: return AdvanceBurst(DARK);
                case FxId.SkillFire:
                    return new[]
                    {
                        new Layer { Name = "Embers", Texture = Tex.Dot, Burst = 26, Life = new Vector2(0.5f, 0.9f), Speed = new Vector2(2f, 4.5f),
                            Size = new Vector2(0.06f, 0.12f), SizeTo = 0.3f, Color = new Color(1f, 0.62f, 0.2f), EndColor = new Color(1f, 0.2f, 0.05f, 0f),
                            Shape = ParticleSystemShapeType.Cone, Angle = 38f, ShapeEuler = new Vector3(-90f, 0f, 0f), Radius = 0.5f, Gravity = -0.3f, Drag = 1f },
                        Flash(new Color(1f, 0.5f, 0.15f, 0.85f), 2.2f, 0.22f, 0.7f),
                        Ring(A(FIRE, 0.7f), 7f, 0.4f),
                    };
                case FxId.SkillWind:
                    return new[]
                    {
                        Ring(WIND, 11f, 0.5f), Ring(A(WIND, 0.6f), 8f, 0.45f, 0.1f),
                        new Layer { Name = "Gust", Texture = Tex.Streak, Burst = 18, Life = new Vector2(0.25f, 0.4f), Speed = new Vector2(5f, 8f),
                            Size = new Vector2(0.03f, 0.05f), Color = new Color(0.85f, 1f, 0.95f, 0.8f), Shape = ParticleSystemShapeType.Circle,
                            Radius = 0.6f, ShapeEuler = new Vector3(90f, 0f, 0f), Mode = ParticleSystemRenderMode.Stretch, LengthScale = 5f,
                            Offset = new Vector3(0f, 0.5f, 0f) },
                    };
                case FxId.SkillLight:
                    return new[]
                    {
                        Flash(new Color(1f, 0.98f, 0.85f, 1f), 3.2f, 0.28f, 0.8f),
                        new Layer { Name = "Stars", Texture = Tex.Star, Burst = 12, Life = new Vector2(0.4f, 0.7f), Speed = new Vector2(1.2f, 2.5f),
                            Size = new Vector2(0.18f, 0.3f), SizeTo = 0.2f, Color = LIGHT, Radius = 0.4f, RandomSpin = true, Offset = new Vector3(0f, 0.8f, 0f) },
                        Ring(A(LIGHT, 0.8f), 9f, 0.45f),
                    };
                case FxId.SkillDark:
                    return new[]
                    {
                        new Layer { Name = "Smoke", Texture = Tex.Smoke, BlendMode = Blend.Alpha, Burst = 22, Life = new Vector2(1f, 1.4f),
                            Speed = new Vector2(0.8f, 2f), Size = new Vector2(0.8f, 1.4f), SizeTo = 2.2f, Color = new Color(0.30f, 0.20f, 0.42f, 0.8f),
                            Radius = 0.5f, Drag = 1.5f, RandomSpin = true, Offset = new Vector3(0f, 0.6f, 0f) },
                        new Layer { Name = "Wisps", Texture = Tex.Dot, Burst = 18, Life = new Vector2(0.7f, 1.1f), Speed = new Vector2(1.2f, 2.6f),
                            Size = new Vector2(0.08f, 0.14f), Color = new Color(0.8f, 0.5f, 1f), Offset = new Vector3(0f, 0.6f, 0f) },
                        Ring(new Color(0.65f, 0.35f, 1f, 0.8f), 9f, 0.5f),
                    };
                case FxId.FrenzyShock:
                    return new[]
                    {
                        Ring(new Color(1f, 0.25f, 0.18f, 0.95f), 10f, 0.45f), Ring(new Color(1f, 0.45f, 0.2f, 0.55f), 7f, 0.4f, 0.1f),
                        new Layer { Name = "Sparks", Texture = Tex.Streak, Burst = 18, Life = new Vector2(0.25f, 0.4f), Speed = new Vector2(5f, 8f),
                            Size = new Vector2(0.04f, 0.06f), Color = new Color(1f, 0.35f, 0.2f), Shape = ParticleSystemShapeType.Circle, Radius = 0.5f,
                            ShapeEuler = new Vector3(90f, 0f, 0f), Mode = ParticleSystemRenderMode.Stretch, LengthScale = 4f, Offset = new Vector3(0f, 0.4f, 0f) },
                    };
                default:
                    return null;
            }
        }

        private static Layer[] AdvanceBurst(Color _c)
        {
            bool dark = _c.r < 0.5f && _c.g < 0.5f;
            return new[]
            {
                Ring(A(_c, 0.9f), 7f, 0.6f),
                new Layer { Name = "Rise", Texture = dark ? Tex.Smoke : Tex.Streak, BlendMode = dark ? Blend.Alpha : Blend.Additive,
                    Burst = 24, Life = new Vector2(0.7f, 1.1f), Speed = new Vector2(2f, 4f), Size = dark ? new Vector2(0.4f, 0.7f) : new Vector2(0.05f, 0.08f),
                    Color = dark ? new Color(0.2f, 0.12f, 0.3f, 0.8f) : _c, Shape = ParticleSystemShapeType.Cone, Angle = 15f,
                    ShapeEuler = new Vector3(-90f, 0f, 0f), Radius = 0.6f, Mode = dark ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch,
                    LengthScale = 3f, Drag = 1f, RandomSpin = dark },
                Flash(A(dark ? new Color(0.6f, 0.4f, 0.9f) : _c, 0.8f), 2f, 0.35f, 0.8f),
            };
        }

        // ── 프리팹 ─────────────────────────────────────────────

        private static GameObject BuildPrefab(FxId _id, Layer[] _layers, Shader _shader, Dictionary<Tex, Texture2D> _textures)
        {
            var root = new GameObject("FX_" + _id);
            var systems = new List<ParticleSystem>();
            float seconds = 0f;

            foreach (Layer layer in _layers)
            {
                var go = new GameObject(layer.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = layer.Offset;

                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.duration = 0.1f;
                main.loop = false;
                main.playOnAwake = false;   // FxOneShot.Restart가 튼다 — 풀에서 켜질 때 두 번 터지지 않게
                main.startDelay = layer.Delay;
                main.startLifetime = new ParticleSystem.MinMaxCurve(layer.Life.x, layer.Life.y);
                main.startSpeed = new ParticleSystem.MinMaxCurve(layer.Speed.x, layer.Speed.y);
                main.startSize = new ParticleSystem.MinMaxCurve(layer.Size.x, layer.Size.y);
                main.startColor = layer.Color;
                main.gravityModifier = layer.Gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.maxParticles = Mathf.Max(1, layer.Burst);
                if (layer.RandomSpin) main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)layer.Burst) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = layer.Shape;
                shape.radius = layer.Radius;
                shape.angle = layer.Angle;
                shape.rotation = layer.ShapeEuler;
                if (layer.Shape == ParticleSystemShapeType.Box) shape.scale = Vector3.one * (layer.Radius * 2f);

                var color = ps.colorOverLifetime;
                color.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(layer.EndColor.r / Mathf.Max(layer.Color.r, 1e-3f),
                        layer.EndColor.g / Mathf.Max(layer.Color.g, 1e-3f), layer.EndColor.b / Mathf.Max(layer.Color.b, 1e-3f)), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(layer.EndColor.a, 1f) });
                color.color = grad;

                if (!Mathf.Approximately(layer.SizeFrom, 1f) || !Mathf.Approximately(layer.SizeTo, 1f))
                {
                    var size = ps.sizeOverLifetime;
                    size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, layer.SizeFrom, 1f, layer.SizeTo));
                }

                if (layer.Drag > 0f)
                {
                    var limit = ps.limitVelocityOverLifetime;
                    limit.enabled = true;
                    limit.limit = 100f;   // 속도 상한은 안 쓴다 — 공기 저항(drag)만으로 퍼지다 멈추게
                    limit.dampen = 0f;
                    limit.drag = layer.Drag;
                }

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = layer.Mode;
                if (layer.Mode == ParticleSystemRenderMode.Stretch) renderer.lengthScale = layer.LengthScale;
                renderer.sharedMaterial = MaterialFor(_shader, layer.Texture, layer.BlendMode, _textures);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = layer.BlendMode == Blend.Alpha ? 1f : 0f;

                systems.Add(ps);
                seconds = Mathf.Max(seconds, layer.Delay + layer.Life.y);
            }

            var shot = root.AddComponent<FxOneShot>();
            var shotSo = new SerializedObject(shot);
            shotSo.FindProperty("m_Seconds").floatValue = seconds + 0.1f;
            SerializedProperty list = shotSo.FindProperty("m_Systems");
            list.arraySize = systems.Count;
            for (int i = 0; i < systems.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            shotSo.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PREFAB_DIR}/FX_{_id}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material MaterialFor(Shader _shader, Tex _tex, Blend _blend, Dictionary<Tex, Texture2D> _textures)
        {
            string path = $"{TEX_DIR}/M_Fx_{_tex}_{_blend}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(_shader) { name = $"M_Fx_{_tex}_{_blend}" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = _shader;
            mat.SetTexture("_BaseMap", _textures[_tex]);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", _blend == Blend.Additive ? 2f : 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(_blend == Blend.Additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── 텍스처 ─────────────────────────────────────────────

        private static Texture2D BakeTexture(Tex _tex)
        {
            string path = $"{TEX_DIR}/T_Fx_{_tex}.png";
            const int SIZE = 128;
            var t = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            var px = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                float u = (x + 0.5f) / SIZE * 2f - 1f;
                float v = (y + 0.5f) / SIZE * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a;
                switch (_tex)
                {
                    case Tex.Ring:
                        a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.78f) / 0.14f);
                        a = a * a;
                        break;
                    case Tex.Streak:
                        a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 2.2f), 2f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 0.8f);
                        break;
                    case Tex.Smoke:
                        float n = Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.6f + Mathf.PerlinNoise(x * 0.19f + 7f, y * 0.19f) * 0.4f;
                        a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f) * Mathf.Lerp(0.55f, 1.15f, n);
                        break;
                    case Tex.Star:
                        float h = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 0.6f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 8f), 1.5f);
                        float vv = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 0.6f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u) * 8f), 1.5f);
                        a = Mathf.Clamp01(h + vv + Mathf.Pow(Mathf.Clamp01(1f - r * 2.4f), 2f));
                        break;
                    case Tex.Slash:
                        // 초승달 — 바깥 원에서 안쪽으로 비낀 원을 뺀다. 끝이 가늘어진다.
                        float outer = Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) / 0.18f);
                        float inner = Mathf.Sqrt((u + 0.18f) * (u + 0.18f) + (v - 0.1f) * (v - 0.1f));
                        float cut = Mathf.Clamp01((inner - 0.62f) / 0.12f);
                        float taper = Mathf.Clamp01((u + 0.9f) / 1.2f) * Mathf.Clamp01(1f - Mathf.Abs(v) * 0.6f);
                        a = outer * cut * taper;
                        break;
                    default:   // Dot
                        a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                        break;
                }
                px[y * SIZE + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            t.SetPixels32(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
