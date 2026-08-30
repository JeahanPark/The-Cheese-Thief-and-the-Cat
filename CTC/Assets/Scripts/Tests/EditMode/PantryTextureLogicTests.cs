using NUnit.Framework;
using CTC.Runtime.Map;
using UnityEngine;

namespace CTC.Tests
{
    public class PantryTextureLogicTests
    {
        [Test]
        public void PeriodicNoise_Wraps_At_Edges()
        {
            for (float v = 0.05f; v < 1f; v += 0.17f)
            {
                float left = PantryTextureLogic.PeriodicNoise(0f, v, 8, 42);
                float right = PantryTextureLogic.PeriodicNoise(1f, v, 8, 42);
                Assert.AreEqual(left, right, 1e-5f, $"v={v}에서 u=0과 u=1이 다르다 — 이음새가 생긴다");
            }
        }

        [Test]
        public void PlankColor_Wraps_Horizontally_And_Vertically()
        {
            var baseC = new Color(0.54f, 0.39f, 0.22f);
            for (float t = 0.03f; t < 1f; t += 0.13f)
            {
                Color l = PantryTextureLogic.PlankColor(0f, t, 8, 7, baseC);
                Color r = PantryTextureLogic.PlankColor(1f, t, 8, 7, baseC);
                Assert.AreEqual(l.r, r.r, 1e-4f);
                Color b = PantryTextureLogic.PlankColor(t, 0f, 8, 7, baseC);
                Color u = PantryTextureLogic.PlankColor(t, 1f, 8, 7, baseC);
                Assert.AreEqual(b.g, u.g, 1e-4f);
            }
        }

        [Test]
        public void PlankColor_Is_Deterministic()
        {
            var baseC = Color.white;
            Color a = PantryTextureLogic.PlankColor(0.37f, 0.61f, 10, 3, baseC);
            Color b = PantryTextureLogic.PlankColor(0.37f, 0.61f, 10, 3, baseC);
            Assert.AreEqual(a, b);
        }
    }
}
