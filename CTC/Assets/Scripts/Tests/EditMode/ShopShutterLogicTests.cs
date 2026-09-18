using CTC.Runtime.Economy;
using CTC.Runtime.Map;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 상점 철문(2026-09-19) — 고양이가 가까이 오면 쾅 닫히고(살금살금이면 더 가까이 와야), 멀어지면 드르륵 열린다.
    /// 닫힌 동안 그 상점에서는 못 산다.
    /// </summary>
    public class ShopShutterLogicTests
    {
        private const float CLOSE = 5f, SNEAK = 2.5f, OPEN = 6.5f, REOPEN = 1.5f;

        [NUnit.Framework.Property("CaseId", "C-39")]
        [Test]
        public void Alarms_WalkingAt5m_SneakingOnlyAt2p5m()
        {
            Assert.IsTrue(ShopShutterLogic.Alarms(4.9f, false, CLOSE, SNEAK), "걷는 고양이는 5m 안이면 들킨다");
            Assert.IsFalse(ShopShutterLogic.Alarms(5.1f, false, CLOSE, SNEAK));
            Assert.IsFalse(ShopShutterLogic.Alarms(4.0f, true, CLOSE, SNEAK), "살금살금이면 4m까지 몰래 온다");
            Assert.IsTrue(ShopShutterLogic.Alarms(2.4f, true, CLOSE, SNEAK));
        }

        [NUnit.Framework.Property("CaseId", "C-39")]
        [Test]
        public void Closes_AtOnce_Reopens_OnlyAfterQuietTimeOutsideTheWiderRing()
        {
            Assert.IsTrue(ShopShutterLogic.NextClosed(false, true, true, 0f, REOPEN), "경보면 즉시 닫는다");
            Assert.IsFalse(ShopShutterLogic.NextClosed(false, false, true, 0f, REOPEN), "열린 채면 6.5m 안에 있어도 그대로 연다(경보 반경 밖)");
            Assert.IsTrue(ShopShutterLogic.NextClosed(true, false, true, 0f, REOPEN), "닫혔으면 6.5m 안에 있는 동안 닫아 둔다");
            Assert.IsTrue(ShopShutterLogic.NextClosed(true, false, false, REOPEN - 0.1f, REOPEN), "밖으로 나가도 잠깐은 기다린다");
            Assert.IsFalse(ShopShutterLogic.NextClosed(true, false, false, REOPEN, REOPEN), "1.5초 조용하면 연다");
            Assert.Less(CLOSE, OPEN, "닫힘 반경 < 열림 반경 — 경계에서 서성여도 철문이 떨지 않는다");
        }

        [NUnit.Framework.Property("CaseId", "C-39")]
        [Test]
        public void Shutter_SlamsFast_RisesSlow_AndResumesMidway()
        {
            Assert.AreEqual(0f, ShopShutterLogic.Lowered(0f, true, 0f), 1e-4f);
            Assert.AreEqual(1f, ShopShutterLogic.Lowered(0f, true, ShopShutterLogic.CLOSE_SECONDS), 1e-4f);
            Assert.Less(ShopShutterLogic.Lowered(0f, true, ShopShutterLogic.CLOSE_SECONDS * 0.5f), 0.5f, "쾅 — 가속이라 처음엔 느리다");
            Assert.AreEqual(0f, ShopShutterLogic.Lowered(1f, false, ShopShutterLogic.OPEN_SECONDS), 1e-4f);
            Assert.Less(ShopShutterLogic.CLOSE_SECONDS, ShopShutterLogic.OPEN_SECONDS, "닫기는 빠르고 열기는 느리다");
            Assert.AreEqual(0.6f, ShopShutterLogic.Lowered(0.6f, true, 0f), 1e-4f, "도중에 뒤집혀도 그 자리에서 이어 간다");
            Assert.IsTrue(ShopShutterLogic.Settled(true, ShopShutterLogic.CLOSE_SECONDS));
        }

        [NUnit.Framework.Property("CaseId", "C-39")]
        [Test]
        public void ClosedShop_DenyTextSaysWhy()
        {
            string text = TechLogic.DenyText(TechDeny.ShopClosed, Runtime.Lobby.RoleId.Mouse, 0, 5);
            StringAssert.Contains("철문", text);
            StringAssert.Contains("고양이", text);
        }

        [NUnit.Framework.Property("CaseId", "D-26")]
        [Test]
        public void Booth_FacesTheMapCenter()
        {
            Assert.AreEqual(0f, ShopSpotLogic.FacingYaw(Vector2.zero), 1e-4f, "가운데에 붙어 있으면 북쪽");
            Vector3 forward = Quaternion.Euler(0f, ShopSpotLogic.FacingYaw(new Vector2(10f, 0f)), 0f) * Vector3.forward;
            Assert.Less((forward - Vector3.left).magnitude, 1e-4f, "동쪽 끝 상점은 서쪽(가운데)을 본다");
            forward = Quaternion.Euler(0f, ShopSpotLogic.FacingYaw(new Vector2(0f, -8f)), 0f) * Vector3.forward;
            Assert.Less((forward - Vector3.forward).magnitude, 1e-4f, "남쪽 끝 상점은 북쪽(가운데)을 본다");
        }
    }
}
