using CTC.Runtime.Core;
using CTC.Runtime.Economy;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 재화를 벌면 우상단 숫자 **옆에 「+N」**이 떴다 사라진다(2026-09-20 사용자 선택 A안) — View.
    ///
    /// 채굴은 잘게 여러 번 들어와서 한 번에 하나씩 띄우면 숫자가 깜빡이기만 한다 — 그래서
    /// <see cref="CheeseGainLogic"/>이 합치는 창(1.2초) 안의 수입을 **한 숫자로 합친다.**
    /// **쓴 돈은 안 띄운다** — 상점이 값을 이미 보여 주고, 빨간 숫자가 같이 뜨면 벌었는지 썼는지가 한눈에 안 읽힌다.
    /// 고양이의 장난기도 같은 자리에 같은 규칙으로 뜬다(재화 이름만 <see cref="StatusHud"/>가 쓴다).
    /// </summary>
    public class CheeseGainPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Label;

        private readonly SerialDisposable m_WalletSub = new SerialDisposable();
        private int m_LastCheese = -1;   // 직전 잔액 — 처음 붙은 순간은 비교하지 않는다(가진 돈이 수입으로 보이면 안 된다)
        private int m_Shown;
        private float m_LastGainAt = float.NegativeInfinity;

        /// <summary>QA — 지금 떠 있는 글("+5"·빈 글자).</summary>
        public string DevText => m_Label != null ? m_Label.text : "";

        /// <summary>QA — 지금 떠 있는 정도(0~1). 라벨이 없으면 -1.</summary>
        public float DevAlpha => m_Label != null ? m_Label.color.a : -1f;

        private void Start()
        {
            m_WalletSub.AddTo(this);
            Hide();
            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            m_LastCheese = -1;
            m_Shown = 0;
            Hide();

            var wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;
            m_WalletSub.Disposable = wallet != null ? wallet.Cheese.Subscribe(OnCheeseChanged) : null;
        }

        private void OnCheeseChanged(int _cheese)
        {
            int gain = m_LastCheese >= 0 ? _cheese - m_LastCheese : 0;
            m_LastCheese = _cheese;
            if (gain <= 0) return;

            m_Shown = CheeseGainLogic.Merge(m_Shown, gain, Time.unscaledTime - m_LastGainAt);
            m_LastGainAt = Time.unscaledTime;
            if (m_Label != null) m_Label.text = CheeseGainLogic.Text(m_Shown);
        }

        private void Update()
        {
            if (m_Label == null || m_Shown <= 0) return;

            float since = Time.unscaledTime - m_LastGainAt;
            float alpha = CheeseGainLogic.Alpha(since);
            if (alpha <= 0f)
            {
                m_Shown = 0;
                Hide();
                return;
            }

            Color color = m_Label.color;
            color.a = alpha;
            m_Label.color = color;

            float scale = CheeseGainLogic.Scale(since);
            m_Label.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Hide()
        {
            if (m_Label == null) return;
            m_Label.text = "";
            m_Label.rectTransform.localScale = Vector3.one;
            Color color = m_Label.color;
            color.a = 1f;
            m_Label.color = color;
        }
    }
}
