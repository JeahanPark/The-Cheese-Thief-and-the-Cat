// Assets/Scripts/Economy/TechLogic.cs
using System.Collections.Generic;
using CTC.Runtime.Lobby;
using Unity.Netcode;

namespace CTC.Runtime.Economy
{
    /// <summary>보유 테크 한 칸 — NetworkList 원소라 INetworkSerializable·IEquatable.</summary>
    public struct TechEntry : INetworkSerializable, System.IEquatable<TechEntry>
    {
        public TechId Id;
        public int Level;

        public TechEntry(TechId _id, int _level)
        {
            Id = _id;
            Level = _level;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> _s) where T : IReaderWriter
        {
            int id = (int)Id;
            _s.SerializeValue(ref id);
            _s.SerializeValue(ref Level);
            Id = (TechId)id;
        }

        public bool Equals(TechEntry _o) => Id == _o.Id && Level == _o.Level;
    }

    /// <summary>CSV 한 행의 값 복사 — 행이 없으면 <see cref="IsValid"/>가 false.</summary>
    public readonly struct TechRow
    {
        public readonly TechId Id;
        public readonly RoleId Role;
        public readonly TechAxis Axis;
        public readonly int Level;
        public readonly int Cost;
        public readonly string Label;
        public readonly string Hint;

        public bool IsValid => Id != TechId.None;

        public TechRow(Info.TechInfo _r)
        {
            Id = _r.TechId; Role = _r.Role; Axis = _r.Axis; Level = _r.Level;
            Cost = _r.Cost; Label = _r.Label; Hint = _r.Hint;
        }
    }

    public enum TechDeny { None, Unknown, WrongRole, MaxLevel, SlotsFull, NotEnough, NotInShop }

    /// <summary>
    /// 테크 판정 순수 로직 (테크 v3, 2026-09-07). MonoBehaviour와 분리 — EditMode TDD.
    /// 규칙(기획서 §2): 칸 5 · 레벨은 순서대로 · 쥐는 상점 안에서만 · 되팔기 없음.
    /// </summary>
    public static class TechLogic
    {
        private static IReadOnlyList<Info.TechInfo> Rows => Info.InfoManager.Instance.Tech;

        /// <summary>역할의 테크 목록 — CSV 등장 순서, 중복 없음.</summary>
        public static IReadOnlyList<TechId> Catalog(RoleId _role)
        {
            var list = new List<TechId>();
            foreach (Info.TechInfo r in Rows)
            {
                if (r.Role != _role || list.Contains(r.TechId)) continue;
                list.Add(r.TechId);
            }
            return list;
        }

        /// <summary>아직 안 산 테크 — 상점 빈 칸 후보(순서 유지).</summary>
        public static IReadOnlyList<TechId> Candidates(RoleId _role, IReadOnlyList<TechEntry> _owned)
        {
            var list = new List<TechId>();
            foreach (TechId id in Catalog(_role))
            {
                if (LevelOf(_owned, id) == 0) list.Add(id);
            }
            return list;
        }

        public static int MaxLevel(TechId _id)
        {
            int max = 0;
            foreach (Info.TechInfo r in Rows)
            {
                if (r.TechId == _id && r.Level > max) max = r.Level;
            }
            return max;
        }

        public static int LevelOf(IReadOnlyList<TechEntry> _owned, TechId _id)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Id == _id) return _owned[i].Level;
            }
            return 0;
        }

        public static TechRow RowOf(TechId _id, int _level)
        {
            foreach (Info.TechInfo r in Rows)
            {
                if (r.TechId == _id && r.Level == _level) return new TechRow(r);
            }
            return default;
        }

        /// <summary>다음에 살 행 — 미보유면 Lv1, 만렙이면 무효.</summary>
        public static TechRow NextRow(IReadOnlyList<TechEntry> _owned, TechId _id)
        {
            return RowOf(_id, LevelOf(_owned, _id) + 1);
        }

        /// <summary>
        /// 구매·업그레이드 가능 판정. 상점 안 여부는 위치라 여기 없다 — 호출부가 <see cref="NeedsShopSpot"/>와
        /// <c>ShopSpot.AnyContains</c>로 따로 본다.
        /// </summary>
        public static TechDeny CanBuy(IReadOnlyList<TechEntry> _owned, TechId _id, RoleId _role, int _cheese, int _slotCap)
        {
            // 새 테크(미보유)인데 칸이 다 찼으면 — 표에 없는 테크라도 "칸이 없다"가 먼저다.
            // (표에 없다는 이유로 Unknown을 앞세우면 칸이 찬 상태에서 미래 테크를 미리 안다는 오정보가 된다)
            bool isNew = LevelOf(_owned, _id) == 0;
            if (isNew && _owned.Count >= _slotCap) return TechDeny.SlotsFull;

            TechRow next = NextRow(_owned, _id);
            if (!next.IsValid)
            {
                return MaxLevel(_id) == 0 ? TechDeny.Unknown : TechDeny.MaxLevel;
            }
            if (next.Role != _role) return TechDeny.WrongRole;
            if (_cheese < next.Cost) return TechDeny.NotEnough;
            return TechDeny.None;
        }

        /// <summary>상점 밖에서 숫자키로 "쓰는" 테크 — 나머지는 패시브라 누를 게 없다.</summary>
        public static bool IsActive(TechId _id)
        {
            return _id == TechId.Fortress || _id == TechId.Warehouse || _id == TechId.Sticky || _id == TechId.Dash;
        }

        /// <summary>기획 규칙 3·5 — 쥐만 상점에 가야 한다.</summary>
        public static bool NeedsShopSpot(RoleId _role) => _role == RoleId.Mouse;

        /// <summary>거절 사유 → 토스트 문구. 재화 이름은 역할별.</summary>
        public static string DenyText(TechDeny _deny, RoleId _role, int _short)
        {
            switch (_deny)
            {
                case TechDeny.MaxLevel: return "이미 만렙";
                case TechDeny.SlotsFull: return $"칸이 다 찼다 — {Info.InfoManager.Instance.Balance.TechSlotCap}개까지";
                case TechDeny.NotEnough: return $"{ShopLogic.CurrencyNameOf(_role)} 부족 · {_short} 모자람";
                case TechDeny.NotInShop: return "상점에서만 살 수 있다";
                case TechDeny.WrongRole: return "이 역할은 못 산다";
                default: return "살 수 없다";
            }
        }
    }
}
