namespace CTC.Runtime.Economy
{
    /// <summary>
    /// 고양이 장난기(Fun) 적립 수학 — 스펙 v3.2. 순수 함수(EditMode TDD).
    /// 실데미지 1 = 1pt(과잉타격은 호출자가 실데미지로 이미 제외), 플레이어 킬만 보너스.
    /// </summary>
    public static class FunLogic
    {
        public const int KILL_BONUS = 20;

        public static int AccruedFun(int _dealtDamage, bool _killedPlayer)
        {
            int dealt = UnityEngine.Mathf.Max(0, _dealtDamage);
            return dealt + (_killedPlayer ? KILL_BONUS : 0);
        }
    }
}
