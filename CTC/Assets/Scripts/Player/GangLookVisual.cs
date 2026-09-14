using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 깡패 쥐의 **보이는 것**(몽둥이·모자·눈썹·화난 눈)과 **변신 연출**(2026-09-14). 켤지 말지는 <see cref="GangLook"/>이 정하고,
    /// 여기는 그걸 어떻게 보여 줄지만 한다(규칙 7).
    ///
    /// - <see cref="ShowInstant"/> — 늦게 들어온 사람·판 초기화처럼 **이미 그 상태인** 경우. 연출 없이 바로.
    /// - <see cref="PlayTransform"/> — 깡패판이 **지금 막** 열렸다. <c>GangTransform</c> 클립을 틀고,
    ///   시간표(<see cref="GangTransformLogic"/>)대로 눈썹을 켜고 몽둥이·모자를 **알파로 스르르** 띄운다 — 총 뽑기(<see cref="MouseGunVisual"/>)와 같은 수법이다.
    ///
    /// 알파는 <see cref="MaterialPropertyBlock"/>으로 민다 — 머티리얼 사본을 만들지 않는다. 그래서 소품 머티리얼은
    /// **반투명으로 구워져 있어야 한다**(<c>GangPropBaker</c>) — URP/Lit은 불투명일 때 알파를 조용히 무시한다.
    /// 전 클라가 각자 튼다 — <see cref="GangLook"/>이 복제값을 구독해 부르므로 통신이 없다.
    /// </summary>
    public class GangLookVisual : MonoBehaviour
    {
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
        private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

        private PlayerAnimationDriver m_Driver;
        private GangAngryFace m_Face;
        private GameObject m_Club;
        private GameObject m_Hat;
        private Renderer m_ClubRenderer;
        private Renderer m_HatRenderer;
        private Color m_ClubColor = Color.white;
        private Color m_HatColor = Color.white;
        private readonly List<GameObject> m_Brows = new List<GameObject>();

        private MaterialPropertyBlock m_Block;           // 순수 C# — 규칙 9 대상 아님
        private CancellationTokenSource m_Cts;

        private float m_ClubAlpha;
        private float m_HatAlpha;
        private bool m_Transforming;

        public float ClubAlpha => m_ClubAlpha;
        public float HatAlpha => m_HatAlpha;
        /// <summary>변신 연출이 도는 중.</summary>
        public bool Transforming => m_Transforming;

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_Driver = GetComponent<PlayerAnimationDriver>();
            m_Face = GetComponent<GangAngryFace>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == GangLook.CLUB_NAME) m_Club = t.gameObject;
                else if (t.name == GangLook.HAT_NAME) m_Hat = t.gameObject;
                else if (t.name.StartsWith(GangLook.BROW_PREFIX)) m_Brows.Add(t.gameObject);
            }
            m_ClubRenderer = CacheRenderer(m_Club, ref m_ClubColor);
            m_HatRenderer = CacheRenderer(m_Hat, ref m_HatColor);
            Apply(0f, 0f, false);
        }

        private void OnDisable() => Cancel();

        /// <summary>연출 없이 바로 켜거나 끈다.</summary>
        public void ShowInstant(bool _on)
        {
            Cancel();
            Apply(_on ? 1f : 0f, _on ? 1f : 0f, _on);
        }

        /// <summary>변신 — 클립을 틀고 시간표대로 소품을 띄운다. 도중에 끄면(<see cref="ShowInstant"/>) 끊긴다.</summary>
        public void PlayTransform()
        {
            // 하던 행동은 결과를 그대로 성공시키고 변신한다(사용자 결정) — 시간을 들여 끝나는 것만 남은 결과를 즉시 적용한다.
            foreach (ICompletableAction action in GetComponents<ICompletableAction>()) action.CompleteNow();
            // 총은 즉시 넣는다 — 깡패는 총을 못 쓴다. 쏘던 발은 이미 서버에서 판정이 끝났다.
            var gun = GetComponent<MouseGunVisual>();
            if (gun != null) gun.HideNow();

            Cancel();
            Apply(0f, 0f, false);
            if (m_Driver != null) m_Driver.PlayOneShot(AnimationStateLogic.GangTransform, GangTransformLogic.SECONDS);

            m_Cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            TransformAsync(m_Cts.Token).Forget();
        }

        private async UniTaskVoid TransformAsync(CancellationToken _token)
        {
            m_Transforming = true;
            float start = Time.time;
            while (true)
            {
                float elapsed = Time.time - start;
                Apply(GangTransformLogic.ClubAlpha(elapsed), GangTransformLogic.HatAlpha(elapsed), GangTransformLogic.BrowsOn(elapsed));
                if (GangTransformLogic.Done(elapsed)) break;
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;   // 끊은 쪽이 이미 상태를 정했다
            }
            m_Transforming = false;
            if (this != null) Apply(1f, 1f, true);
        }

        private void Cancel()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
            m_Transforming = false;
        }

        private void Apply(float _clubAlpha, float _hatAlpha, bool _brows)
        {
            m_ClubAlpha = _clubAlpha;
            m_HatAlpha = _hatAlpha;
            Push(m_Club, m_ClubRenderer, m_ClubColor, _clubAlpha);
            Push(m_Hat, m_HatRenderer, m_HatColor, _hatAlpha);
            foreach (GameObject brow in m_Brows)
            {
                if (brow != null && brow.activeSelf != _brows) brow.SetActive(_brows);
            }
            if (m_Face != null) m_Face.enabled = _brows;
        }

        // 알파를 먼저 넣고 켠다 — 켠 다음에 넣으면 직전 알파로 한 프레임 그려진다(MouseGunVisual과 같다).
        private void Push(GameObject _go, Renderer _renderer, Color _base, float _alpha)
        {
            bool render = GunFadeLogic.ShouldRender(_alpha);
            if (_renderer != null && render)
            {
                _renderer.GetPropertyBlock(m_Block);
                Color tint = _base;
                tint.a = _alpha;
                m_Block.SetColor(BASE_COLOR_ID, tint);
                m_Block.SetColor(COLOR_ID, tint);
                _renderer.SetPropertyBlock(m_Block);
            }
            if (_go != null && _go.activeSelf != render) _go.SetActive(render);
        }

        private static Renderer CacheRenderer(GameObject _go, ref Color _color)
        {
            if (_go == null) return null;
            Renderer r = _go.GetComponentInChildren<Renderer>(true);
            if (r == null) return null;
            Material m = r.sharedMaterial;
            if (m != null)
            {
                if (m.HasProperty(BASE_COLOR_ID)) _color = m.GetColor(BASE_COLOR_ID);
                else if (m.HasProperty(COLOR_ID)) _color = m.GetColor(COLOR_ID);
            }
            return r;
        }
    }
}
