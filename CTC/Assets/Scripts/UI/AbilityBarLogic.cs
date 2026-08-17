using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;

namespace CTC.Runtime.UI
{
    public enum AbilityState { Ready, Cooldown, Active }

    /// <summary>능력 슬롯 1칸의 정적 정보(키·이름). 시간값(쿨·지속)은 View가 매 프레임 컴포넌트에서 읽는다.</summary>
    public readonly struct AbilitySlot
    {
        public readonly string Key;
        public readonly string Name;

        public AbilitySlot(string _key, string _name)
        {
            Key = _key;
            Name = _name;
        }
    }

    /// <summary>
    /// 능력 바 규칙(2026-08-17) — 순수 함수. 좌하단 글자 칩(ControlsHud)을 대체한다.
    /// 슬롯은 역할별(쥐: Space 대시·F 임명 / 고양이: Q 스킬). 기본 공격·살금은 슬롯이 없다.
    /// **해금 안 된 것은 아예 안 보인다**(2026-08-17 사용자 결정) — 대시는 사야, F는 깡패가 돼야,
    /// Q는 전직해야 칸이 생긴다. 쓸 수 없는 걸 화면에 두면 자리만 먹는다.
    /// </summary>
    public static class AbilityBarLogic
    {
        public const string KEY_DASH = "Space";
        public const string KEY_APPOINT = "F";
        public const string KEY_SKILL = "Q";

        public static AbilitySlot[] Build(RoleId _role, MouseJobId _job, bool _dashOwned, CatClassId _class)
        {
            var list = new System.Collections.Generic.List<AbilitySlot>(2);
            switch (_role)
            {
                case RoleId.Mouse:
                    if (_dashOwned) list.Add(new AbilitySlot(KEY_DASH, "대시"));
                    if (_job == MouseJobId.Gang) list.Add(new AbilitySlot(KEY_APPOINT, "임명"));
                    break;
                case RoleId.Cat:
                    if (_class != CatClassId.None) list.Add(new AbilitySlot(KEY_SKILL, CatClassLogic.ClassWordOf(_class)));
                    break;
            }
            return list.ToArray();
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
