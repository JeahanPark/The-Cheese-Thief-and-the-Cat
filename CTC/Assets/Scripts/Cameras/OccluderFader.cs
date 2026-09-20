using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 카메라와 추적 대상 사이를 가리는 벽을 반투명으로 만든다(Witcher 3·A Hat in Time 방식).
    /// 카메라를 앞으로 당기는 Deoccluder를 대체한다 — 당김은 좁은 방(4~7m)·복도(1.6m)에서
    /// 차폐가 붙었다 떨어졌다 하며 진동하고 화면을 좁혔다(2026-07-28 플레이 피드백).
    /// 이 방식은 카메라 거리를 상수로 유지하므로 진동·협착이 원천적으로 생기지 않는다.
    /// 로컬 전용 연출이라 네트워크와 무관하다.
    ///
    /// **흐리는 것은 「맞은 콜라이더의 물체」 전체다**(2026-09-20 사용자 보고: 「카메라가 부딪쳐야만 투명해지고,
    /// 사이에 있는 사물은 안 흐려진다」). 예전엔 콜라이더와 **같은 오브젝트의 렌더러 하나**만 흐려서, 판정 상자와
    /// 그림이 따로 붙은 물체(상점 부스의 `BodyCollider`, 요새·창고)는 한 번도 안 흐려졌다 — 카메라가 메시 안으로
    /// 들어가 뒷면이 안 그려질 때만 비쳐 보였던 것이다. 이제 ① 발과 겨누는 점 두 곳으로 캐스트하고
    /// ② 카메라가 이미 닿은 것도 겹침으로 잡고 ③ 콜라이더 → 그 물체의 렌더러 묶음으로 넓혀 흐린다.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class OccluderFader : MonoBehaviour
    {
        private const int HIT_BUFFER_SIZE = 32;

        [SerializeField] private float m_CastRadius = 0.25f;
        [SerializeField, Range(0f, 1f)] private float m_FadedAlpha = 0.25f;
        [SerializeField] private LayerMask m_ObstacleLayers = 1; // Default — 바닥은 Ignore Raycast(2)라 자동 제외
        [SerializeField] private string m_IgnoreTag = "Player";  // 플레이어 몸(모델 자식 포함)은 가림막이 아니다
        // 카메라가 겨누는 높이(궤도 카메라 TargetOffset과 같은 값) — 발끝만 보면 윗몸을 가린 것을 놓친다
        [SerializeField] private float m_AimHeight = 0.7f;

        private CinemachineCamera m_Camera;
        private readonly RaycastHit[] m_Hits = new RaycastHit[HIT_BUFFER_SIZE];
        private readonly Collider[] m_Overlaps = new Collider[HIT_BUFFER_SIZE];

        // 콜라이더 → 흐릴 렌더러 묶음. 맵은 판 중에 거의 안 바뀌어 한 번 찾으면 다시 쓴다
        // (매 프레임 GetComponentsInChildren을 부르면 배열 할당이 GC로 간다).
        private readonly Dictionary<Collider, Renderer[]> m_Groups = new Dictionary<Collider, Renderer[]>();
        private readonly List<Renderer> m_Scratch = new List<Renderer>();

        // 원본 머티리얼별 반투명 사본 — 렌더러 수와 무관하게 머티리얼 종류만큼만 만든다.
        private readonly Dictionary<Material, Material> m_FadedVariants = new Dictionary<Material, Material>();
        // 되돌리기용 원본. 키가 곧 "지금 흐려진 렌더러" 집합이다.
        private readonly Dictionary<Renderer, Material[]> m_Originals = new Dictionary<Renderer, Material[]>();

        private readonly HashSet<Renderer> m_CurrentHits = new HashSet<Renderer>();
        private readonly List<Renderer> m_Restored = new List<Renderer>();
        private readonly List<Renderer> m_NewlyFaded = new List<Renderer>();

        /// <summary>지금 반투명 처리된 렌더러 수 — QA 검증용.</summary>
        public int FadedCount => m_Originals.Count;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>QA 전용 — 지금 흐린 렌더러와 그 사본 머티리얼(셰이더·큐·투명 키워드·알파)을 한 줄로.</summary>
        public string DevDescribeFaded()
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<Renderer, Material[]> kv in m_Originals)
            {
                if (kv.Key == null) continue;
                Material m = kv.Key.sharedMaterial;
                sb.Append(kv.Key.name).Append('[');
                if (m != null)
                {
                    sb.Append(m.shader != null ? m.shader.name : "null").Append(" q=").Append(m.renderQueue)
                      .Append(" t=").Append(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                      .Append(" a=").Append(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a.ToString("F2") : "-");
                }
                sb.Append(kv.Key.enabled ? "" : " off").Append("] ");
            }
            return sb.ToString();
        }
