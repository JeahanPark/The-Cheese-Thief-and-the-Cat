using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class AnimationStateLogicTests
    {
        [Test]
        public void BelowWalkThreshold_ReturnsIdle()
        {
            Assert.AreEqual("Idle_A", AnimationStateLogic.FromSpeed(0.05f, 0.1f, 3.5f));
        }

        [Test]
        public void BetweenThresholds_ReturnsWalk()
        {
            Assert.AreEqual("Walk", AnimationStateLogic.FromSpeed(1.5f, 0.1f, 3.5f));
        }

        [Test]
        public void AtOrAboveRunThreshold_ReturnsRun()
        {
            Assert.AreEqual("Run", AnimationStateLogic.FromSpeed(3.5f, 0.1f, 3.5f));
        }

        [Test]
        public void ExactlyWalkThreshold_ReturnsWalk()
        {
            Assert.AreEqual("Walk", AnimationStateLogic.FromSpeed(0.1f, 0.1f, 3.5f));
        }

        // ===== 팩 클립 원샷 매핑(테크 v3 계획 2b Task 5) =====
        //
        // CrossFadeInFixedTime은 없는 상태 이름을 받아도 **에러도 경고도 내지 않는다** —
        // 화면에서 아무 일이 안 일어나는 것으로만 알 수 있다. 그래서 철자를 여기서 못 박고,
        // 아래에서 컨트롤러 원본에 그 이름이 실제로 있는지까지 본다.

        private const string CONTROLLER_DIR =
            "Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations";

        private static readonly string[] ONE_SHOT_STATES =
        {
            AnimationStateLogic.Roll, AnimationStateLogic.Bounce, AnimationStateLogic.Jump,
            AnimationStateLogic.Spin, AnimationStateLogic.Clicked, AnimationStateLogic.Eat,
        };

        [Test]
        public void OneShotStateNames_MatchPackSpelling()
        {
            Assert.AreEqual("Roll", AnimationStateLogic.Roll);
            Assert.AreEqual("Bounce", AnimationStateLogic.Bounce);
            Assert.AreEqual("Jump", AnimationStateLogic.Jump);
            Assert.AreEqual("Spin", AnimationStateLogic.Spin);
            Assert.AreEqual("Clicked", AnimationStateLogic.Clicked);
            Assert.AreEqual("Eat", AnimationStateLogic.Eat);
        }

        /// <summary>
        /// 쥐 액티브의 자작 클립 철자(2026-09-13). 컨트롤러에 있는지는 <c>MouseOnly</c> 목록으로
        /// <c>MouseGunVisualLogicTests</c>가 이미 본다 — 여기서는 **호출부가 쓰는 글자**만 못 박는다.
        /// </summary>
        [Test]
        public void MouseActiveClipNames_AreSelfMade()
        {
            Assert.AreEqual("Dash", AnimationStateLogic.Dash);
            Assert.AreEqual("Sticky", AnimationStateLogic.Sticky);
            Assert.AreEqual("Fortress", AnimationStateLogic.Fortress);
            Assert.AreEqual("Warehouse", AnimationStateLogic.Warehouse);
            CollectionAssert.IsSubsetOf(
                new[] { AnimationStateLogic.Dash, AnimationStateLogic.Sticky, AnimationStateLogic.Fortress, AnimationStateLogic.Warehouse },
                AnimationStateLogic.MouseOnly,
                "쥐 액티브 클립이 MouseOnly에서 빠지면 Bake Mouse Gun이 AC_Mouse에 상태를 안 만든다");
        }

        [Test]
        public void OneShotStates_ExistInMouseController()
        {
            AssertStatesExist("AC_Mouse.controller");
        }

        [Test]
        public void OneShotStates_ExistInCatController()
        {
            AssertStatesExist("AC_Cat.controller");
        }

        // ===== 이름 → 상태 되돌리기 목록(2026-09-09 감사 B-04) =====
        //
        // `Animator`는 이름이 아니라 해시를 준다. `PlayerAnimationDriver.PlayingState`는
        // `AnimationStateLogic.All`을 후보로 훑어 글자로 되돌린다 — 목록에서 빠진 상태는
        // 실제로 돌고 있어도 "?해시"로 나와, QA가 「안 걸렸다」로 오독한다.
        // 상수를 새로 넣고 목록에 안 넣는 것이 정확히 그 사고다.

        [Test]
        public void All_ContainsEveryDeclaredStateConstant()
        {
            var declared = new List<string>();
            foreach (FieldInfo f in typeof(AnimationStateLogic).GetFields(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                declared.Add((string)f.GetRawConstantValue());
            }

            Assert.IsNotEmpty(declared, "상수를 하나도 못 찾았다 — 이 테스트가 항진명제가 됐다");
            CollectionAssert.AreEquivalent(declared, AnimationStateLogic.All,
                "선언한 상태 상수와 All 목록이 어긋났다 — 빠진 상태는 QA에서 '?해시'로 보인다");
        }

        [Test]
        public void All_HasNoDuplicates()
        {
            var seen = new HashSet<string>();
            foreach (string s in AnimationStateLogic.All)
            {
                Assert.IsTrue(seen.Add(s), $"All에 '{s}'가 두 번 있다");
            }
        }

        [Test]
        public void Shared_StatesExistInBothControllers()
        {
            AssertStatesExist("AC_Mouse.controller", AnimationStateLogic.Shared);
            AssertStatesExist("AC_Cat.controller", AnimationStateLogic.Shared);
        }

        /// <summary>
        /// <c>Shared</c>가 <c>All</c>에서 쥐 전용만 뺀 것인지 — 이게 어긋나면 위 테스트가
        /// **검사 범위를 조용히 잃는다**(예: Shared가 빈 배열이면 전부 통과한다).
        /// </summary>
        [Test]
        public void Shared_IsAllMinusMouseOnly()
        {
            var expected = new List<string>();
            foreach (string s in AnimationStateLogic.All)
            {
                if (System.Array.IndexOf(AnimationStateLogic.MouseOnly, s) < 0
                    && System.Array.IndexOf(AnimationStateLogic.CatOnly, s) < 0) expected.Add(s);
            }

            Assert.IsNotEmpty(AnimationStateLogic.MouseOnly, "쥐 전용이 비었다 — 분리가 무의미해졌다");
            Assert.IsNotEmpty(AnimationStateLogic.CatOnly, "고양이 전용이 비었다 — 분리가 무의미해졌다");
            CollectionAssert.AreEqual(expected, AnimationStateLogic.Shared);
        }

        /// <summary>
        /// 쥐 전용 상태가 **고양이 컨트롤러에는 없어야** 한다(2026-09-13).
        ///
        /// 「AC_Mouse에 있나」는 <c>MouseGunVisualLogicTests.ShootStates_ExistInMouseController</c>가
        /// 이미 같은 <c>MouseOnly</c> 목록으로 덮고 있다 — 2026-09-13에 가짜 이름을 넣어
        /// 확인했다(그쪽이 빨개졌다). **여기서 다시 세지 않는다.**
        ///
        /// 안 덮여 있던 것은 반대쪽이다: 쥐 전용이 AC_Cat에 섞여 들어가면 고양이가
        /// 총 쏘고 대시하는 상태를 갖게 되는데, 그건 아무 테스트도 안 보고 있었다.
        /// </summary>
        [Test]
        public void MouseOnly_StatesAreAbsentFromCatController()
        {
            AssertStatesAbsent("AC_Cat.controller", AnimationStateLogic.MouseOnly);
        }

        /// <summary>고양이 전용(광분·전직 스킬, 2026-09-13)은 AC_Cat에 **있고** AC_Mouse에는 **없어야** 한다.</summary>
        [Test]
        public void CatOnly_ExistInCat_AbsentFromMouse()
        {
            AssertStatesExist("AC_Cat.controller", AnimationStateLogic.CatOnly);
            AssertStatesAbsent("AC_Mouse.controller", AnimationStateLogic.CatOnly);
            // 전직별로 갈라지기 전의 한 클립(CatSkill)이 남으면 클립 없는 빈 상태가 된다.
            AssertStatesAbsent("AC_Cat.controller", new[] { "CatSkill" });
        }

        /// <summary>전직마다 **다른** 스킬 동작이고, 전부 고양이 전용 목록에 있다(2026-09-13).</summary>
        [Test]
        public void CatSkillFor_EachClassHasItsOwnCatOnlyClip()
        {
            var seen = new HashSet<string>();
            foreach (CatClassId c in new[] { CatClassId.Fire, CatClassId.Wind, CatClassId.Light, CatClassId.Dark })
            {
                string clip = AnimationStateLogic.CatSkillFor(c);
                Assert.IsNotNull(clip, c.ToString());
                Assert.IsTrue(seen.Add(clip), $"{c}가 다른 전직과 같은 동작 '{clip}'을 쓴다");
                CollectionAssert.Contains(AnimationStateLogic.CatOnly, clip, $"{clip}이 CatOnly에 없다 — AC_Cat에 상태가 안 생긴다");
            }
            Assert.IsNull(AnimationStateLogic.CatSkillFor(CatClassId.None));
        }

        [Test]
        public void MouseAndCatOnly_DoNotOverlap()
        {
            foreach (string s in AnimationStateLogic.CatOnly)
                CollectionAssert.DoesNotContain(AnimationStateLogic.MouseOnly, s);
        }

        private static void AssertStatesExist(string _controllerFile)
        {
            AssertStatesExist(_controllerFile, ONE_SHOT_STATES);
        }

        private static void AssertStatesAbsent(string _controllerFile, string[] _states)
        {
            string path = Path.Combine(Application.dataPath, CONTROLLER_DIR, _controllerFile);
            if (!File.Exists(path))
            {
                Assert.Ignore($"컨트롤러 원본이 없다: {path}");
                return;
            }

            string[] lines = File.ReadAllLines(path);
            foreach (string state in _states)
            {
                foreach (string line in lines)
                {
                    Assert.AreNotEqual("m_Name: " + state, line.Trim(),
                        $"{_controllerFile}에 '{state}'가 들어갔다 — 이 컨트롤러 몫이 아니다(쥐 전용·고양이 전용이 섞였다)");
                }
            }
        }

        private static void AssertStatesExist(string _controllerFile, string[] _states)
        {
            string path = Path.Combine(Application.dataPath, CONTROLLER_DIR, _controllerFile);
            if (!File.Exists(path))
            {
                Assert.Ignore($"컨트롤러 원본이 없다: {path}");
                return;
            }

            // 줄 단위로 정확히 견준다 — Shapekey 레이어의 Eyes_Spin 같은 이름에 섞여 들지 않게.
            string[] lines = File.ReadAllLines(path);
            foreach (string state in _states)
            {
                bool found = false;
                foreach (string line in lines)
                {
                    if (line.Trim() != "m_Name: " + state) continue;
                    found = true;
                    break;
                }
                Assert.IsTrue(found,
                    $"{_controllerFile}에 '{state}' 상태가 없다 — 이름이 틀어지면 원샷은 조용히 죽는다");
            }
        }
    }
}
