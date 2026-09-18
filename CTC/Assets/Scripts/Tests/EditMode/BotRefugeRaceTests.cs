using CTC.Runtime.Bots;
using CTC.Runtime.Info;
using CTC.Runtime.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 감사 D-11 — 쥐 봇의 요새 진입 경주 배율(<see cref="MouseBotLogic.REFUGE_RACE_RATIO"/>)이
    /// CSV `PlayerInfo`의 **실제** 고양이/쥐 이속 비 이상이다.
    ///
    /// 막는 것: CSV 이속만 고치고 봇 상수를 안 따라 고친다. 배율이 실제 비보다 작으면
    /// 봇이 「이긴다」고 계산한 경주를 실제로는 져서, 요새 앞에서 덮쳐진다.
    /// </summary>
    public class BotRefugeRaceTests
    {
        [NUnit.Framework.Property("CaseId", "D-11")]
        [Test]
        public void RefugeRaceRatio_IsAtLeastCsvCatToMouseSpeedRatio()
        {
            float cat = InfoManager.Instance.PlayerOf(RoleId.Cat).MoveSpeed;
            float mouse = InfoManager.Instance.PlayerOf(RoleId.Mouse).MoveSpeed;
            Assert.Greater(cat, 0f, "CSV 고양이 이속이 0 — PlayerInfo를 못 읽었다");
            Assert.Greater(mouse, 0f, "CSV 쥐 이속이 0 — PlayerInfo를 못 읽었다");

            float actual = cat / mouse;
            Assert.GreaterOrEqual(MouseBotLogic.REFUGE_RACE_RATIO + 1e-4f, actual,
                $"봇 경주 배율 {MouseBotLogic.REFUGE_RACE_RATIO} < CSV 이속 비 {actual:0.###} (고양이 {cat} / 쥐 {mouse})" +
                " — MouseBotLogic.REFUGE_RACE_RATIO를 올려라");
        }

        /// <summary>배율이 실제로 판정에 쓰이는지 — 경계 바로 안/밖에서 결과가 갈린다(상수만 보는 검산이 헛돌지 않게).</summary>
        [NUnit.Framework.Property("CaseId", "D-11")]
        [Test]
        public void ChooseRefuge_UsesRaceRatioAtTheBoundary()
        {
            float mine = 10f;
            float edge = mine * MouseBotLogic.REFUGE_RACE_RATIO + MouseBotLogic.REFUGE_RACE_MARGIN;
            var forts = new[] { Vector3.zero };
            var self = new Vector3(mine, 0f, 0f);

            Assert.AreEqual(-1, MouseBotLogic.ChooseRefuge(forts, self, new Vector3(-(edge - 0.5f), 0f, 0f)),
                "고양이가 경계보다 가까우면 경주에 진다고 본다");
            Assert.AreEqual(0, MouseBotLogic.ChooseRefuge(forts, self, new Vector3(-(edge + 0.5f), 0f, 0f)),
                "경계보다 멀면 들어간다");
        }
    }
}
