using System.Threading;
using CTC.Runtime.Core;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 칼에 맞아 굳은 **내 화면** 가장자리가 하얗게 선다(2026-09-19, 기획 hero-sword §4). 세기 규칙은 <see cref="StunVignetteLogic"/>.
    ///
    /// 로컬 몸의 <see cref="Combat.Stun.Applied"/>(복제값이 바뀌는 자리 — 전 클라가 도는 길)를 구독한다. 남의 스턴에는 반응하지 않는다.
    /// 테두리 그림은 코드로 한 번 굽는다(가운데가 빈 흰 원형 그러데이션) — 에셋 파일이 따로 없다.
    /// </summary>
    public class StunVignette : MonoBehaviour
    {
        private const int TEXTURE_SIZE = 128;

        [SerializeField] private Image m_Image;

        private Combat.Stun m_Stun;
        private CancellationTokenSource m_Cts;
        private Texture2D m_Texture;
        private Sprite m_Sprite;

        /// <summary>QA: 지금 테두리 세기(알파).</summary>
        public float Level => m_Image != null ? m_Image.color.a : 0f;

        private void Start()
        {
            if (m_Image == null) return;
            m_Image.sprite = BuildSprite();
            SetLevel(0f);
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayer).AddTo(this);
        }

        private void OnDestroy()
        {
            Unbind();
            Cancel();
            if (m_Sprite != null) Destroy(m_Sprite);
            if (m_Texture != null) Destroy(m_Texture);
        }

        private void OnLocalPlayer(NetworkObject _player)
        {
            Unbind();
            m_Stun = _player != null ? _player.GetComponent<Combat.Stun>() : null;
            if (m_Stun != null) m_Stun.Applied += OnApplied;
        }

        private void Unbind()
        {
            if (m_Stun != null) m_Stun.Applied -= OnApplied;
            m_Stun = null;
        }

        private void OnApplied(float _until)
        {
            Cancel();
            m_Cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            RunAsync(m_Cts.Token).Forget();
        }

        private async UniTaskVoid RunAsync(CancellationToken _token)
        {
            float hit = Time.unscaledTime;
            while (m_Stun != null && m_Stun.Active)
            {
                SetLevel(StunVignetteLogic.WhileStunned(Time.unscaledTime - hit));
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;
            }
            float from = Level;
            float end = Time.unscaledTime;
            while (true)
            {
                float level = StunVignetteLogic.AfterStun(from, Time.unscaledTime - end);
                SetLevel(level);
                if (level <= 0f) break;
                bool cancelled = await UniTask.NextFrame(_token).SuppressCancellationThrow();
                if (cancelled) return;
            }
        }

        private void SetLevel(float _alpha)
        {
            if (m_Image == null) return;
            Color c = m_Image.color;
            c.a = _alpha;
            m_Image.color = c;
            bool on = _alpha > 0f;
            if (m_Image.enabled != on) m_Image.enabled = on;
        }

        private Sprite BuildSprite()
        {
            m_Texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "StunVignette",
            };
            var pixels = new Color32[TEXTURE_SIZE * TEXTURE_SIZE];
            float half = (TEXTURE_SIZE - 1) * 0.5f;
            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    // 모서리까지 1이 넘게 — 네 귀퉁이가 가장 진하다
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
                    byte a = (byte)Mathf.RoundToInt(255f * StunVignetteLogic.EdgeAlpha(r));
                    pixels[y * TEXTURE_SIZE + x] = new Color32(255, 255, 255, a);
                }
            }
            m_Texture.SetPixels32(pixels);
            m_Texture.Apply(false, true);
            m_Sprite = Sprite.Create(m_Texture, new Rect(0, 0, TEXTURE_SIZE, TEXTURE_SIZE), new Vector2(0.5f, 0.5f));
            return m_Sprite;
        }

        private void Cancel()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = null;
        }
    }
}
