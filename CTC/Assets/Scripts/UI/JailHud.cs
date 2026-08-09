using CTC.Runtime.Core;
using CTC.Runtime.Match;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 감옥 표시 (v3.8 4번) — MVC의 View.
    ///
    /// 갇힌 본인에게는 **남은 시간**이 전부다. 그 숫자가 "구조가 올까"라는 긴장의 전부이고,
    /// 0이 되면 진짜 탈락이라 카운트다운 없이는 갇힌 시간이 그냥 지루해진다.
    ///
    /// 규칙 3(폴링 금지)대로 `JailedTag`의 스트림을 구독한다. 로컬 플레이어가 바뀌면
    /// `SerialDisposable`이 이전 구독을 끊는다(`WalletHud`와 같은 꼴).
    /// </summary>
    public class JailHud : MonoBehaviour
    {
        private static readonly Color COLOR_URGENT = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color COLOR_CALM = new Color(0.95f, 0.9f, 0.55f);

        // 이 아래로는 빨갛게 — 구조가 늦었다는 신호
        private const float URGENT_SECONDS = 10f;

        [SerializeField] private TMP_Text m_Text;

        private readonly SerialDisposable m_JailedSub = new SerialDisposable();
        private readonly SerialDisposable m_RemainingSub = new SerialDisposable();

        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_JailedSub.AddTo(this);
            m_RemainingSub.AddTo(this);

            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            var tag = _player != null ? _player.GetComponent<JailedTag>() : null;
            if (tag == null)
            {
                // 고양이이거나 관전 중 — 표시할 게 없다
                m_JailedSub.Disposable = null;
                m_RemainingSub.Disposable = null;
                m_Text.gameObject.SetActive(false);
                return;
            }

            m_JailedSub.Disposable = tag.JailedChanged.Subscribe(jailed =>
            {
                m_Text.gameObject.SetActive(jailed);
                m_RemainingSub.Disposable = jailed
                    ? tag.RemainingChanged.Subscribe(Redraw)
                    : null;
            });
        }

        private void Redraw(float _remaining)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, _remaining));
            m_Text.text = $"갇혔다 — {seconds}초\n동료가 감옥으로 오면 풀려난다";
            m_Text.color = _remaining <= URGENT_SECONDS ? COLOR_URGENT : COLOR_CALM;
        }
    }
}
