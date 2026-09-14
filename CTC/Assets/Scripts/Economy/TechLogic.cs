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
            // enum이 아니라 int로 태운다 — 와이어 폭 4바이트 고정, TechId 밑 타입이 바뀌어도 프로토콜 불변.
            // 이웃 struct(LootEntry 등)는 enum을 직접 넘기지만 여기선 의도적으로 다르게 간다.
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
        // 레벨별 효과 파라미터 — 의미는 테크마다 다르다(TechInfo.csv 주석·기획서 §4).
        // 컬럼 폭발을 막으려고 세 칸에 몰아 넣었다. 안 쓰는 칸은 0.
        public readonly float P1;
        public readonly float P2;
        public readonly float P3;

        public bool IsValid => Id != TechId.None;

        public TechRow(Info.TechInfo _r)
        {
            Id = _r.TechId; Role = _r.Role; Axis = _r.Axis; Level = _r.Level;
            Cost = _r.Cost; Label = _r.Label; Hint = _r.Hint;
            P1 = _r.P1; P2 = _r.P2; P3 = _r.P3;
        }
    }

    // **뒤에만 추가한다** — 값이 직렬화되진 않지만 QA 문자열(wesqa)이 이름에 붙어 있다.
    /// <remarks><c>GangNoGun</c>(2026-09-14) — 깡패판의 깡패 편은 총을 못 쓰니 총·후추 탄도 못 산다. 새 값은 **맨 뒤에** 붙인다(정수 값 유지).</remarks>
    public enum TechDeny { None, Unknown, WrongRole, MaxLevel, SlotsFull, NotEnough, NotInShop, SoldOut, GangNoGun }

    /// <summary>
    /// "쓰기"가 막힌 이유 (2026-09-07). 전에는 <c>CanUse</c>가 bool이라 View가 TechId로 이유를 추측했다 —
    /// 치즈가 모자란 건데 "창고는 하나까지"라고 거짓말을 했다. 이유는 판정한 쪽이 말한다.
    /// </summary>
    public enum UseDeny { None, NotOwned, NotActive, InsideFortress, NotEnough, LimitReached, Unavailable, Cooldown }

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
            IReadOnlyList<Info.TechInfo> rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                Info.TechInfo r = rows[i];
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
            IReadOnlyList<Info.TechInfo> rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                Info.TechInfo r = rows[i];
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

        // 매 프레임 경로에서 불린다 — 열거자 할당을 피한다.
        public static TechRow RowOf(TechId _id, int _level)
        {
            IReadOnlyList<Info.TechInfo> rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                Info.TechInfo r = rows[i];
                if (r.TechId == _id && r.Level == _level) return new TechRow(r);
            }
            return default;
        }

        /// <summary>레벨별 효과 파라미터(1~3). 미보유(0)·없는 레벨은 0 — 효과 훅이 "없음"으로 읽게.</summary>
        public static float ParamOf(TechId _id, int _level, int _index)
        {
            if (_level <= 0) return 0f;
            TechRow row = RowOf(_id, _level);
            if (!row.IsValid) return 0f;
            switch (_index)
            {
                case 1: return row.P1;
                case 2: return row.P2;
                case 3: return row.P3;
                default: return 0f;
            }
        }

        /// <summary>다음에 살 행 — 미보유면 Lv1, 만렙이면 무효.</summary>
        public static TechRow NextRow(IReadOnlyList<TechEntry> _owned, TechId _id)
        {
            return RowOf(_id, LevelOf(_owned, _id) + 1);
        }

        /// <summary>
        /// 구매·업그레이드 가능 판정. 상점 안 여부는 위치라 여기 없다 — 호출부가 <see cref="NeedsShopSpot"/>와
        /// <c>ShopSpot.AnyContains</c>로 따로 본다.
        ///
        /// <paramref name="_soldOut"/>은 **오늘의 품절**(계획 3 Task 3 · 기획서 §7-2). null이면 품절 없음이라
        /// 기본값을 준다 — 로비·봇 실험실처럼 시드가 없는 자리와, 품절을 모르는 옛 테스트가 그대로 돈다.
        /// </summary>
        public static TechDeny CanBuy(IReadOnlyList<TechEntry> _owned, TechId _id, RoleId _role, int _cheese,
            int _slotCap, IReadOnlyList<TechId> _soldOut = null)
        {
            // 역할 불일치는 행이 있을 때만 판단 가능하니 먼저 본다.
            // 표에 없는 테크(next 무효)면서 칸이 찼으면 SlotsFull이 불가피 — Unknown보다 앞세운다
            // (표에 없다는 이유로 Unknown을 앞세우면 칸이 찬 상태에서 미래 테크를 미리 안다는 오정보가 된다).
            TechRow next = NextRow(_owned, _id);
            if (next.IsValid && next.Role != _role) return TechDeny.WrongRole;
            // 품절은 **칸이 없어서**가 아니라 **오늘은 없어서**다 — SlotsFull보다 먼저 말한다.
            // 이미 가진 테크의 업그레이드는 막지 않는다: 품절은 판 시작에 정해지니 실제로는 Lv0만 걸리지만,
            // 규칙으로 못 박아 둔다(나중에 판 중간 품절이 생겨도 산 것을 뺏지 않는다).
            if (LevelOf(_owned, _id) == 0 && TechRandomLogic.IsSoldOut(_soldOut, _id)) return TechDeny.SoldOut;
            if (LevelOf(_owned, _id) == 0 && _owned.Count >= _slotCap) return TechDeny.SlotsFull;
            if (!next.IsValid) return MaxLevel(_id) == 0 ? TechDeny.Unknown : TechDeny.MaxLevel;
            if (_cheese < next.Cost) return TechDeny.NotEnough;
            return TechDeny.None;
        }

        /// <summary>상점 밖에서 숫자키로 "쓰는" 테크 — 나머지는 패시브라 누를 게 없다.</summary>
        public static bool IsActive(TechId _id)
        {
            // 광분은 고양이의 첫 액티브 테크다(테크 v3 계획 2b) — 전직 스킬(Q)과 달리 칸을 쓰고 숫자키로 켠다.
            return _id == TechId.Fortress || _id == TechId.Warehouse || _id == TechId.Sticky
                || _id == TechId.Dash || _id == TechId.CatFrenzy;
        }

        /// <summary>기획 규칙 3·5 — 쥐만 상점에 가야 한다.</summary>
        public static bool NeedsShopSpot(RoleId _role) => _role == RoleId.Mouse;

        /// <summary>
        /// 이 역할의 핫바는 **칸이 곧 테크**인가 (2026-09-11).
        ///
        /// 상점에 가야 하는 역할(쥐)은 테크 10종을 칸 5개로 훑어야 해서 「같은 숫자키 반복 = 다음 후보」가
        /// 필요하다. 상점 면제 역할(고양이)은 6종뿐이고 그중 다섯이 순수 스탯이라 넘길 것이 없다 —
        /// 값은 못 하고 부작용만 냈다(발톱이 1·2번에, 광분이 3·5번에 겹쳐 뜬 2026-09-11 신고).
        /// 그 역할은 카탈로그 순서대로 한 칸씩 못박는다.
        /// </summary>
        public static bool FixedSlots(RoleId _role) => !NeedsShopSpot(_role);

        /// <summary>
        /// 역할별 칸 수. 고정 배치 역할은 **제 테크 수만큼** 칸을 갖는다(1키 = 1테크) —
        /// 그 수를 CSV에 또 적으면 테크가 늘 때 둘이 조용히 어긋난다. 넘기기 역할은 CSV의 전역 상한.
        /// </summary>
        public static int SlotCapOf(RoleId _role)
            => FixedSlots(_role) ? Catalog(_role).Count : Info.InfoManager.Instance.Balance.TechSlotCap;

        /// <summary>씬이 구워야 할 칸 수 — 역할 중 가장 많은 쪽. 남는 칸은 런타임에 역할별로 숨긴다.</summary>
        public static int SlotCapMax()
        {
            int max = 0;
            foreach (RoleId role in System.Enum.GetValues(typeof(RoleId)))
            {
                if (role == RoleId.None) continue;
                int cap = SlotCapOf(role);
                if (cap > max) max = cap;
            }
            return max;
        }

        /// <summary>고정 배치에서 이 칸이 맡은 테크 — 넘기기 역할이거나 범위 밖이면 None.</summary>
        public static TechId SlotTechOf(RoleId _role, int _slot)
        {
            if (!FixedSlots(_role) || _slot < 0) return TechId.None;
            IReadOnlyList<TechId> catalog = Catalog(_role);
            return _slot < catalog.Count ? catalog[_slot] : TechId.None;
        }

        /// <summary>
        /// 빈 칸이 넘길 다음 후보. **다른 빈 칸이 이미 보여 주는 것은 건너뛴다** — 두 칸이 같은 테크를
        /// 가리키면 「고를 게 하나뿐」으로 읽힌다(2026-09-11 신고의 나머지 절반).
        /// <paramref name="_taken"/>은 이미 <c>% _count</c>로 접힌 인덱스들이다.
        /// 후보가 빈 칸보다 적어 겹침을 피할 수 없으면 그냥 다음으로 간다.
        /// </summary>
        public static int NextCandidateIndex(int _current, int _count, IReadOnlyList<int> _taken)
        {
            if (_count <= 0) return 0;
            for (int step = 1; step <= _count; step++)
            {
                int idx = (_current + step) % _count;
                bool taken = false;
                for (int i = 0; _taken != null && i < _taken.Count; i++)
                {
                    if (_taken[i] != idx) continue;
                    taken = true;
                    break;
                }
                if (!taken) return idx;
            }
            return (_current + 1) % _count;
        }

        /// <summary>
        /// 이 역할·테크는 **액티브 전용 키로만** 쓸 수 있는가.
        ///
        /// 「밖에서 숫자키 = 쓴다」는 <b>밖이 있는 역할</b>에서만 성립한다. 상점 면제 역할(고양이)은
        /// <see cref="NeedsShopSpot"/>이 false라 핫바가 영원히 구매 모드고, 숫자키는 전부
        /// 「산다/키운다」로 간다 — 그래서 산 광분을 켤 길이 아예 없었다(2026-09-09 감사 P0-1).
        /// 그 역할의 액티브만 전용 키(Space)를 갖는다.
        /// </summary>
        public static bool NeedsActiveKey(RoleId _role, TechId _id) => !NeedsShopSpot(_role) && IsActive(_id);

        /// <summary>
        /// 총에 붙는 테크인가 — 총 티어(<see cref="TechId.Gun"/>)와 총알에 붙는 후추 탄(<see cref="TechId.Pepper"/>).
        /// 깡패판의 깡패 편은 공격 버튼이 몽둥이라 이 둘은 사 봐야 치즈만 사라진다 → 막는다(2026-09-14 사용자 결정).
        /// </summary>
        public static bool IsGunTech(TechId _id) => _id == TechId.Gun || _id == TechId.Pepper;

        /// <summary>거절 사유 → 토스트 문구. 재화 이름은 역할별, 칸 수는 CanBuy에 준 값과 같아야 한다(단일 출처).</summary>
        public static string DenyText(TechDeny _deny, RoleId _role, int _short, int _slotCap)
        {
            switch (_deny)
            {
                case TechDeny.MaxLevel: return "이미 만렙";
                case TechDeny.SlotsFull: return $"칸이 다 찼다 — {_slotCap}개까지";
                case TechDeny.NotEnough: return $"{ShopLogic.CurrencyNameOf(_role)} 부족 · {_short} 모자람";
                case TechDeny.NotInShop: return "상점에서만 살 수 있다";
                case TechDeny.WrongRole: return "이 역할은 못 산다";
                case TechDeny.SoldOut: return "오늘은 품절";
                case TechDeny.GangNoGun: return "깡패는 총을 못 쓴다 — 몽둥이뿐";
                default: return "살 수 없다";
            }
        }

        /// <summary>"쓰기" 거절 사유 → 토스트 문구. <paramref name="_short"/>는 설치비에서 모자란 양.</summary>
        public static string UseDenyText(UseDeny _deny, RoleId _role, int _short)
        {
            switch (_deny)
            {
                case UseDeny.NotOwned: return "아직 없다";
                case UseDeny.NotActive: return "패시브 — 늘 켜져 있다";
                case UseDeny.InsideFortress: return "요새 안에서는 못 세운다 — 밖으로";
                case UseDeny.NotEnough: return $"{ShopLogic.CurrencyNameOf(_role)} 부족 · {_short} 모자람";
                case UseDeny.LimitReached: return "창고는 하나까지";
                case UseDeny.Cooldown: return "아직 쿨다운";
                default: return "지금은 못 쓴다";
            }
        }
    }
}
