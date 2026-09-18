using System;
using System.Collections.Generic;
using CTC.Runtime.Economy;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>QA 대장 C-10·C-11·C-13·C-14 — 테크 등록·표·이속·스택 상한을 열거로 확인한다.</summary>
    public class TechRegistryCaseTests
    {
        private static IEnumerable<TechId> AllTechs()
        {
            foreach (TechId id in Enum.GetValues(typeof(TechId)))
            {
                if (id != TechId.None) yield return id;
            }
        }

        [NUnit.Framework.Property("CaseId", "C-10")]
        [Test]
        public void EveryTech_IsHandledByBothApplyCheckAndApply()
        {
            // 빈 몸이면 두 switch 모두 부작용 없이 끝난다 — 컴포넌트가 필요한 테크는 MissingComponent,
            // 몸에 붙일 게 없는 테크는 Ok, **switch가 모르는 테크만 Unhandled**다.
            var host = new GameObject("C10_BareHost");
            try
            {
                Assert.AreEqual(ShopController.ApplyResult.Unhandled, ShopController.CheckApplyLevel(host, TechId.None),
                    "None이 Unhandled가 아니면 이 테스트가 등록 누락을 구분하지 못한다");
                Assert.AreEqual(ShopController.ApplyResult.Unhandled, ShopController.ApplyLevel(host, TechId.None, 1));

                var problems = new List<string>();
                foreach (TechId id in AllTechs())
                {
                    ShopController.ApplyResult check = ShopController.CheckApplyLevel(host, id);
                    ShopController.ApplyResult apply = ShopController.ApplyLevel(host, id, 1);
                    if (check == ShopController.ApplyResult.Unhandled) problems.Add($"{id}: 적용 검사에 없음");
                    if (apply == ShopController.ApplyResult.Unhandled) problems.Add($"{id}: 적용 실행에 없음");
                    // 같은 몸에서 두 쪽의 요구가 같아야 한다 — 검사 통과 후 실행 실패면 치즈만 사라진다.
                    if (check != apply) problems.Add($"{id}: 검사 {check} ≠ 실행 {apply}");
                }
                Assert.IsEmpty(problems, string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [NUnit.Framework.Property("CaseId", "C-11")]
        [Test]
        public void EveryTech_HasCsvRowsForLevelsOneToThree()
        {
            var missing = new List<string>();
            foreach (TechId id in AllTechs())
            {
                for (int level = 1; level <= 3; level++)
                {
                    if (!TechLogic.RowOf(id, level).IsValid) missing.Add($"{id} Lv{level}");
                }
            }
            Assert.IsEmpty(missing, "TechInfo.csv에 행이 없다: " + string.Join(", ", missing));
        }

        /// <summary>
        /// 「쥐의 지속 최고 이속」 정의 — <c>PlayerMovement.CurrentSpeed</c>의 쥐 최댓값:
        /// 기본(PlayerInfo Mouse) + 강화 스택 0 + 빠른 발 만렙 P1, 바깥 배율 1.
        /// - 강화 스택(SpeedPerStack)은 CatStomp(고양이 테크)만 쌓는다 → 쥐는 0.
        /// - 바깥 배율은 감속(끈끈이)뿐이라 1 이하.
        /// - 제외: 대시(순간 이동, 지속 아님) · 잠행(×0.45 감속) · 바람 전직(고양이 전용).
        /// 직업(영웅·깡패)은 HP·피해 배율만 있고 이속 배율이 없다.
        /// </summary>
        [NUnit.Framework.Property("CaseId", "C-13")]
        [Test]
        public void MouseSustainedTopSpeed_IsBelowCatBaseSpeed()
        {
            Assert.AreEqual(RoleId.Cat, TechLogic.RowOf(TechId.CatStomp, 1).Role,
                "강화 스택을 쥐가 살 수 있게 되면 아래 정의(스택 0)가 틀린다");
            Assert.AreEqual(RoleId.Mouse, TechLogic.RowOf(TechId.FastFeet, 1).Role);

            float mouseBase = InfoManager.Instance.PlayerOf(RoleId.Mouse).MoveSpeed;
            float catBase = InfoManager.Instance.PlayerOf(RoleId.Cat).MoveSpeed;
            float fastFeetMax = TechLogic.ParamOf(TechId.FastFeet, TechLogic.MaxLevel(TechId.FastFeet), 1);
            Assert.Greater(fastFeetMax, 0f, "빠른 발 만렙 가산이 0이면 이 테스트가 뜻을 잃는다");

            float mouseTop = ShopLogic.MoveSpeed(mouseBase, 0) + fastFeetMax;
            Assert.Less(mouseTop, catBase,
                $"쥐 최고 이속 {mouseTop} (기본 {mouseBase} + 빠른 발 {fastFeetMax}) ≥ 고양이 기본 {catBase}");
        }

        [NUnit.Framework.Property("CaseId", "C-14")]
        [Test]
        public void MischiefStackCap_CoversMaxLevelOfStackTechs()
        {
            // 스택을 쌓는 테크 = ShopController.ApplyLevel에서 스택을 더하는 둘(발톱 → 피해 스택, 발 구르기 → 이속 스택).
            foreach (TechId id in new[] { TechId.CatClaw, TechId.CatStomp })
            {
                Assert.AreEqual(RoleId.Cat, TechLogic.RowOf(id, 1).Role, $"{id}는 장난기로 사는 고양이 테크여야 한다");
                int maxLevel = TechLogic.MaxLevel(id);
                Assert.Greater(maxLevel, 0, $"{id}: 표에 행이 없다");
                Assert.GreaterOrEqual(ShopLogic.MAX_UPGRADE_STACKS, maxLevel,
                    $"{id}: 스택 상한 {ShopLogic.MAX_UPGRADE_STACKS} < 최대 레벨 {maxLevel}");

                // 레벨 하나 = 스택 하나 — 만렙까지 매 구매마다 스택이 실제로 들어가야 한다.
                for (int level = 1; level <= maxLevel; level++)
                {
                    Assert.IsTrue(ShopLogic.CanAddStack(level - 1), $"{id} Lv{level} 구매 때 스택이 막힌다");
                }
            }
        }
    }
}
