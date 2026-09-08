using CTC.Runtime.Bots;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// **계측기 자신의 집계 규칙**을 잰다(2026-09-09 감사 1단계, 케이스 E-13·E-14).
    ///
    /// 이 프로젝트가 겪은 사고의 뿌리는 「계측기가 무테스트였다」는 것이다.
    /// 「첫 발견」이 최솟값이 아니라 목록의 첫 값이었고, 우회율은 길을 포기하면 만점이 됐다 —
    /// 둘 다 **잘못된 방향으로 좋아 보이는** 거짓말이라 눈으로는 안 잡힌다.
    /// </summary>
    public class BotStatsLogicTests
    {
        // ───────────────────── 첫 발견 (E-13) ─────────────────────

        [Test]
        public void FirstSight_NoSample_IsNotZero()
        {
            // 「아무도 못 봤다」가 0초(=시작하자마자 봤다)로 읽히면 안 된다.
            Assert.IsFalse(BotStatsLogic.NoSight.Exists);
            Assert.Less(BotStatsLogic.NoSight.Seconds, 0f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "E-13")]
        public void FirstSight_TakesTheEarliest_NotTheFirstInList()
        {
            // 고양이 A가 목록 앞이지만 30초, B가 10초 — 답은 10이어야 한다.
            BotStatsLogic.FirstSight best = BotStatsLogic.NoSight;
            best = BotStatsLogic.FoldFirstSight(best, 30f, 40f);
            best = BotStatsLogic.FoldFirstSight(best, 10f, 12f);

            Assert.AreEqual(10f, best.Seconds, 0.001f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "E-13")]
        public void FirstSight_PairsSecondsWithItsOwnDistance()
        {
            // 시각과 거리를 따로 접으면 「A의 시각 + B의 거리」라는 없던 사건이 나온다.
            BotStatsLogic.FirstSight best = BotStatsLogic.NoSight;
            best = BotStatsLogic.FoldFirstSight(best, 30f, 40f);
            best = BotStatsLogic.FoldFirstSight(best, 10f, 12f);

            Assert.AreEqual(12f, best.Distance, 0.001f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "E-13")]
        public void FirstSight_SurvivesWhenTheEarlierCatIsGone()
        {
            // 죽은 고양이의 목격을 이월한 뒤, 살아남은 고양이(늦게 봄)를 접어도 값이 안 늦어진다.
            // 예전 버그는 정확히 반대였다 — **고양이가 죽을수록 첫 발견이 늦어졌다**.
            BotStatsLogic.FirstSight retired = BotStatsLogic.FoldFirstSight(BotStatsLogic.NoSight, 10f, 12f);
            BotStatsLogic.FirstSight best = BotStatsLogic.FoldFirstSight(retired, 30f, 40f);

            Assert.AreEqual(10f, best.Seconds, 0.001f);
            Assert.AreEqual(12f, best.Distance, 0.001f);
        }

        [Test]
        public void FirstSight_IgnoresNeverSaw()
        {
            BotStatsLogic.FirstSight best = BotStatsLogic.FoldFirstSight(BotStatsLogic.NoSight, 10f, 12f);
            best = BotStatsLogic.FoldFirstSight(best, -1f, -1f);

            Assert.AreEqual(10f, best.Seconds, 0.001f);
        }

        [Test]
        public void FirstSight_AllNeverSaw_StaysNone()
        {
            BotStatsLogic.FirstSight best = BotStatsLogic.FoldFirstSight(BotStatsLogic.NoSight, -1f, -1f);
            best = BotStatsLogic.FoldFirstSight(best, -1f, -1f);

            Assert.IsFalse(best.Exists);
        }

        // ───────────────────── 우회율 (E-14) ─────────────────────

        [Test]
        [NUnit.Framework.Property("CaseId", "E-14")]
        public void Detour_NoSample_IsNotPerfectScore()
        {
            // **여기가 T6의 핵심이다.** 표본이 없다는 건 「곧장 갔다」가 아니라
            // 「한 번도 길을 못 찾았다」다. 1.00을 주면 포기가 지표를 좋게 만든다.
            Assert.AreEqual(BotStatsLogic.NONE, BotStatsLogic.AverageDetour(0f, 0), 0.001f);
            Assert.AreNotEqual(1f, BotStatsLogic.AverageDetour(0f, 0));
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "E-14")]
        public void Detour_IsCumulativeAverage_NotTheLastOne()
        {
            // 1.0 · 2.0 · 3.0 → 2.0. 마지막 한 건만 보면 3.0이 나온다.
            Assert.AreEqual(2f, BotStatsLogic.AverageDetour(1f + 2f + 3f, 3), 0.001f);
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "E-14")]
        public void Detour_OldWayLetGiveUpsFlatterTheScore()
        {
            // 잰 경로 둘(1.4·1.6)의 평균은 1.5다.
            // 옛 방식은 포기 10건을 **1.00(만점)** 으로 같이 세어 평균을 1.08로 끌어내렸다 —
            // 길을 못 찾을수록 「곧장 간다」에 가까워 보였다. 표본 규칙은
            // <see cref="NavStatsLogicTests"/>가 잰다.
            const float SUM = 1.4f + 1.6f;
            float honest = BotStatsLogic.AverageDetour(SUM, 2);
            float oldWay = (SUM + 10f * 1f) / 12f;

            Assert.AreEqual(1.5f, honest, 0.001f);
            Assert.Less(oldWay, honest, "포기를 만점으로 세면 지표가 좋아진다 — 그게 T6이었다");
        }

        [Test]
        public void Shortfall_NoSample_IsNotZero()
        {
            // 0m는 「목표에 정확히 닿았다」다 — 「잰 적 없다」와 같으면 안 된다.
            Assert.AreEqual(BotStatsLogic.NONE, BotStatsLogic.AverageShortfall(0f, 0), 0.001f);
        }

        [Test]
        public void Shortfall_IsCumulativeAverage()
        {
            Assert.AreEqual(5f, BotStatsLogic.AverageShortfall(4f + 6f, 2), 0.001f);
        }

        // ───────────────────── 퇴장분 이월 (E-11·E-12) ─────────────────────

        [Test]
        [NUnit.Framework.Property("CaseId", "E-11")]
        public void Total_CountsRetiredBodies()
        {
            // 전멸한 판(산 쥐 0 · 퇴장 8)이 「0마리였다」로 읽히면 그 판이 통째로 사라진다.
            Assert.AreEqual(8, BotStatsLogic.TotalWithRetired(0, 8));
            Assert.AreEqual(8, BotStatsLogic.TotalWithRetired(3, 5));
        }
    }
}
