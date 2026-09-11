using CTC.Editor;
using NUnit.Framework;

namespace CTC.Tests
{
    public class ClipParityLogicTests
    {
        private static CurveSample S(string _key, params float[] _values)
        {
            return new CurveSample(_key, _values);
        }

        [Test]
        public void SameKeysSameValues_IsIdentical()
        {
            var a = new[] { S("Rig/root:m_LocalPosition.x", 0f, 1f, 2f) };
            var b = new[] { S("Rig/root:m_LocalPosition.x", 0f, 1f, 2f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsTrue(r.IsIdentical, r.ToText());
        }

        [Test]
        public void KeyMissingInB_IsReportedAsMissing()
        {
            var a = new[] { S("Rig/root:x", 0f), S("Rig/root/body:x", 0f) };
            var b = new[] { S("Rig/root:x", 0f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsFalse(r.IsIdentical);
            CollectionAssert.AreEqual(new[] { "Rig/root/body:x" }, r.MissingInB);
            CollectionAssert.IsEmpty(r.ExtraInB);
        }

        [Test]
        public void KeyOnlyInB_IsReportedAsExtra()
        {
            var a = new[] { S("Rig/root:x", 0f) };
            var b = new[] { S("Rig/root:x", 0f), S("Rig/root/body_end:x", 0f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsFalse(r.IsIdentical);
            CollectionAssert.AreEqual(new[] { "Rig/root/body_end:x" }, r.ExtraInB);
            CollectionAssert.IsEmpty(r.MissingInB);
        }

        [Test]
        public void ValueBeyondEpsilon_IsReportedWithIndexAndDelta()
        {
            var a = new[] { S("Rig/root:x", 0f, 0f, 0f) };
            var b = new[] { S("Rig/root:x", 0f, 0.5f, 0f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsFalse(r.IsIdentical);
            Assert.AreEqual(1, r.ValueMismatches.Count);
            StringAssert.Contains("Rig/root:x", r.ValueMismatches[0]);
            StringAssert.Contains("[1]", r.ValueMismatches[0]);
        }

        [Test]
        public void ValueWithinEpsilon_IsNotReported()
        {
            var a = new[] { S("Rig/root:x", 0f, 1.0000f) };
            var b = new[] { S("Rig/root:x", 0f, 1.0005f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsTrue(r.IsIdentical, r.ToText());
        }

        [Test]
        public void SampleCountMismatch_IsReported()
        {
            var a = new[] { S("Rig/root:x", 0f, 1f) };
            var b = new[] { S("Rig/root:x", 0f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsFalse(r.IsIdentical);
            Assert.AreEqual(1, r.ValueMismatches.Count);
            StringAssert.Contains("샘플 수", r.ValueMismatches[0]);
        }

        [Test]
        public void EmptyBothSides_IsNotSilentlyIdentical()
        {
            ParityReport r = ClipParityLogic.Compare(new CurveSample[0], new CurveSample[0], 1e-3f);

            Assert.IsFalse(r.IsIdentical,
                "커브가 0개인 두 클립을 '같다'고 하면 배선이 죽은 것을 통과시킨다");
        }

        [Test]
        public void DuplicateKeyInA_IsReportedAndNotDoubleCounted()
        {
            var a = new[] { S("Rig/root:x", 0f), S("Rig/root:x", 0f) };
            var b = new[] { S("Rig/root:x", 0f) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsFalse(r.IsIdentical, r.ToText());
            Assert.AreEqual(1, r.DuplicateKeys.Count, r.ToText());
            StringAssert.Contains("Rig/root:x", r.DuplicateKeys[0]);
            Assert.AreEqual(1, r.ComparedCurves, "중복 키가 있어도 고유 키 기준으로 한 번만 세야 한다");
        }

        [Test]
        public void DuplicateKeyMissingInB_IsListedOnce()
        {
            var a = new[] { S("Rig/root/body:x", 0f), S("Rig/root/body:x", 0f) };
            var b = new CurveSample[0];

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            CollectionAssert.AreEqual(new[] { "Rig/root/body:x" }, r.MissingInB);
        }

        [Test]
        public void IdenticalInfinities_AreNotReported()
        {
            var a = new[] { S("Rig/root:x", 0f, float.PositiveInfinity) };
            var b = new[] { S("Rig/root:x", 0f, float.PositiveInfinity) };

            ParityReport r = ClipParityLogic.Compare(a, b, 1e-3f);

            Assert.IsTrue(r.IsIdentical, r.ToText());
        }
    }
}