#endif

        private void Awake()
        {
            m_Camera = GetComponent<CinemachineCamera>();
        }

        private void OnDisable()
        {
            RestoreAll();
        }

        private void OnDestroy()
        {
            RestoreAll();
            foreach (Material variant in m_FadedVariants.Values)
            {
                if (variant != null) Destroy(variant);
            }
            m_FadedVariants.Clear();
        }

        private void LateUpdate()
        {
            Transform target = m_Camera != null ? m_Camera.Follow : null;
            if (target == null)
            {
                RestoreAll();
                return;
            }

            CollectHits(transform.position, target.position);

            OcclusionFadeLogic.CollectRestored(m_Originals.Keys, m_CurrentHits, m_Restored);
            foreach (Renderer r in m_Restored) Restore(r);

            OcclusionFadeLogic.CollectNewlyFaded(m_CurrentHits, m_Originals.Keys, m_NewlyFaded);
            foreach (Renderer r in m_NewlyFaded) Fade(r);
        }

        private void CollectHits(Vector3 _cameraPos, Vector3 _feet)
        {
            m_CurrentHits.Clear();

            CastTo(_cameraPos, _feet);
            CastTo(_cameraPos, OcclusionFadeLogic.AimPoint(_feet, m_AimHeight));

            // 카메라가 이미 들어가 있거나 닿은 것 — 스피어캐스트는 시작점에서 겹친 콜라이더를 못 본다.
            int overlaps = Physics.OverlapSphereNonAlloc(_cameraPos, m_CastRadius, m_Overlaps,
                m_ObstacleLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlaps; i++) AddGroup(m_Overlaps[i]);
        }

        private void CastTo(Vector3 _cameraPos, Vector3 _point)
        {
            Vector3 origin = OcclusionFadeLogic.CastOrigin(_cameraPos, _point, m_CastRadius);
            float distance = OcclusionFadeLogic.CastDistance(_cameraPos, _point, m_CastRadius);
            if (distance <= 0f) return;

            Vector3 dir = (_point - _cameraPos).normalized;
            int count = Physics.SphereCastNonAlloc(origin, m_CastRadius, dir, m_Hits, distance,
                m_ObstacleLayers, QueryTriggerInteraction.Collide);
            // Collide인 이유(2026-08-31): 팬트리 가구(MapDresser)는 트리거 콜라이더만 든다 —
            // 이동·시야 레이는 전부 Ignore라 게임플레이에 안 잡히고, 여기서만 잡혀 페이드된다.
            for (int i = 0; i < count; i++) AddGroup(m_Hits[i].collider);
        }

        private void AddGroup(Collider _col)
        {
            if (_col == null || _col.CompareTag(m_IgnoreTag)) return;
            // 트리거는 **제 렌더러를 단 것만** 가림막이다(가구의 페이드 상자). 상점 반경·치즈 지점 같은 판정 구역은
            // 그 안에 서기만 해도 캐스트에 걸려, 넓히면 상점에 서 있는 동안 부스가 통째로 흐려진다.
            if (_col.isTrigger && _col.GetComponent<Renderer>() == null) return;

            Renderer[] group = GroupOf(_col);
            for (int i = 0; i < group.Length; i++)
            {
                // 꺼진 렌더러는 흐릴 게 없다 — 옷을 입힌 맵 박스(MapDresser)는 제 렌더러를 끄고 가구가 그림을 맡는다
                if (group[i] != null && group[i].enabled) m_CurrentHits.Add(group[i]);
            }
        }

        // 흐릴 묶음 — 콜라이더의 오브젝트와 그 아래 그림, 그게 없으면(판정 상자만 따로 단 자식) 한 단계 위 부모 아래 그림.
        // 너무 크면(맵 통째) 안 흐린다(OcclusionFadeLogic.IsFadeableGroup). 파티클은 머티리얼을 바꾸면 깨진다 — 뺀다.
        private Renderer[] GroupOf(Collider _col)
        {
            if (m_Groups.TryGetValue(_col, out Renderer[] cached)) return cached;

            CollectRenderers(_col.transform);
            Transform parent = _col.transform.parent;
            if (m_Scratch.Count == 0 && parent != null && !parent.CompareTag(m_IgnoreTag)) CollectRenderers(parent);

            Renderer[] group = OcclusionFadeLogic.IsFadeableGroup(m_Scratch.Count)
                ? m_Scratch.ToArray()
                : System.Array.Empty<Renderer>();
            m_Groups[_col] = group;
            return group;
        }

        private void CollectRenderers(Transform _root)
        {
            _root.GetComponentsInChildren(true, m_Scratch);
            m_Scratch.RemoveAll(r => r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
        }

        private void Fade(Renderer _renderer)
        {
            Material[] originals = _renderer.sharedMaterials;
            var faded = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                faded[i] = FadedVariantOf(originals[i]);
            }

            m_Originals[_renderer] = originals;
            _renderer.sharedMaterials = faded;
        }

        private void Restore(Renderer _renderer)
        {
            if (!m_Originals.TryGetValue(_renderer, out Material[] originals)) return;
            if (_renderer != null) _renderer.sharedMaterials = originals;
            m_Originals.Remove(_renderer);
        }

        private void RestoreAll()
        {
            foreach (KeyValuePair<Renderer, Material[]> kv in m_Originals)
            {
                if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            }
            m_Originals.Clear();
            m_CurrentHits.Clear();
        }

        // URP Lit을 Transparent로 전환한 사본. 원본당 1개만 만들어 캐시한다.
        private Material FadedVariantOf(Material _source)
        {
            if (_source == null) return null;
            // URP Lit 계열(_Surface가 있는 셰이더)만 반투명으로 바꿀 줄 안다 — 다른 셰이더(글자·셰이더 그래프)는 그대로 둔다
            if (!_source.HasProperty("_Surface")) return _source;
            if (m_FadedVariants.TryGetValue(_source, out Material cached) && cached != null) return cached;

            var variant = new Material(_source) { name = _source.name + " (Faded)" };
            MakeTransparent(variant, m_FadedAlpha);
            m_FadedVariants[_source] = variant;
            return variant;
        }

        // URP Lit의 Surface Type을 코드로 Transparent로 바꾸는 정해진 절차.
        // 셰이더 키워드·블렌드·ZWrite·렌더큐를 전부 손대야 실제로 반투명해진다.
        private static void MakeTransparent(Material _material, float _alpha)
        {
            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);
            if (_material.HasProperty("_Blend")) _material.SetFloat("_Blend", 0f); // Alpha
            if (_material.HasProperty("_SrcBlend")) _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (_material.HasProperty("_DstBlend")) _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (_material.HasProperty("_ZWrite")) _material.SetFloat("_ZWrite", 0f);

            _material.SetShaderPassEnabled("ShadowCaster", false);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.DisableKeyword("_ALPHATEST_ON");
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            foreach (string colorProp in new[] { "_BaseColor", "_Color" })
            {
                if (!_material.HasProperty(colorProp)) continue;
                Color c = _material.GetColor(colorProp);
                c.a = _alpha;
                _material.SetColor(colorProp, c);
            }
        }
    }
}
