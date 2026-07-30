using CTC.Runtime.Core;
using CTC.Runtime.Lobby;
using R3;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 자기 지갑 잔액 HUD (MVC의 View). 쥐 `치즈:` / 고양이 `장난기:`.
    ///
    /// 개편 3b(2026-07-31): Update 폴링 → R3 구독. 매 프레임 로컬 플레이어를 찾고
    /// 문자열을 새로 만들던 것을 **값이 바뀔 때만** 하도록 바꿨다.
    /// 관전자(로컬 플레이어 null)는 스트림이 null을 흘려 자동으로 숨겨진다.
    /// </summary>
    public class WalletHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Text;

        private readonly SerialDisposable m_WalletSub = new SerialDisposable();

        private void Start()
        {
            m_Text.gameObject.SetActive(false);
            m_WalletSub.AddTo(this);

            LocalPlayerBinder.LocalPlayer.Subscribe(OnLocalPlayerChanged).AddTo(this);
        }

        // 플레이어가 바뀔 때마다 지갑 구독을 통째로 갈아 끼운다(SerialDisposable이 이전 것을 끊는다)
        private void OnLocalPlayerChanged(NetworkObject _player)
        {
            if (_player == null)
            {
                m_WalletSub.Disposable = null;
                m_Text.gameObject.SetActive(false);
                return;
            }

            var wallet = _player.GetComponent<CheeseWallet>();
            var role = _player.GetComponent<Combat.RoleTag>();
            if (wallet == null || role == null)
            {
                m_Text.gameObject.SetActive(false);
                return;
            }

            m_Text.gameObject.SetActive(true);
            string label = ShopLogic.CurrencyNameOf(role.Role);
            m_WalletSub.Disposable = wallet.Cheese.Subscribe(c => m_Text.text = $"{label}: {c}");
        }
    }
}
