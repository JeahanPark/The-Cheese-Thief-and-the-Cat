using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 치즈 방패 (테크 v3 계획 2a) — 피해의 일부를 치즈로 대신 낸다. "째서 번 돈이 곧 목숨".
    /// 지갑이 모자라면 낼 수 있는 만큼만 치즈로, 나머지는 HP로.
    /// </summary>
    public static class CheeseShieldLogic
    {
        public static void Absorb(int _damage, float _ratio, int _cheesePerHp, int _cheese,
            out int _hpDamage, out int _cheeseCost)
        {
            if (_damage <= 0 || _ratio <= 0f || _cheesePerHp <= 0 || _cheese <= 0)
            {
                _hpDamage = Mathf.Max(0, _damage);
                _cheeseCost = 0;
                return;
            }
            int wantHp = Mathf.RoundToInt(_damage * _ratio);              // 치즈로 대신 낼 HP
            int canHp = Mathf.Min(wantHp, _cheese / _cheesePerHp);         // 지갑이 감당하는 HP
            _cheeseCost = canHp * _cheesePerHp;
            _hpDamage = _damage - canHp;
        }
    }
}
