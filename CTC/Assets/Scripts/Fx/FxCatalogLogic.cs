using CTC.Runtime.Player;

namespace CTC.Runtime.Fx
{
    /// <summary>전직 → 이펙트 고르기 — 순수 함수(EditMode, 2026-09-14). 전직 없음은 null(아무것도 안 튼다).</summary>
    public static class FxCatalogLogic
    {
        public static FxId? AdvanceFor(CatClassId _class)
        {
            switch (_class)
            {
                case CatClassId.Fire: return FxId.AdvanceFire;
                case CatClassId.Wind: return FxId.AdvanceWind;
                case CatClassId.Light: return FxId.AdvanceLight;
                case CatClassId.Dark: return FxId.AdvanceDark;
                default: return null;
            }
        }

        public static FxId? SkillFor(CatClassId _class)
        {
            switch (_class)
            {
                case CatClassId.Fire: return FxId.SkillFire;
                case CatClassId.Wind: return FxId.SkillWind;
                case CatClassId.Light: return FxId.SkillLight;
                case CatClassId.Dark: return FxId.SkillDark;
                default: return null;
            }
        }
    }
}
