using System;
using System.Collections.Generic;
using CTC.Runtime.Fx;
using CTC.Runtime.Player;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>기술 이펙트 표(2026-09-14) — 전직별 이펙트가 서로 다르고, 번호가 카탈로그 안에 있다.</summary>
    public class FxCatalogLogicTests
    {
        [NUnit.Framework.Property("CaseId", "B-26")]
        [Test]
        public void EveryClass_HasDistinctAdvanceAndSkillFx_NoneHasNothing()
        {
            var seen = new HashSet<FxId>();
            foreach (CatClassId c in new[] { CatClassId.Fire, CatClassId.Wind, CatClassId.Light, CatClassId.Dark })
            {
                Assert.IsTrue(seen.Add(FxCatalogLogic.AdvanceFor(c).Value), $"전직 빛 중복: {c}");
                Assert.IsTrue(seen.Add(FxCatalogLogic.SkillFor(c).Value), $"스킬 이펙트 중복: {c}");
            }
            Assert.IsNull(FxCatalogLogic.AdvanceFor(CatClassId.None));
            Assert.IsNull(FxCatalogLogic.SkillFor(CatClassId.None));
        }

        [NUnit.Framework.Property("CaseId", "B-26")]
        [Test]
        public void AllIds_FitInCatalog_AndAreUnique()
        {
            var values = (FxId[])Enum.GetValues(typeof(FxId));
            var set = new HashSet<int>();
            foreach (FxId id in values)
            {
                Assert.That((int)id, Is.InRange(0, FxCatalog.SIZE - 1), $"{id}가 카탈로그 밖이다");
                Assert.IsTrue(set.Add((int)id), $"{id} 번호 중복");
            }
        }
    }
}
