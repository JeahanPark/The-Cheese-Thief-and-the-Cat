using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 용사 무적 테두리(2026-09-19, 기획 hero-sword §5-2) — 무적이 켜 있는 동안 몸 가장자리가 흰빛으로 빛나고,
    /// 끝나면 0.3초에 걸쳐 꺼진다. 켜는 순간의 흰 빛 확산(<c>FxId.HeroInvincible</c>)과 짝이다.
    ///
    /// **§11(이펙트는 한 번 터지고 끝난다)의 예외다** — 무적은 상대도 알아야 하는 상태라(때려도 안 들어간다) 지속 표시가 필요하다.
    /// 대신 **벽 너머로 새지 않는다**: 셰이더(<c>CTC/HeroRim</c>)가 깊이 검사를 한다.
    ///
    /// 몸 <see cref="SkinnedMeshRenderer"/>의 머티리얼 칸 맨 뒤에 테두리 머티리얼을 **켜 있는 동안만** 덧붙인다
    /// (같은 메시를 한 번 더 그린다). LOD 넷을 전부 굴린다(함정 19). 소품(칼·총·모자)은 MeshRenderer라 안 걸린다.
    /// 통신 0 — <see cref="HeroInvincible.Started"/>(복제값이 바뀌는 자리)를 전 클라가 각자 본다.
    /// </summary>
    [RequireComponent(typeof(HeroInvincible))]
    public class HeroInvincibleVisual : MonoBehaviour
    {
        /// <summary>끝날 때 꺼지는 시간(기획 §5-2).</summary>
        public const float FADE_OUT_SECONDS = 0.3f;

        /// <summary>켜질 때 차오르는 시간 — 켜는 순간은 흰 빛 확산이 따로 알린다.</summary>
        public const float FADE_IN_SECONDS = 0.1f;

        private static readonly int INTENSITY_ID = Shader.PropertyToID("_Intensity");

        [SerializeField] private Material m_RimMaterial;   // HeroPropBaker가 배선한다(M_HeroRim)

        private HeroInvincible m_Invincible;
        private SkinnedMeshRenderer[] m_Bodies = System.Array.Empty<SkinnedMeshRenderer>();
        private readonly List<(SkinnedMeshRenderer body, Material[] original)> m_Attached =
            new List<(SkinnedMeshRenderer, Material[])>();
        private MaterialPropertyBlock m_Block;   // 순수 C# — 규칙 9 대상 아님
        private CancellationTokenSource m_Cts;
        private float m_Base = 1f;
        private float m_Level;

        /// <summary>QA: 지금 테두리 세기(0 = 꺼짐, 1 = 다 켜짐).</summary>
        public float RimLevel => m_Level;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_Invincible = GetComponent<HeroInvincible>();
            m_Bodies = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (m_RimMaterial != null && m_RimMaterial.HasProperty(INTENSITY_ID)) m_Base = m_RimMaterial.GetFloat(INTENSITY_ID);
            if (m_Invincible != null) m_Invincible.Started += OnStarted;
        }

        private void OnDestroy()
        {
            if (m_Invincible != null) m_Invincible.Started -= OnStarted;
            Cancel();
        }

        private void OnDisable()
        {
            Cancel();
            Detach();
        }

        private void OnStarted()
        {
            if (m_RimMaterial == null || m_Invincible == null) return;
            Cancel();
            m_Cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            GlowAsync(m_Cts.Token).Forget();
        }

        // 켜 있는 동안(복제값에서 계산한 남은 시간) 빛나고, 다 끝나면 서서히 끈다.
        private async UniTaskVoid GlowAsync(CancellationToken _token)
        {
            Attach();
            float start = Time.time;
            while (m_Invincible != null && m_Invincible.ActiveRemaining > 0f)
            {
                SetLevel(Mathf.Clamp01((Time.time - start) / FADE_IN_SECONDS));
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;
            }

            float from = m_Level;
            float fadeStart = Time.time;
            while (true)
            {
                float p = Mathf.Clamp01((Time.time - fadeStart) / FADE_OUT_SECONDS);
                SetLevel(from * (1f - p));
                if (p >= 1f) break;
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;
            }
            Detach();
        }

        private void Attach()
        {
            if (m_Attached.Count > 0) return;
            foreach (SkinnedMeshRenderer body in m_Bodies)
            {
                if (body == null) continue;
                Material[] original = body.sharedMaterials;
                m_Attached.Add((body, original));
                var withRim = new Material[original.Length + 1];
                original.CopyTo(withRim, 0);
                withRim[original.Length] = m_RimMaterial;
                body.sharedMaterials = withRim;
            }
            SetLevel(0f);
        }

        private void Detach()
        {
            foreach ((SkinnedMeshRenderer body, Material[] original) in m_Attached)
            {
                if (body != null) body.sharedMaterials = original;
            }
            m_Attached.Clear();
            m_Level = 0f;
        }

        private void SetLevel(float _level)
        {
            m_Level = _level;
            foreach ((SkinnedMeshRenderer body, Material[] original) in m_Attached)
            {
                if (body == null) continue;
                int index = original.Length;   // 덧붙인 칸 — 그 칸에만 세기를 준다(몸 머티리얼은 그대로)
                body.GetPropertyBlock(m_Block, index);
                m_Block.SetFloat(INTENSITY_ID, m_Base * _level);
                body.SetPropertyBlock(m_Block, index);
            }
        }

        private void Cancel()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
        }
    }
}
