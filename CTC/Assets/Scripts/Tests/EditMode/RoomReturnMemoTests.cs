using NUnit.Framework;
using CTC.Runtime.Room;

namespace CTC.Tests
{
    /// <summary>
    /// 판에서 돌아온 대기실이 방 설정(봇 수·맵)을 되살리는 쪽지(2026-09-19).
    /// 돌아오면 봇이 0으로 돌아가 혼자+봇이면 시작이 안 눌렸다(사용자 보고).
    /// </summary>
    public class RoomReturnMemoTests
    {
        [Test]
        [NUnit.Framework.Property("CaseId", "A-31")]
        public void Returning_From_Match_Restores_Settings_Once()
        {
            RoomReturnMemo.Remember(3, 2);
            RoomReturnMemo.MarkReturning();

            Assert.IsTrue(RoomReturnMemo.TryConsume(out int bots, out int map));
            Assert.AreEqual(3, bots);
            Assert.AreEqual(2, map);
            Assert.IsFalse(RoomReturnMemo.TryConsume(out _, out _), "한 번 꺼낸 설정은 다시 안 쓴다");
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "A-31")]
        public void Room_Without_Return_Mark_Stands_Fresh()
        {
            // 시작만 하고 대기실 복귀를 안 거쳤다 — 메인에서 새로 연 방은 지난 방의 설정을 물려받지 않는다.
            RoomReturnMemo.Remember(3, 2);

            Assert.IsFalse(RoomReturnMemo.TryConsume(out _, out _));
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "A-31")]
        public void Second_Return_Without_New_Start_Restores_Nothing()
        {
            RoomReturnMemo.Remember(4, 1);
            RoomReturnMemo.MarkReturning();
            Assert.IsTrue(RoomReturnMemo.TryConsume(out _, out _));

            // 새로 시작하지 않고 다시 복귀 표시만 왔다 — 쓸 설정이 없다.
            RoomReturnMemo.MarkReturning();
            Assert.IsFalse(RoomReturnMemo.TryConsume(out _, out _));
        }

        [Test]
        [NUnit.Framework.Property("CaseId", "A-31")]
        public void New_Start_Clears_A_Pending_Return()
        {
            // 복귀 표시가 남은 채로 새 판을 시작하면 그 표시는 지난 판 것이다 — 새 판의 복귀를 기다린다.
            RoomReturnMemo.Remember(2, 0);
            RoomReturnMemo.MarkReturning();
            RoomReturnMemo.Remember(5, 1);

            Assert.IsFalse(RoomReturnMemo.TryConsume(out _, out _));
            RoomReturnMemo.MarkReturning();
            Assert.IsTrue(RoomReturnMemo.TryConsume(out int bots, out int map));
            Assert.AreEqual(5, bots);
            Assert.AreEqual(1, map);
        }
    }
}
