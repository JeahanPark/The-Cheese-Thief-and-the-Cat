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
    ///
    /// 계획 3(2026-09-08): **봇도 상점까지 걸어간다.** 서버의 봇 면제(IsBotExempt)를 지웠으니
    /// 여기서 민 구매는 상점 밖이면 거절된다 — 어디로 갈지는 행동 트리의 쇼핑 가지가 정하고
    /// (<see cref="BotAgent.TryGetShop"/>), 여기는 "도착했으면 산다"만 맡는다.
    /// 그래서 계측치에 **상점 이동 비용이 들어 있다**.
    /// </summary>
    public class BotShopper
    {
        private readonly Transform m_Body;
        private readonly ShopController m_Shop;
        private readonly CheeseWallet m_Wallet;
        private readonly CatClass m_Class;

        // 칸 상한(TechSlotCap) 판단에 쓴다 — 몇 칸 찼는지는 여기서 세지 않고 Model이 갖는다.
        private readonly TechSlots m_Slots;

        private bool m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress;
        private bool m_BoughtShield, m_BoughtPouch;
        private bool m_PendingWarehousePlace;
        private int m_WarehouseCountBeforePlace;

        public BotShopper(Transform _body, ShopController _shop, CheeseWallet _wallet, CatClass _class)
        {
            m_Body = _body;
            m_Shop = _shop;
            m_Wallet = _wallet;
            m_Class = _class;
            m_Slots = _shop != null ? _shop.GetComponent<TechSlots>() : null;
        }

        /// <summary>
        /// 도주 중 요새 — 즉발 방패(2026-08-15). 내 위치 중심에 즉시 서고 고양이는 밀려난다.
        /// 지갑이 모자라면 상점이 거른다. NextPurchase의 요새 구매(파밍 중 제자리)와는 별개다.
        /// </summary>
        public void BuyFortressShield()
        {
            // 설치권이 있고 설치비가 있으면 즉발(서버가 거른다).
            if (m_Shop != null && m_Shop.CanUse(TechId.Fortress) == UseDeny.None) m_Shop.Use(TechId.Fortress);
        }

        /// <summary>
        /// 지금 사려는 것. 없으면 <c>None</c>.
        ///
        /// 행동 트리가 **상점에 갈지를 정하려면** 거기 도착하기 전에 살 것을 알아야 한다 —
        /// 그래서 구매 순서 판단만 따로 뽑아 낸다. 오늘 품절인 줄은 여기서 이미 빠져 있다 —
        /// 모르면 봇이 못 사는 테크 하나를 보고 상점까지 걷다가 빈손으로 돌아온다.
        /// </summary>
        public TechId NextWant()
        {
            if (m_Shop == null || m_Wallet == null) return TechId.None;

            return MouseBotLogic.NextPurchase(
                m_Wallet.CheeseValue, m_BoughtDash, m_BoughtGun, m_BoughtWarehouse, m_BoughtFortress,
                m_BoughtShield, m_BoughtPouch, m_Slots != null ? m_Slots.Count : 0, m_Shop.SoldOutToday);
        }

        /// <summary>쥐 — 살 수 있으면 산다. 설치물은 제자리에 둔다.</summary>
        public void BuyForMouse()
        {
            if (m_Shop == null || m_Wallet == null) return;

            // 창고 설치권을 산 직후엔 보통 설치비(WarehousePlaceCost)까지 낼 돈이 안 남는다 —
            // 서버 RPC 자체는 호스트가 곧 서버라 인라인으로 바로 처리되니 지연이 아니라 **지갑**이
            // 진짜 이유다. 돈이 모일 때까지 기다렸다가 실제로 세워질 때만 플래그를 내린다.
            if (m_PendingWarehousePlace)
            {
                ulong owner = m_Shop.OwnerClientId;
                if (WarehouseProduction.CountOwnedBy(owner) > m_WarehouseCountBeforePlace)
                {
                    m_PendingWarehousePlace = false;   // 실제로 섰다 — 확인 완료
                }
                else if (m_Shop.CanUse(TechId.Warehouse) == UseDeny.LimitReached)
                {
                    // 봇들은 서버가 주인이라 OwnerClientId를 공유한다 — 다른 봇이 같은 한도를
                    // 이미 채웠으면 이 봇은 영영 못 세운다. 계속 기다려 봐야 소용없으니 포기한다.
                    m_PendingWarehousePlace = false;
                }
                else if (m_Shop.CanUse(TechId.Warehouse) == UseDeny.None)
                {
                    m_WarehouseCountBeforePlace = WarehouseProduction.CountOwnedBy(owner);
                    m_Shop.UseAt(TechId.Warehouse, m_Body.position);
                }
            }

            TechId next = NextWant();
            if (next == TechId.None) return;

            // 창고는 봇끼리 OwnerClientId를 공유해 한도도 같이 쓴다 — 이미 다른 봇이 채웠으면
            // 권한만 사도 영영 못 세운다. 죽은 구매라 치즈를 아낀다.
            // 상한은 창고 테크 레벨을 탄다(Lv2부터 2개) — 서버 판정과 같은 식을 쓴다.
            if (next == TechId.Warehouse
                && WarehouseProduction.CountOwnedBy(m_Shop.OwnerClientId)
                    >= WarehouseLogic.LimitOf(TechOwner.LevelOf(m_Shop.gameObject, TechId.Warehouse)))
                return;

            // 상점 밖이면(또는 어떤 이유로든) 거절될 구매는 아예 안 보낸다 — 봇 열에
            // 봇 여덟이면 0.5초마다 거절 RPC가 쓰레기처럼 도는다. 품절·칸 상한을
            // 미리 걸러 내는 것과 같은 이유다(죽은 구매를 안 한다).
            if (m_Shop.CanBuyNow(next) != TechDeny.None) return;

            m_Shop.Buy(next);

            if (next == TechId.Dash) m_BoughtDash = true;
            else if (next == TechId.Gun) m_BoughtGun = true;
            else if (next == TechId.Fortress) m_BoughtFortress = true;
            else if (next == TechId.CheeseShield) m_BoughtShield = true;
            else if (next == TechId.BigPouch) m_BoughtPouch = true;
            else if (next == TechId.Warehouse) { m_BoughtWarehouse = true; m_PendingWarehousePlace = true; }
        }

        /// <summary>
        /// 고양이 — <see cref="CatBotLogic.PURCHASE_ORDER"/>를 위에서부터 훑어 살 수 있는 첫째를 산다.
        /// 문턱(누적 획득 300)을 넘으면 전직. **불**을 고르는 건 판단이 필요 없는 유일한 전직이라서다.
        /// 조건이 "강화 둘 만렙"에서 바뀌었지만 여기는 <c>CanAdvance</c>만 보므로 코드는 그대로다.
        ///
        /// 순서(발톱 → 발 구르기 → 두꺼운 가죽 → 배고픔)와 "광분·예민한 귀는 안 산다"는
        /// 이유는 전부 순수 표에 적혀 있다 — 여기는 밀어 넣기만 한다(쥐 쪽과 같은 분업).
        /// 칸 상한은 <c>CanBuyNow</c>가 <c>SlotsFull</c>로 걸러 준다.
        /// </summary>
        public void BuyForCat()
        {
            if (m_Shop == null || m_Wallet == null) return;

            if (m_Class != null && m_Class.CanAdvance && m_Class.Value == CatClassId.None)
            {
                m_Class.ChooseRpc(CatClassId.Fire);
                return;
            }

            for (int i = 0; i < CatBotLogic.PURCHASE_ORDER.Count; i++)
            {
                TechId id = CatBotLogic.PURCHASE_ORDER[i];
                if (m_Shop.CanBuyNow(id) != TechDeny.None) continue;

                m_Shop.Buy(id);
                return;
            }
        }
    }
}
#endif
