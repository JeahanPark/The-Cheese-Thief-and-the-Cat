using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;

namespace CTC.Runtime.UI
{
    public enum AbilityState { Ready, Cooldown, Active }

    /// <summary>능력 슬롯 1칸의 정적 정보(키·이름·반줄 힌트). 시간값(쿨·지속)은 View가 매 프레임 컴포넌트에서 읽는다.</summary>
    public readonly struct AbilitySlot
    {
        public readonly string Key;
        public readonly string Name;
        public readonly string Hint;   // 핫바 칸의 반줄 힌트와 같은 결 — "딱봤을 때 뭔지"

        public AbilitySlot(string _key, string _name, string _hint)
        {
            Key = _key;
            Name = _name;
            Hint = _hint;
        }
    }

    /// <summary>
    /// 능력 바 규칙(2026-08-17) — 순수 함수. 좌하단 글자 칩(ControlsHud)을 대체한다.
    /// 슬롯은 역할별(쥐: Space 대시·F 임명 / 고양이: Q 스킬·Space 광분). 기본 공격·살금은 슬롯이 없다.
    /// **해금 안 된 것은 아예 안 보인다**(2026-08-17 사용자 결정) — 대시는 사야, F는 깡패가 돼야,
    /// Q는 전직해야 칸이 생긴다. 쓸 수 없는 걸 화면에 두면 자리만 먹는다.
    ///
    /// **Space가 두 역할에 있지만 겹치지 않는다** — 쥐는 대시, 고양이는 광분이고 한 사람은 한 역할이다.
    /// 고양이 쪽 Space는 2026-09-09에 붙였다: 고양이는 상점을 안 다녀 핫바가 늘 구매 모드라
    /// 숫자키가 안 남고, 그래서 산 광분을 켤 길이 아예 없었다(감사 P0-1).
    /// </summary>
    public static class AbilityBarLogic
    {
        public const string KEY_DASH = "Space";
        public const string KEY_APPOINT = "F";
        public const string KEY_SKILL = "Q";

        /// <summary>고양이 액티브 전용 키 — <c>ShopHotbar</c>도 이 글자를 읽는다(단일 출처).</summary>
        public const string KEY_FRENZY = "Space";

        /// <summary>
        /// 용사 무적(2026-09-19) — 용사의 Space는 대시가 아니라 무적이다(대시는 사지도 못한다 — <c>HeroSwordLogic.Denied</c>).
        /// 칸 이름이 곧 갈림길이다(<see cref="NAME_INVINCIBLE"/>) — 글자는 대시와 같은 "Space"라서.
        /// </summary>
        public const string KEY_INVINCIBLE = "Space";
        public const string NAME_INVINCIBLE = "무적";

        public static AbilitySlot[] Build(RoleId _role, MouseJobId _job, bool _dashOwned, CatClassId _class,
            bool _frenzyOwned = false)
        {
            var list = new System.Collections.Generic.List<AbilitySlot>(2);
            switch (_role)
            {
                case RoleId.Mouse:
                    // 용사는 Space가 무적이다 — 대시를 이미 샀어도 칸을 무적이 차지한다(누르면 무적이 켜진다)
                    if (_job == MouseJobId.Hero) list.Add(new AbilitySlot(KEY_INVINCIBLE, NAME_INVINCIBLE, "3초 동안 안 다친다"));
                    else if (_dashOwned) list.Add(new AbilitySlot(KEY_DASH, "대시", "순간 가속"));
                    if (_job == MouseJobId.Gang) list.Add(new AbilitySlot(KEY_APPOINT, "임명", "보는 쥐를 부하로"));
                    break;
                case RoleId.Cat:
                    if (_class != CatClassId.None)
                        list.Add(new AbilitySlot(KEY_SKILL, CatClassLogic.ClassWordOf(_class), SkillHintOf(_class)));
                    if (_frenzyOwned) list.Add(new AbilitySlot(KEY_FRENZY, "분노 발톱", "한동안 덮치기가 더 아프다"));
                    break;
            }
            return list.ToArray();
        }

        /// <summary>전직 스킬 반줄 힌트 — 핫바 힌트처럼 짧게.</summary>
        public static string SkillHintOf(CatClassId _class)
        {
            switch (_class)
            {
                case CatClassId.Fire: return "덮치기 더 아프게";
                case CatClassId.Wind: return "한동안 더 빨리";
                case CatClassId.Light: return "벽 너머 쥐 보임";
                case CatClassId.Dark: return "쥐 화면 어둡게";
                default: return "";
            }
        }

        /// <summary>지속 중 &gt; 쿨다운 &gt; 준비.</summary>
        public static AbilityState StateOf(float _activeRemaining, float _cooldownRemaining)
        {
            if (_activeRemaining > 0f) return AbilityState.Active;
            if (_cooldownRemaining > 0f) return AbilityState.Cooldown;
            return AbilityState.Ready;
        }

        /// <summary>남은/전체 → 0~1. 전체가 0이면 0.</summary>
        public static float FillRatio(float _remaining, float _total)
        {
            if (_total <= 0f) return 0f;
            return UnityEngine.Mathf.Clamp01(_remaining / _total);
        }

        /// <summary>남은 초 글자 — 올림 정수, 0이면 빈 칸.</summary>
        public static string SecondsText(float _remaining)
        {
            if (_remaining <= 0f) return "";
            return UnityEngine.Mathf.CeilToInt(_remaining).ToString();
        }
    }
}
