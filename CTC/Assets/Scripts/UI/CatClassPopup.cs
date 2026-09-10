using System;
using CTC.Runtime.Economy;
using CTC.Runtime.Core;
using CTC.Runtime.Player;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CTC.Runtime.UI
{
    /// <summary>
    /// 전직 선택 창 (v3.9). **누적 획득 장난기**가 문턱을 넘은 고양이에게 뜬다(2026-09-09 계획 3 Task 5).
    ///
    /// MVC의 View + Controller다 — 상태는 `CatClass`가 갖고, 여기는 보여 주고 고른 값을
    /// 서버로 넘긴다. 서버가 조건을 다시 보므로 이 창을 속여도 전직되지 않는다.
    ///
    /// **폴링하지 않는다**(규칙 3) — 지갑의 누적 스트림과 전직 상태를 합쳐 구독하고,
    /// 로컬 플레이어가 바뀌면 구독을 갈아 끼운다.
    /// </summary>
    public class CatClassPopup : MonoBehaviour
    {
        [SerializeField] private GameObject m_Root;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private Button[] m_Buttons;   // Fire · Wind · Light · Dark 순서

        private static readonly CatClassId[] CHOICES =
        {
            CatClassId.Fire, CatClassId.Wind, CatClassId.Light, CatClassId.Dark,
        };

        private readonly SerialDisposable m_Binding = new SerialDisposable();
        private CatClass m_Class;

        /// <summary>지금 화면에 떠 있는가 — QA가 「조건을 채우면 창이 뜬다」를 눈이 아니라 값으로 잰다.</summary>
        public bool IsOpen => m_Root != null && m_Root.activeSelf;

        private void Start()
        {
            if (m_Root != null) m_Root.SetActive(false);

            for (int i = 0; i < m_Buttons.Length && i < CHOICES.Length; i++)
            {
                CatClassId choice = CHOICES[i];
                m_Buttons[i].OnClickAsObservable()
                    .Subscribe(_ => OnClickChoose(choice))
                    .AddTo(this);
            }

            LocalPlayerBinder.LocalPlayer
                .Subscribe(Rebind)
                .AddTo(this);

            m_Binding.AddTo(this);
        }

        private void Rebind(NetworkObject _player)
        {
            m_Binding.Disposable = null;
            m_Class = _player != null ? _player.GetComponent<CatClass>() : null;

            var wallet = _player != null ? _player.GetComponent<CheeseWallet>() : null;
            if (m_Class == null || wallet == null)
            {
                Show(false);
                return;
            }

            // 누적 획득 + 전직 상태가 하나라도 바뀌면 다시 판단한다.
            m_Binding.Disposable = Observable
                .Merge(
                    wallet.Earned.Select(_ => Unit.Default),
                    m_Class.ClassChanged.Select(_ => Unit.Default))
                .Subscribe(_ => Show(m_Class.CanAdvance));
        }

        private void Show(bool _visible)
        {
            if (m_Root == null || m_Root.activeSelf == _visible) return;

            m_Root.SetActive(_visible);
            // 창이 뜨면 커서를 풀어 줘야 클릭할 수 있다 — 옵션 창과 같은 규약(CursorPolicyLogic).
            UiFocus.SetModalOpen(_visible);

            if (_visible && m_Title != null)
            {
                m_Title.text = "전직 — 하나를 고른다 (되돌릴 수 없다)";
            }
        }

        private void OnClickChoose(CatClassId _choice)
        {
            if (m_Class == null) return;

            m_Class.ChooseRpc(_choice);
            Show(false);
        }
    }
}
