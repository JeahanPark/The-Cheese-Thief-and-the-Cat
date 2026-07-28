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
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class OccluderFader : MonoBehaviour
    {
        private const int HIT_BUFFER_SIZE = 16;

        [SerializeField] private float m_CastRadius = 0.25f;
        [SerializeField, Range(0f, 1f)] private float m_FadedAlpha = 0.25f;
        [SerializeField] private LayerMask m_ObstacleLayers = 1; // Default — 바닥은 Ignore Raycast(2)라 자동 제외
        [SerializeField] private string m_IgnoreTag = "Player";  // 플레이어 몸(모델 자식 포함)은 가림막이 아니다

        private CinemachineCamera m_Camera;
        private readonly RaycastHit[] m_Hits = new RaycastHit[HIT_BUFFER_SIZE];

        // 원본 머티리얼별 반투명 사본 — 렌더러 수와 무관하게 머티리얼 종류만큼만 만든다.
        private readonly Dictionary<Material, Material> m_FadedVariants = new Dictionary<Material, Material>();
        // 되돌리기용 원본. 키가 곧 "지금 흐려진 렌더러" 집합이다.
        private readonly Dictionary<Renderer, Material[]> m_Originals = new Dictionary<Renderer, Material[]>();

        private readonly HashSet<Renderer> m_CurrentHits = new HashSet<Renderer>();
        private readonly List<Renderer> m_Restored = new List<Renderer>();
        private readonly List<Renderer> m_NewlyFaded = new List<Renderer>();

        /// <summary>지금 반투명 처리된 렌더러 수 — QA 검증용.</summary>
        public int FadedCount => m_Originals.Count;

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

        private void CollectHits(Vector3 _cameraPos, Vector3 _targetPos)
        {
            m_CurrentHits.Clear();

            Vector3 origin = OcclusionFadeLogic.CastOrigin(_cameraPos, _targetPos, m_CastRadius);
            float distance = OcclusionFadeLogic.CastDistance(_cameraPos, _targetPos, m_CastRadius);
            if (distance <= 0f) return;

            Vector3 dir = (_targetPos - _cameraPos).normalized;
            int count = Physics.SphereCastNonAlloc(origin, m_CastRadius, dir, m_Hits, distance,
                m_ObstacleLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = m_Hits[i].collider;
                if (col == null || col.CompareTag(m_IgnoreTag)) continue;

                var renderer = col.GetComponent<Renderer>();
                if (renderer != null) m_CurrentHits.Add(renderer);
            }
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
