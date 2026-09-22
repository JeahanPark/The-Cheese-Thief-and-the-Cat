using CTC.Runtime.Managers;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// 채팅 예약어(2026-08-20). `/`를 치면 힌트가 뜨고, 완성해 보내면 채팅 대신 실행된다.
    /// 모르는 /문자열은 명령이 아니라 그냥 채팅이다 — 오타를 삼키면 안 된다.
    /// </summary>
    public class ChatCommandLogicTests
    {
        [Test]
        public void Match_SlashShowsEverything()
        {
            Assert.AreEqual(ChatCommandLogic.ALL.Length, ChatCommandLogic.Match("/").Count);
            Assert.AreEqual("/send log", ChatCommandLogic.Match("/")[0].Name);
        }

        [Test]
        public void Match_NarrowsWhileTyping()
        {
            Assert.AreEqual(1, ChatCommandLogic.Match("/se").Count);
            Assert.AreEqual(1, ChatCommandLogic.Match("/send log").Count, "다 쳐도 힌트는 남는다");
            Assert.AreEqual(0, ChatCommandLogic.Match("/x").Count);
            Assert.AreEqual(0, ChatCommandLogic.Match("/send logx").Count);
        }

        [Test]
        public void Match_IgnoresCaseAndEdgeSpaces()
        {
            Assert.AreEqual(1, ChatCommandLogic.Match("/SEND").Count);
            Assert.AreEqual(1, ChatCommandLogic.Match("  /se").Count, "앞 공백은 실수다 — 버린다");
        }

        [Test]
        public void MoveSelection_WrapsBothWays()
        {
            // 위아래로 고르기(2026-09-23) — 끝에서 누르면 반대쪽 끝으로 돈다
            Assert.AreEqual(1, ChatCommandLogic.MoveSelection(0, +1, 3));
            Assert.AreEqual(0, ChatCommandLogic.MoveSelection(2, +1, 3), "맨 아래에서 아래 → 맨 위");
            Assert.AreEqual(2, ChatCommandLogic.MoveSelection(0, -1, 3), "맨 위에서 위 → 맨 아래");
            Assert.AreEqual(0, ChatCommandLogic.MoveSelection(0, -1, 1), "하나뿐이면 제자리");
            Assert.AreEqual(0, ChatCommandLogic.MoveSelection(5, +1, 0), "후보가 없으면 0");
        }

        [Test]
        public void IsComplete_OnlyWhenTheWholeCommandIsTyped()
        {
            // 치는 중이면 Enter는 채우기, 다 쳤으면 실행
            Assert.IsFalse(ChatCommandLogic.IsComplete("/"));
            Assert.IsFalse(ChatCommandLogic.IsComplete("/se"));
            Assert.IsTrue(ChatCommandLogic.IsComplete("/send log"));
            Assert.IsTrue(ChatCommandLogic.IsComplete(" /SEND  log "), "대소문자·공백은 실행 판정과 같게 본다");
            Assert.IsFalse(ChatCommandLogic.IsComplete("안녕"));
            Assert.IsFalse(ChatCommandLogic.IsComplete(null));
        }

        [Test]
        public void Match_PlainChatShowsNothing()
        {
            Assert.AreEqual(0, ChatCommandLogic.Match("안녕").Count);
            Assert.AreEqual(0, ChatCommandLogic.Match("").Count);
            Assert.AreEqual(0, ChatCommandLogic.Match(null).Count);
            Assert.AreEqual(0, ChatCommandLogic.Match("고양이 1/2로 깎았다").Count, "문장 속 /는 명령이 아니다");
        }

        [Test]
        public void IsSendLog_AcceptsHumanVariations()
        {
            Assert.IsTrue(ChatCommandLogic.IsSendLog("/send log"));
            Assert.IsTrue(ChatCommandLogic.IsSendLog("/SEND LOG"));
            Assert.IsTrue(ChatCommandLogic.IsSendLog("/send  log"), "공백 두 번은 같은 명령이다");
            Assert.IsTrue(ChatCommandLogic.IsSendLog(" /send log "));
        }

        [Test]
        public void IsSendLog_RejectsPartialsAndChat()
        {
            Assert.IsFalse(ChatCommandLogic.IsSendLog("/send"));
            Assert.IsFalse(ChatCommandLogic.IsSendLog("/send logs"));
            Assert.IsFalse(ChatCommandLogic.IsSendLog("send log"));
            Assert.IsFalse(ChatCommandLogic.IsSendLog(null));
        }

        // ---- 전송 조각 계산 ----

        [Test]
        public void TailStart_KeepsSmallFilesWhole()
        {
            Assert.AreEqual(0L, ChatCommandLogic.TailStart(100, 512 * 1024));
            Assert.AreEqual(0L, ChatCommandLogic.TailStart(0, 512 * 1024));
            Assert.AreEqual(0L, ChatCommandLogic.TailStart(512 * 1024, 512 * 1024), "딱 상한이면 전문");
        }

        [Test]
        public void TailStart_CutsOnlyTheHead()
        {
            // 에러는 끝에 있다 — 큰 파일은 머리만 버린다
            Assert.AreEqual(88L, ChatCommandLogic.TailStart(512 * 1024 + 88, 512 * 1024));
        }

        [Test]
        public void ChunkCount_RoundsUp()
        {
            Assert.AreEqual(0, ChatCommandLogic.ChunkCount(0, 16384));
            Assert.AreEqual(1, ChatCommandLogic.ChunkCount(1, 16384));
            Assert.AreEqual(1, ChatCommandLogic.ChunkCount(16384, 16384));
            Assert.AreEqual(2, ChatCommandLogic.ChunkCount(16385, 16384));
            Assert.AreEqual(32, ChatCommandLogic.ChunkCount(512 * 1024, 16384), "상한 512KB = 32조각");
        }
    }
}
