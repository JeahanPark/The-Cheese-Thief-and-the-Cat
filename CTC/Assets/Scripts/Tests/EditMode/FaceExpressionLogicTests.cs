using System.IO;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 원샷 → 표정 매핑(2026-09-13). 매핑 자체보다 **기각 목록이 새지 않는가**가 요점이다 —
    /// 체력·잠복을 얼굴로 흘리는 표정이 한 줄 추가로 들어오는 것을 여기서 막는다(기획서 §11).
    /// </summary>
    public class FaceExpressionLogicTests
    {
        private const string CONTROLLER_DIR =
            "Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations";

        [Test]
        public void VisibleOneShots_GetTheirFace()
        {
            Assert.AreEqual(FaceExpressionLogic.SHRINK, FaceExpressionLogic.ForOneShot(AnimationStateLogic.Hit));
            Assert.AreEqual(FaceExpressionLogic.RABID, FaceExpressionLogic.ForOneShot(AnimationStateLogic.Attack));
            Assert.AreEqual(FaceExpressionLogic.RABID, FaceExpressionLogic.ForOneShot(AnimationStateLogic.Frenzy));
            foreach (string s in new[] { AnimationStateLogic.CatSkillFire, AnimationStateLogic.CatSkillWind, AnimationStateLogic.CatSkillLight, AnimationStateLogic.CatSkillDark })
                Assert.AreEqual(FaceExpressionLogic.RABID, FaceExpressionLogic.ForOneShot(s), s);
            Assert.IsNull(FaceExpressionLogic.ForOneShot(AnimationStateLogic.Spin), "광분은 이제 Spin을 안 쓴다");
            Assert.AreEqual(FaceExpressionLogic.HAPPY, FaceExpressionLogic.ForOneShot(AnimationStateLogic.Eat));
            Assert.AreEqual(FaceExpressionLogic.EXCITED, FaceExpressionLogic.ForOneShot(AnimationStateLogic.Clicked));
        }

        [Test]
        public void MovementAndMouseActives_KeepTheFace()
        {
            string[] none =
            {
                AnimationStateLogic.Idle, AnimationStateLogic.Walk, AnimationStateLogic.Run,
                AnimationStateLogic.Shoot, AnimationStateLogic.ShootDraw, AnimationStateLogic.Dash,
                AnimationStateLogic.Sticky, AnimationStateLogic.Fortress, AnimationStateLogic.Warehouse, "없는상태", null,
            };
            foreach (string s in none) Assert.IsNull(FaceExpressionLogic.ForOneShot(s), s);
        }

        /// <summary>어떤 원샷도 기각한 표정으로 가지 않는다 — 모든 상태 이름을 훑는다.</summary>
        [Test]
        public void NoOneShot_MapsToARejectedFace()
        {
            foreach (string state in AnimationStateLogic.All)
            {
                string face = FaceExpressionLogic.ForOneShot(state);
                if (face == null) continue;
                CollectionAssert.DoesNotContain(FaceExpressionLogic.REJECTED, face,
                    $"'{state}'가 기각한 표정 '{face}'로 간다 — 없던 정보를 얼굴로 흘린다");
                CollectionAssert.Contains(FaceExpressionLogic.Used, face,
                    $"'{face}'가 Used에 없다 — QA가 그 표정을 '?해시'로 읽는다");
            }
        }

        [Test]
        public void UsedAndRejected_DoNotOverlap()
        {
            foreach (string face in FaceExpressionLogic.Used)
                CollectionAssert.DoesNotContain(FaceExpressionLogic.REJECTED, face);
        }

        [Test]
        [NUnit.Framework.Category("Asset")]
        public void UsedFaces_ExistInMouseAndCatControllers()
        {
            AssertStatesExist("AC_Mouse.controller");
            AssertStatesExist("AC_Cat.controller");
        }

        [Test]
        [NUnit.Framework.Category("Asset")]
        public void ShapekeyLayer_ExistsInMouseAndCatControllers()
        {
            foreach (string file in new[] { "AC_Mouse.controller", "AC_Cat.controller" })
            {
                string[] lines = ReadController(file);
                bool found = false;
                foreach (string line in lines)
                    if (line.Trim() == "m_Name: " + FaceExpressionLogic.LAYER_NAME) { found = true; break; }
                Assert.IsTrue(found, $"{file}에 '{FaceExpressionLogic.LAYER_NAME}' 레이어가 없다 — 표정 컴포넌트가 스스로 꺼진다");
            }
        }

        private static void AssertStatesExist(string _file)
        {
            string[] lines = ReadController(_file);
            foreach (string face in FaceExpressionLogic.Used)
            {
                bool found = false;
                foreach (string line in lines)
                    if (line.Trim() == "m_Name: " + face) { found = true; break; }
                Assert.IsTrue(found, $"{_file}에 표정 '{face}'가 없다 — CrossFade는 조용히 아무 일도 안 한다");
            }
        }

        private static string[] ReadController(string _file)
        {
            string path = Path.Combine(Application.dataPath, CONTROLLER_DIR, _file);
            if (!File.Exists(path)) Assert.Fail($"컨트롤러 원본이 없다: {path}");
            return File.ReadAllLines(path);
        }
    }
}
