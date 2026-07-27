using CTC.Runtime.Combat;
using CTC.Runtime.Economy;
using CTC.Runtime.Lobby;
using UnityEngine;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// 치즈 수입 짤랑 소리 — 버는 쥐 위치에서 3D(~12m)로 전 클라 재생.
    /// "번다 = 소리 낸다 = 위치 노출": 고양이 귀 사냥을 성립시키는 정보 노브.
    /// 지갑은 NetworkVariable이라 어느 클라에서든 증가를 감지 — RPC 불필요.
    /// </summary>
    public class FarmingSfx : MonoBehaviour
    {
        private const float AUDIBLE_DISTANCE = 20f;
        private const float VOLUME = 0.5f;

        private CheeseWallet m_Wallet;
        private RoleTag m_Tag;
        private int m_LastCheese = -1;

        private void Awake()
        {
            m_Wallet = GetComponent<CheeseWallet>();
            m_Tag = GetComponent<RoleTag>();
        }

        private void Update()
        {
            if (m_Wallet == null || !m_Wallet.IsSpawned)
            {
                m_LastCheese = -1;
                return;
            }
            if (m_Tag == null || m_Tag.Role != RoleId.Mouse) return; // 고양이 장난기 적립은 무음

            int cheese = m_Wallet.Cheese;
            if (m_LastCheese >= 0 && cheese > m_LastCheese)
            {
                SfxPlayer.PlayAt(SfxId.CheeseTick, transform.position, VOLUME, AUDIBLE_DISTANCE);
            }
            m_LastCheese = cheese;
        }
    }
}
