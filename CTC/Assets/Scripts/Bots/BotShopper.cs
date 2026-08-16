#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Economy;
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Bots
{
    /// <summary>
    /// 봇의 **지갑 쓰기** (2026-08-12에 `BotAgent`에서 분리).
    ///
    /// 사는 순서 자체는 순수 로직(`MouseBotLogic.NextPurchase`)이 정하고, 여기서는
    /// 사람이 쓰는 입구(`ShopController`)로 밀어 넣기만 한다.
    ///
    /// **자리 고르기는 흉내 내지 않는다** — 창고를 어디에 둘지는 사람의 판단이라
    /// 봇이 흉내 내면 그 순간 재는 대상이 밸런스가 아니라 봇의 실력이 된다. 제자리에 둔다.
    /// </summary>
    public class BotShopper
    {
        private readonly Transform m_Body;
        private readonly ShopController m_Shop;
        private readonly CheeseWallet m_Wallet;
        private readonly CatClass m_Class;

        private bool m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress;

        public BotShopper(Transform _body, ShopController _shop, CheeseWallet _wallet, CatClass _class)
        {
            m_Body = _body;
            m_Shop = _shop;
            m_Wallet = _wallet;
            m_Class = _class;
        }

        /// <summary>
        /// 도주 중 요새 — 즉발 방패(2026-08-15). 내 위치 중심에 즉시 서고 고양이는 밀려난다.
        /// 지갑이 모자라면 상점이 거른다. NextPurchase의 요새 구매(파밍 중 제자리)와는 별개다.
        /// </summary>
        public void BuyFortressShield()
        {
            if (m_Shop != null) m_Shop.Buy(ShopItemId.Fortress);
        }

        /// <summary>쥐 — 살 수 있으면 산다. 설치물은 제자리에 둔다.</summary>
        public void BuyForMouse()
        {
            if (m_Shop == null || m_Wallet == null) return;

            ShopItemId next = MouseBotLogic.NextPurchase(
                m_Wallet.CheeseValue, m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress);
            if (next == ShopItemId.None) return;

            if (ShopLogic.IsPlaceable(next)) m_Shop.BuyAt(next, m_Body.position);
            else m_Shop.Buy(next);

            if (next == ShopItemId.Dash) m_BoughtDash = true;
            else if (next == ShopItemId.Slingshot) m_BoughtGun = true;
            else if (next == ShopItemId.Warehouse) m_BoughtWarehouse = true;
            else if (next == ShopItemId.Fortress) m_BoughtFortress = true;
        }

        /// <summary>
        /// 고양이 — 공격·이속을 번갈아 올린다. 둘 다 만렙이면 전직.
        /// **불**을 고르는 건 판단이 필요 없는 유일한 전직이라서다.
        /// </summary>
        public void BuyForCat()
        {
            if (m_Shop == null || m_Wallet == null) return;

            if (m_Class != null && m_Class.CanAdvance && m_Class.Value == CatClassId.None)
            {
                m_Class.ChooseRpc(CatClassId.Fire);
                return;
            }

            if (m_Shop.CanApply(ShopItemId.CatAttack)
                && m_Wallet.CheeseValue >= ShopLogic.Find(ShopItemId.CatAttack).Cost)
            {
                m_Shop.Buy(ShopItemId.CatAttack);
                return;
            }

            if (m_Shop.CanApply(ShopItemId.CatSpeed)
                && m_Wallet.CheeseValue >= ShopLogic.Find(ShopItemId.CatSpeed).Cost)
            {
                m_Shop.Buy(ShopItemId.CatSpeed);
            }
        }
    }
}
#endif
