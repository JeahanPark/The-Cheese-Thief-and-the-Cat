using System.IO;
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

        private static void AssertStatesExist(string _controllerFile)
        {
            string path = Path.Combine(Application.dataPath, CONTROLLER_DIR, _controllerFile);
            if (!File.Exists(path))
            {
                Assert.Ignore($"컨트롤러 원본이 없다: {path}");
                return;
            }

            // 줄 단위로 정확히 견준다 — Shapekey 레이어의 Eyes_Spin 같은 이름에 섞여 들지 않게.
            string[] lines = File.ReadAllLines(path);
            foreach (string state in ONE_SHOT_STATES)
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
