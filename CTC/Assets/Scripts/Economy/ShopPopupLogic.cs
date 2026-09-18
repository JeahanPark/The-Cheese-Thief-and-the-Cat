using System.Collections.Generic;
using CTC.Runtime.Lobby;

namespace CTC.Runtime.Economy
{
    /// <summary>팝업에 그릴 카드 한 장. 이름·힌트는 View가 CSV에서 읽고, 여기는 상태만 준다.</summary>
    public struct ShopPopupCard
    {
        public TechId Id;
        public int OwnedLevel;
        /// <summary>다음에 살 레벨. 만렙이면 0.</summary>
        public int NextLevel;
        public int Cost;
        public TechDeny Deny;
        /// <summary>모자란 치즈(모자라지 않으면 0) — 문구에 그대로 들어간다.</summary>
        public int Short;

        public bool CanBuy => Deny == TechDeny.None;
    }

    /// <summary>
    /// 상점 팝업 판정 (2026-09-19, 기획 `2026-09-19-shop-popup-design.md`).
    ///
    /// **표시용이다** — 진짜 판정은 서버(`ShopController.BuyRpc`)가 한다. 여기서 클릭을 막지 않고
    /// 회색으로 보여 주기만 하므로, 클라 판정이 서버와 어긋나도 치즈가 새지 않는다.
    ///
    /// 거절 사유는 <see cref="TechLogic.CanBuy"/>를 그대로 쓴다 — 판정의 출처를 늘리지 않는다.
    /// </summary>
    public static class ShopPopupLogic
    {
        /// <summary>카드 한 장의 상태.</summary>
        public static ShopPopupCard CardOf(IReadOnlyList<TechEntry> _owned, TechId _id, RoleId _role,
            int _cheese, int _slotCap, IReadOnlyList<TechId> _soldOut, bool _clubActive)
        {
            int level = TechLogic.LevelOf(_owned, _id);
            TechRow next = TechLogic.NextRow(_owned, _id);

            // 깡패판의 깡패는 총·후추를 못 산다 — 공격 버튼이 몽둥이라 사 봐야 치즈만 사라진다.
            TechDeny deny = _clubActive && TechLogic.IsGunTech(_id)
                ? TechDeny.GangNoGun
                : TechLogic.CanBuy(_owned, _id, _role, _cheese, _slotCap, _soldOut);

            return new ShopPopupCard
            {
                Id = _id,
                OwnedLevel = level,
                NextLevel = next.IsValid ? next.Level : 0,
                Cost = next.IsValid ? next.Cost : 0,
                Deny = deny,
                Short = deny == TechDeny.NotEnough && next.IsValid ? next.Cost - _cheese : 0,
            };
        }

        /// <summary>
        /// 창을 열 수 있는가 — **상점 원 안의 살아 있는 쥐**만, 손이 비어 있을 때.
        /// 고양이는 상점이 없어 핫바로 산다(<see cref="TechLogic.NeedsShopSpot"/>).
        /// </summary>
        public static bool CanOpen(RoleId _role, bool _inShop, bool _dead, bool _locked, bool _typing)
        {
            if (!TechLogic.NeedsShopSpot(_role)) return false;
            return _inShop && !_dead && !_locked && !_typing;
        }

        /// <summary>
        /// 열려 있는 창을 강제로 닫아야 하는가. **맞으면 닫는다** — 상점은 안전지대가 아니다(기획 §5).
        /// 커서가 풀린 채 굳는 것을 막는 마지막 방어선이기도 하다.
        /// </summary>
        public static bool ShouldClose(bool _inShop, bool _dead, bool _hurt, bool _locked, bool _matchOver)
        {
            return !_inShop || _dead || _hurt || _locked || _matchOver;
        }
    }
}
