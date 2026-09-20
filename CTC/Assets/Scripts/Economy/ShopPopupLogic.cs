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

    /// <summary>상세 칸 사다리 한 단(2026-09-19 시안 B) — 가진 단계 · 다음에 살 단계 · 못 사는 다음 단계 · 그 뒤.</summary>
    public enum ShopStep { Owned, Next, NextBlocked, Locked }

    /// <summary>상세 구매 버튼의 모양 — 살 수 있으면 노랑(주 버튼), 아니면 이유의 색.</summary>
    public enum ShopButtonTone { Buy, Short, Blocked, Maxed }

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
            int _cheese, int _slotCap, IReadOnlyList<TechId> _soldOut, bool _clubActive, bool _swordActive = false)
        {
            int level = TechLogic.LevelOf(_owned, _id);
            TechRow next = TechLogic.NextRow(_owned, _id);

            // 깡패판의 깡패는 총·후추를 못 산다 — 공격 버튼이 몽둥이라 사 봐야 치즈만 사라진다.
            // 용사는 칼을 쓴다 — 총알·대시는 소용없다(2026-09-19). 서버(ShopController.CanBuyNow)와 같은 규칙.
            TechDeny deny = _clubActive && TechLogic.IsGunTech(_id) ? TechDeny.GangNoGun
                : _swordActive && Combat.HeroSwordLogic.Denied(_id) ? TechDeny.HeroNoGun
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

        // ───────────────── 목록 + 상세 화면 (2026-09-19 시안 B, 사용자 선택) ─────────────────

        /// <summary>
        /// 목록 순서 — **축별로 묶는다**(공격 · 방어 · 경제 · 도주 · 이동). 같은 축 안에서는 카탈로그(CSV) 순서 그대로다.
        /// 축은 CSV `Axis` 컬럼이 원본이라 새 기술을 넣어도 제자리를 찾아간다.
        /// </summary>
        public static List<TechId> ListOrder(IReadOnlyList<TechId> _catalog)
        {
            var order = new List<TechId>(_catalog.Count);
            foreach (TechAxis axis in System.Enum.GetValues(typeof(TechAxis)))
            {
                for (int i = 0; i < _catalog.Count; i++)
                {
                    if (TechLogic.RowOf(_catalog[i], 1).Axis == axis) order.Add(_catalog[i]);
                }
            }
            return order;
        }

        /// <summary>축 머리글.</summary>
        public static string AxisLabel(TechAxis _axis)
        {
            switch (_axis)
            {
                case TechAxis.Attack: return "공격";
                case TechAxis.Defense: return "방어";
                case TechAxis.Resource: return "경제";
                case TechAxis.Escape: return "도주";
                case TechAxis.Utility: return "이동";
                default: return "";
            }
        }

        /// <summary>목록·상세에 쓰는 기술 이름 — 1레벨 이름(새총·요새…). 단계마다 이름이 바뀌는 기술도 줄은 하나다.</summary>
        public static string FamilyName(TechId _id) => TechLogic.RowOf(_id, 1).Label;

        /// <summary>"Lv 1/3" — 예전 ○○○ 점을 글자로 바꿨다(2026-09-19 사용자: 「ooo은 뭐냐」).</summary>
        public static string LevelText(ShopPopupCard _card) => $"Lv {_card.OwnedLevel}/{TechLogic.MaxLevel(_card.Id)}";

        /// <summary>목록 오른쪽 값 — 다음 단계 가격, 아니면 품절 · 만렙.</summary>
        public static string RowPrice(ShopPopupCard _card, RoleId _role)
        {
            if (_card.NextLevel == 0) return "만렙";
            if (_card.Deny == TechDeny.SoldOut) return "품절";
            return $"{_card.Cost} {ShopLogic.CurrencyNameOf(_role)}";
        }

        /// <summary>
        /// 사다리의 그 단계(1부터). 가진 단계는 <see cref="ShopStep.Owned"/>, 바로 다음 단계는 살 수 있으면
        /// <see cref="ShopStep.Next"/> · 이유가 있어 못 사면 <see cref="ShopStep.NextBlocked"/>. 품절이면 다음 단계도 잠긴 것으로 본다.
        /// </summary>
        public static ShopStep StepOf(ShopPopupCard _card, int _level)
        {
            if (_level <= _card.OwnedLevel) return ShopStep.Owned;
            if (_card.NextLevel == 0 || _level != _card.NextLevel || _card.Deny == TechDeny.SoldOut) return ShopStep.Locked;
            return _card.CanBuy ? ShopStep.Next : ShopStep.NextBlocked;
        }

        /// <summary>상세 구매 버튼의 모양.</summary>
        public static ShopButtonTone ButtonTone(ShopPopupCard _card)
        {
            if (_card.CanBuy) return ShopButtonTone.Buy;
            if (_card.NextLevel == 0) return ShopButtonTone.Maxed;
            if (_card.Deny == TechDeny.NotEnough || _card.Deny == TechDeny.SlotsFull) return ShopButtonTone.Short;
            return ShopButtonTone.Blocked;
        }

        /// <summary>
        /// 상세 구매 버튼의 글 — 살 수 있으면 「바쁜 창고 키운다 · 320 치즈」(새로면 「산다」), 아니면 이유.
        /// 이유 문구는 토스트와 같은 <see cref="TechLogic.DenyText"/>다(출처를 늘리지 않는다) — 부족만 짧게 쓴다.
        /// </summary>
        public static string ButtonText(ShopPopupCard _card, RoleId _role, int _slotCap)
        {
            string currency = ShopLogic.CurrencyNameOf(_role);
            if (_card.CanBuy)
            {
                string verb = _card.OwnedLevel == 0 ? "산다" : "키운다";
                return $"{TechLogic.RowOf(_card.Id, _card.NextLevel).Label} {verb} · {_card.Cost} {currency}";
            }
            if (_card.NextLevel == 0) return "만렙 — 더 못 키운다";
            if (_card.Deny == TechDeny.NotEnough) return $"{currency} {_card.Short} 모자람";
            return TechLogic.DenyText(_card.Deny, _role, _card.Short, _slotCap);
        }

        /// <summary>버튼 밑 한 줄 — 칸을 쓰는지, 모자랄 때 무엇이 얼마인지.</summary>
        public static string NoteText(ShopPopupCard _card, RoleId _role)
        {
            if (_card.CanBuy) return _card.OwnedLevel == 0 ? "새로 사면 칸 1개를 쓴다" : "강화는 칸을 안 쓴다";
            if (_card.NextLevel == 0) return "마지막 단계까지 샀다";
            if (_card.Deny == TechDeny.SoldOut) return "이번 판에는 못 산다";
            if (_card.Deny == TechDeny.NotEnough)
                return $"{TechLogic.RowOf(_card.Id, _card.NextLevel).Label} · {_card.Cost} {ShopLogic.CurrencyNameOf(_role)}";
            return "";
        }
    }
}
