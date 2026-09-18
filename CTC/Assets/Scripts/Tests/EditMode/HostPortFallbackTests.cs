using System.Collections.Generic;
using System.IO;
using CTC.Runtime.Networking;
using NUnit.Framework;

namespace CTC.Tests.EditMode
{
    /// <summary>
    /// A-17 — 기본 포트가 막히면 호스트가 다음 포트로 올라가고, 그 번호가 공유 파일을 거쳐 클라에 닿는다.
    /// 소켓·NGO 없이 순수 부분(<see cref="HostPortFallback.NextFreeAttempt"/>·포트 파일 읽기/쓰기)만 잰다.
    /// </summary>
    public class HostPortFallbackTests
    {
        private string m_TempDir;

        [SetUp]
        public void SetUp()
        {
            m_TempDir = Path.Combine(Path.GetTempPath(), "ctc-port-test-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_TempDir)) Directory.Delete(m_TempDir, true);
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void NextFreeAttempt_BasePortFree_UsesBase()
        {
            Assert.AreEqual(0, HostPortFallback.NextFreeAttempt(0, _ => true));
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void NextFreeAttempt_BasePortBlocked_ClimbsToNextFree()
        {
            var blocked = new HashSet<int> { HostPortFallback.BASE_PORT, HostPortFallback.BASE_PORT + 1 };
            int attempt = HostPortFallback.NextFreeAttempt(0, _p => !blocked.Contains(_p));

            Assert.AreEqual(2, attempt);
            Assert.AreEqual(HostPortFallback.BASE_PORT + 2, HostPortFallback.BASE_PORT + attempt);
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void NextFreeAttempt_ResumesAfterFailedStart()
        {
            // 빈 포트였는데 StartHost가 실패하면 그다음 시도부터 다시 찾는다 — 같은 포트를 또 찌르지 않는다.
            Assert.AreEqual(4, HostPortFallback.NextFreeAttempt(3 + 1, _ => true));
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void NextFreeAttempt_AllBlocked_ReturnsMinusOne_WithinMaxAttempts()
        {
            var asked = new List<int>();
            int attempt = HostPortFallback.NextFreeAttempt(0, _p => { asked.Add(_p); return false; });

            Assert.AreEqual(-1, attempt);
            Assert.AreEqual(HostPortFallback.MAX_ATTEMPTS, asked.Count, "시도 상한을 넘어 찾으면 안 된다");
            Assert.AreEqual(HostPortFallback.BASE_PORT + HostPortFallback.MAX_ATTEMPTS - 1, asked[asked.Count - 1]);
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void PortFile_WriteThenRead_RoundTrips_CreatingFolder()
        {
            string path = Path.Combine(m_TempDir, "nested", "host_port.txt");
            int chosen = HostPortFallback.BASE_PORT + 3;

            HostPortFallback.WritePortFile(path, chosen);   // 호스트
            Assert.IsTrue(File.Exists(path), "폴더가 없어도 만들어 써야 한다");
            Assert.AreEqual(chosen, HostPortFallback.ReadPortFile(path));   // 클라
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void PortFile_Overwrite_ClientSeesLatestHost()
        {
            string path = Path.Combine(m_TempDir, "host_port.txt");
            HostPortFallback.WritePortFile(path, HostPortFallback.BASE_PORT + 5);
            HostPortFallback.WritePortFile(path, HostPortFallback.BASE_PORT + 1);

            Assert.AreEqual(HostPortFallback.BASE_PORT + 1, HostPortFallback.ReadPortFile(path));
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void PortFile_MissingOrGarbage_FallsBackToBasePort()
        {
            Assert.AreEqual(HostPortFallback.BASE_PORT,
                HostPortFallback.ReadPortFile(Path.Combine(m_TempDir, "none.txt")));

            Directory.CreateDirectory(m_TempDir);
            string garbage = Path.Combine(m_TempDir, "garbage.txt");
            File.WriteAllText(garbage, "not-a-port");
            Assert.AreEqual(HostPortFallback.BASE_PORT, HostPortFallback.ReadPortFile(garbage));

            string padded = Path.Combine(m_TempDir, "padded.txt");
            File.WriteAllText(padded, "  7790\r\n");
            Assert.AreEqual(7790, HostPortFallback.ReadPortFile(padded), "앞뒤 공백·줄바꿈은 무시한다");
        }

        [NUnit.Framework.Property("CaseId", "A-17")]
        [Test]
        public void PortFilePath_IsSharedTempLocation()
        {
            // 호스트와 클론이 같은 파일을 봐야 한다 — 프로젝트별 경로가 아니라 시스템 temp 아래 고정 이름이다.
            StringAssert.StartsWith(Path.GetTempPath(), HostPortFallback.PortFilePath);
            StringAssert.EndsWith("host_port.txt", HostPortFallback.PortFilePath);
        }
    }
}
