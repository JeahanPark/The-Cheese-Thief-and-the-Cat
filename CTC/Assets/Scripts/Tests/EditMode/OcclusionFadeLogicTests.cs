using System.Collections.Generic;
using CTC.Runtime.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class OcclusionFadeLogicTests
    {
        [Test]
        public void CastOrigin_MovesTowardTargetByRadius()
        {
            Vector3 origin = OcclusionFadeLogic.CastOrigin(Vector3.zero, new Vector3(0f, 0f, 10f), 0.25f);
            Assert.AreEqual(new Vector3(0f, 0f, 0.25f), origin);
        }

        [Test]
        public void CastOrigin_DegenerateDistance_ReturnsCamera()
        {
            Vector3 camera = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(camera, OcclusionFadeLogic.CastOrigin(camera, camera, 0.25f));
        }

        [Test]
        public void CastDistance_StopsBeforeTargetSurface()
        {
            // 10m 거리, 반경 0.25 → 시작점에서 0.25 전진하고 대상 앞 0.25에서 멈춘다
            Assert.AreEqual(9.5f, OcclusionFadeLogic.CastDistance(Vector3.zero, new Vector3(0f, 0f, 10f), 0.25f), 1e-4f);
        }

        [Test]
        public void CastDistance_NeverNegative()
        {
            Assert.AreEqual(0f, OcclusionFadeLogic.CastDistance(Vector3.zero, new Vector3(0f, 0f, 0.1f), 0.25f), 1e-4f);
        }

        [Test]
        public void CollectRestored_OnlyItemsThatLeftTheHitSet()
        {
            var faded = new List<string> { "a", "b", "c" };
            var hits = new HashSet<string> { "b" };
            var restored = new List<string>();

            OcclusionFadeLogic.CollectRestored(faded, hits, restored);

            CollectionAssert.AreEquivalent(new[] { "a", "c" }, restored);
        }

        [Test]
        public void CollectNewlyFaded_SkipsAlreadyFaded()
        {
            var hits = new List<string> { "a", "b" };
            var alreadyFaded = new HashSet<string> { "a" };
            var newly = new List<string>();

            OcclusionFadeLogic.CollectNewlyFaded(hits, alreadyFaded, newly);

            CollectionAssert.AreEqual(new[] { "b" }, newly);
        }

        [Test]
        public void CollectNewlyFaded_SameHitTwoFrames_NoReswap()
        {
            // 연속 프레임에 같은 벽이 잡혀도 다시 스왑하지 않아야 머티리얼 churn이 없다
            var hits = new List<string> { "wall" };
            var alreadyFaded = new HashSet<string> { "wall" };
            var newly = new List<string>();

            OcclusionFadeLogic.CollectNewlyFaded(hits, alreadyFaded, newly);

            Assert.IsEmpty(newly);
        }

        [Test]
        public void EmptyHits_RestoresEverything()
        {
            var faded = new List<string> { "a", "b" };
            var restored = new List<string>();

            OcclusionFadeLogic.CollectRestored(faded, new HashSet<string>(), restored);

            CollectionAssert.AreEquivalent(faded, restored);
        }

        [Test]
        public void OutputCollections_AreClearedBeforeFill()
        {
            var restored = new List<string> { "stale" };
            OcclusionFadeLogic.CollectRestored(new List<string>(), new HashSet<string>(), restored);
            Assert.IsEmpty(restored);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-39")]
        public void AimPoint_RisesFromFeet_NeverBelow()
        {
            // 발끝만 보면 윗몸을 가린 물체를 놓친다(2026-09-20) — 겨누는 점은 발 위다
            Vector3 feet = new Vector3(3f, 0f, -2f);
            Assert.AreEqual(new Vector3(3f, 0.7f, -2f), OcclusionFadeLogic.AimPoint(feet, 0.7f));
            Assert.AreEqual(feet, OcclusionFadeLogic.AimPoint(feet, -1f), "음수 높이는 발로 친다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "B-39")]
        public void FadeableGroup_RejectsEmptyAndWholeMap()
        {
            Assert.IsFalse(OcclusionFadeLogic.IsFadeableGroup(0), "그림이 없는 판정 상자");
            Assert.IsTrue(OcclusionFadeLogic.IsFadeableGroup(1));
            Assert.IsTrue(OcclusionFadeLogic.IsFadeableGroup(20), "상점 부스 한 채");
            Assert.IsFalse(OcclusionFadeLogic.IsFadeableGroup(OcclusionFadeLogic.MAX_GROUP_RENDERERS + 1),
                "맵 루트까지 올라가 통째로 흐리면 안 된다");
        }
    }
}
