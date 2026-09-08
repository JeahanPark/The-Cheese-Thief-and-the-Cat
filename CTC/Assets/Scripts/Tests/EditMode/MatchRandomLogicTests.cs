using System.Collections.Generic;
using CTC.Runtime.Match;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// 매치 시드의 순수 부분(<see cref="MatchRandom.NextSeed"/>·<see cref="MatchRandom.SubSeed"/>).
    /// MonoBehaviour 쪽(복제·명부)은 여기서 안 본다 — 그건 플레이모드·wesqa의 몫이다.
    /// </summary>
    public class MatchRandomLogicTests
    {
        private static readonly int[] SALTS =
            { MatchRandom.SALT_SHOP, MatchRandom.SALT_SOLD_OUT, MatchRandom.SALT_TICKET };

        // ===== SubSeed — 서버와 클라가 여기서 만난다 =====

        [Test]
        public void SubSeed_IsDeterministic()
        {
            Assert.AreEqual(MatchRandom.SubSeed(12345, MatchRandom.SALT_SHOP),
                MatchRandom.SubSeed(12345, MatchRandom.SALT_SHOP));
        }

        [Test]
        public void SubSeed_DiffersBySalt()
        {
            int shop = MatchRandom.SubSeed(12345, MatchRandom.SALT_SHOP);
            int soldOut = MatchRandom.SubSeed(12345, MatchRandom.SALT_SOLD_OUT);
            int ticket = MatchRandom.SubSeed(12345, MatchRandom.SALT_TICKET);

            Assert.AreNotEqual(shop, soldOut);
            Assert.AreNotEqual(shop, ticket);
            Assert.AreNotEqual(soldOut, ticket);
        }

        [Test]
        public void SubSeed_DiffersBySeed()
        {
            Assert.AreNotEqual(MatchRandom.SubSeed(1, MatchRandom.SALT_SHOP),
                MatchRandom.SubSeed(2, MatchRandom.SALT_SHOP));
        }

        /// <summary>시드 0(로비·실험실)이 들어와도 셋이 **서로 다른 쓸 수 있는 수**를 받아야 한다.</summary>
        [Test]
        public void SubSeed_NeverPassesZeroThrough()
        {
            var seen = new HashSet<int>();
            foreach (int salt in SALTS)
            {
                int sub = MatchRandom.SubSeed(0, salt);
                Assert.AreNotEqual(0, sub, $"소금 {salt}이 0을 그대로 흘렸다");
                Assert.IsTrue(seen.Add(sub), $"소금 {salt}이 앞선 소금과 같은 수를 냈다");
            }
        }

        /// <summary>어떤 시드가 들어와도 0은 안 나온다 — 0은 "아직 안 굴렸다"로 읽히는 자리다.</summary>
        [Test]
        public void SubSeed_IsNeverZero()
        {
            for (int seed = -500; seed <= 500; seed++)
            {
                foreach (int salt in SALTS)
                    Assert.AreNotEqual(0, MatchRandom.SubSeed(seed, salt), $"seed={seed} salt={salt}");
            }
        }

        /// <summary>수열이 갈렸는지 — 같은 시드 범위를 소금 셋으로 돌려 겹치는 값이 거의 없어야 한다.</summary>
        [Test]
        public void SubSeed_SaltsDoNotCorrelate()
        {
            int collisions = 0;
            for (int seed = 1; seed <= 1000; seed++)
            {
                if (MatchRandom.SubSeed(seed, MatchRandom.SALT_SHOP)
                    == MatchRandom.SubSeed(seed, MatchRandom.SALT_SOLD_OUT)) collisions++;
            }
            Assert.AreEqual(0, collisions);
        }

        // ===== NextSeed — 판을 다시 열면 값이 바뀐다 =====

        [Test]
        public void NextSeed_IsNeverZero()
        {
            for (int prev = -200; prev <= 200; prev++)
            {
                for (int entropy = -3; entropy <= 3; entropy++)
                    Assert.AreNotEqual(0, MatchRandom.NextSeed(prev, entropy), $"prev={prev} e={entropy}");
            }
        }

        /// <summary>2판째가 1판과 같은 시드를 받으면 무작위가 통째로 죽는다 — 엔트로피가 같아도 달라야 한다.</summary>
        [Test]
        public void NextSeed_NeverRepeatsPrevious()
        {
            for (int prev = -200; prev <= 200; prev++)
            {
                for (int entropy = -3; entropy <= 3; entropy++)
                    Assert.AreNotEqual(prev, MatchRandom.NextSeed(prev, entropy), $"prev={prev} e={entropy}");
            }
        }

        [Test]
        public void NextSeed_IsDeterministicForSameInputs()
        {
            Assert.AreEqual(MatchRandom.NextSeed(7, 99), MatchRandom.NextSeed(7, 99));
        }

        [Test]
        public void NextSeed_DiffersByEntropy()
        {
            Assert.AreNotEqual(MatchRandom.NextSeed(7, 99), MatchRandom.NextSeed(7, 100));
        }

        /// <summary>같은 엔트로피로 판을 연달아 열어도(같은 프레임·같은 시계) 시드가 계속 굴러야 한다.</summary>
        [Test]
        public void NextSeed_KeepsMovingWithFrozenEntropy()
        {
            var seen = new HashSet<int>();
            int seed = 0;
            for (int match = 0; match < 20; match++)
            {
                seed = MatchRandom.NextSeed(seed, 4242);
                Assert.IsTrue(seen.Add(seed), $"{match + 1}판째에서 시드가 돌아왔다: {seed}");
            }
        }
    }
}
