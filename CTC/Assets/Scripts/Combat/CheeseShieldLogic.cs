using UnityEngine;

namespace CTC.Runtime.Combat
{
    /// <summary>
    /// 치즈 방패 (테크 v3 계획 2a) — 피해의 일부를 치즈로 대신 낸다. "쥐가 번 돈이 곧 목숨".
    /// 지갑이 모자라면 낼 수 있는 만큼만 치즈로, 나머지는 HP로.
    /// </summary>
    public static class CheeseShieldLogic
    {
        /// <param name="_currentHp">지금 남은 HP — 어차피 죽는 타격이면 치즈를 받지 않는다.</param>
        public static void Absorb(int _damage, float _ratio, int _cheesePerHp, int _cheese, int _currentHp,
            out int _hpDamage, out int _cheeseCost)
        {
            if (_damage <= 0 || _ratio <= 0f || _cheesePerHp <= 0 || _cheese <= 0)
            {
                _hpDamage = Mathf.Max(0, _damage);
                _cheeseCost = 0;
                return;
            }
            // 비율이 1을 넘어도 피해보다 더 물릴 수는 없다 — 안 맞은 몫까지 돈을 받는 꼴이 된다.
            int wantHp = Mathf.Min(_damage, Mathf.RoundToInt(_damage * _ratio));
            int canHp = Mathf.Min(wantHp, _cheese / _cheesePerHp);         // 지갑이 감당하는 HP

            // 막고도 죽는 타격이면 아무것도 흡수하지 않는다 — "번 돈이 곧 목숨"인데
            // 돈을 내고도 죽으면 개념이 거꾸로다. 치즈는 살아남을 때만 값을 한다.
            if (_damage - canHp >= _currentHp)
            {
                _hpDamage = _damage;
                _cheeseCost = 0;
                return;
            }

            _cheeseCost = canHp * _cheesePerHp;
            _hpDamage = _damage - canHp;
        }
    }
}
