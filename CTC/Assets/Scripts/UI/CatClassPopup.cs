using System;
using CTC.Runtime.Combat;
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
    /// 전직 선택 창 (v3.9). 강화를 **둘 다 끝까지** 올린 고양이에게만 뜬다.
    ///
    /// MVC의 View + Controller다 — 상태는 `CatClass`가 갖고, 여기는 보여 주고 고른 값을
    /// 서버로 넘긴다. 서버가 조건을 다시 보므로 이 창을 속여도 전직되지 않는다.
    ///
    /// **폴링하지 않는다**(규칙 3) — 강화 중첩 스트림 둘을 합쳐 구독하고,
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

            var pounce = _player != null ? _player.GetComponent<CatPounce>() : null;
            var move = _player != null ? _player.GetComponent<PlayerMovement>() : null;
            if (m_Class == null || pounce == null || move == null)
            {
                Show(false);
                return;
            }

            // 강화 둘 + 전직 상태가 하나라도 바뀌면 다시 판단한다.
            m_Binding.Disposable = Observable
                .Merge(
                    pounce.DamageStacksChanged.Select(_ => Unit.Default),
                    move.SpeedStacksChanged.Select(_ => Unit.Default),
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
