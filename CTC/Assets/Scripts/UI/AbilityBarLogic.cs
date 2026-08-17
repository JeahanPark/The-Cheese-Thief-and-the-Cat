using CTC.Runtime.Lobby;
using CTC.Runtime.Match;
using CTC.Runtime.Player;

namespace CTC.Runtime.UI
{
    public enum AbilityState { Ready, Cooldown, Active, Locked }

    /// <summary>능력 슬롯 1칸의 정적 정보(키·이름·잠금). 시간값(쿨·지속)은 View가 매 프레임 컴포넌트에서 읽는다.</summary>
    public readonly struct AbilitySlot
    {
        public readonly string Key;
        public readonly string Name;
        public readonly bool Locked;
        public readonly string LockReason;

        public AbilitySlot(string _key, string _name, bool _locked, string _lockReason)
        {
            Key = _key;
            Name = _name;
            Locked = _locked;
            LockReason = _lockReason;
        }
    }

    /// <summary>
    /// 능력 바 규칙(2026-08-17) — 순수 함수. 좌하단 글자 칩(ControlsHud)을 대체한다.
    /// 슬롯은 역할별 고정(쥐: Space 대시·F 임명 / 고양이: Q 스킬). 기본 공격·살금은 슬롯이 없다.
    /// 미보유는 숨기지 않고 흐리게 + 얻는 곳을 적는다 — "뭐가 있는지"를 첫 판에 알게.
    /// </summary>
    public static class AbilityBarLogic
    {
        public const string KEY_DASH = "Space";
        public const string KEY_APPOINT = "F";
        public const string KEY_SKILL = "Q";

        public static AbilitySlot[] Build(RoleId _role, MouseJobId _job, bool _dashOwned, CatClassId _class, int _dashShopSlot)
        {
            switch (_role)
            {
                case RoleId.Mouse:
                    return new[]
                    {
                        new AbilitySlot(KEY_DASH, "대시", !_dashOwned, _dashOwned ? "" : $"상점 {_dashShopSlot}"),
                        new AbilitySlot(KEY_APPOINT, "임명", _job != MouseJobId.Gang, _job == MouseJobId.Gang ? "" : "깡패만"),
                    };
                case RoleId.Cat:
                    bool advanced = _class != CatClassId.None;
                    return new[]
                    {
                        new AbilitySlot(KEY_SKILL, advanced ? CatClassLogic.ClassWordOf(_class) : "스킬",
                            !advanced, advanced ? "" : "전직 필요"),
                    };
                default:
                    return System.Array.Empty<AbilitySlot>();
            }
        }

        /// <summary>잠금 &gt; 지속 중 &gt; 쿨다운 &gt; 준비.</summary>
        public static AbilityState StateOf(bool _locked, float _activeRemaining, float _cooldownRemaining)
        {
            if (_locked) return AbilityState.Locked;
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
