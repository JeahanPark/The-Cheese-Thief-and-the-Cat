using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// **대장과 구현이 어긋나면 여기서 깨진다.** (전체 검수 3단계, 2026-09-09)
    ///
    /// 이 프로젝트가 이미 쓰는 전수 검사 관용구와 같은 모양이다 —
    /// 「클립 안 붙은 SfxId」를 전수로 세는 훅이 있듯, 여기서는 **선언과 부착을 전수로 맞댄다.**
    /// 새 개념이 0이라 외울 것이 없다.
    ///
    ///   ① 선언이 「구현됨」인데 부착이 없으면 → 깨진다 (없는 것을 있다고 적은 것)
    ///   ② 선언이 「미구현」인데 부착이 있으면 → 깨진다 (상태를 안 올린 것)
    ///   ③ 코드에 박힌 ID가 선언에 없으면 → 깨진다 (오타이거나 대장에 안 올린 것)
    ///   ④ 폐기 번호가 어딘가에 붙어 있으면 → 깨진다 (번호를 되살려 쓴 것)
    ///   ⑤ 번호가 밀리거나 비면 → 깨진다 (재사용 금지를 번호 연속성으로 강제한다)
    ///   ⑥ 생성 문서가 지금 선언과 다르면 → 깨지고, 새 파일을 옆에 뱉는다
    /// </summary>
    public class QaCaseRegistryTests
    {
        private static SortedDictionary<string, List<string>> Attachments()
        {
            return QaCaseLedger.ScanAttachments();
        }

        // ───────────────────────────── 대장 자신의 형태 ─────────────────────────────

        [Test]
        public void Ids_AreUniqueAndWellFormed()
        {
            var seen = new HashSet<string>();
            foreach (QaCase c in QaCases.ALL)
            {
                Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(c.Id, @"^[A-Z]-[0-9]{2}$"),
                    $"ID 형식이 아니다: {c.Id}");
                Assert.IsTrue(seen.Add(c.Id), $"ID가 중복됐다 — 번호는 영구다: {c.Id}");
                Assert.IsNotEmpty(c.What, $"{c.Id}: 케이스 본문이 비었다");
                if (c.Status != QaStatus.Retired)
                    Assert.IsNotEmpty(c.Catches,
                        $"{c.Id}: 「잡는 회귀」가 없다 — 그건 지워도 되는 케이스라는 뜻이다");
            }
        }

        /// <summary>
        /// **번호 재사용·밀기 금지를 코드로 강제한다.** 도메인마다 01부터 빈칸 없이 이어져야 한다 —
        /// 폐기한 케이스도 행을 남기므로 그 번호는 계속 차 있다. 행을 지우면 뒤가 밀리고 여기서 깨진다.
        /// </summary>
        [Test]
        public void Numbers_AreContiguousPerDomain_SoRetiredIdsStayReserved()
        {
            foreach (string domain in QaCases.DOMAINS)
            {
                var numbers = new List<int>();
                foreach (QaCase c in QaCases.ALL)
                    if (c.Domain == domain) numbers.Add(c.Number);

                Assert.Greater(numbers.Count, 0, $"도메인 {domain}에 케이스가 하나도 없다");
                numbers.Sort();
                for (int i = 0; i < numbers.Count; i++)
                    Assert.AreEqual(i + 1, numbers[i],
                        $"도메인 {domain}의 번호가 비거나 밀렸다 — 폐기해도 행은 남기고 번호는 비워 둔다. " +
                        $"실제: {string.Join(",", numbers.ConvertAll(n => n.ToString()).ToArray())}");
            }
        }

        [Test]
        public void ManualCases_CarryTheirProcedureAndExpectation()
        {
            foreach (QaCase c in QaCases.ALL)
            {
                if (c.IsAuto || c.Status == QaStatus.Retired) continue;
                Assert.IsNotEmpty(c.Procedure, $"{c.Id}: 수동인데 절차가 없다 — 그건 케이스가 아니다");
                Assert.IsNotEmpty(c.Expected, $"{c.Id}: 수동인데 「무엇을 보면 통과인가」가 없다");
            }
        }

        // ───────────────────────────── 선언 ↔ 구현 드리프트 ─────────────────────────────

        /// <summary>① 「구현됨」이라 적힌 자동 케이스에는 부착된 구현이 반드시 있다.</summary>
        [Test]
        public void EveryImplementedAutoCase_HasAnAttachment()
        {
            SortedDictionary<string, List<string>> attach = Attachments();
            var orphans = new List<string>();
            foreach (QaCase c in QaCases.ALL)
            {
                if (!c.IsAuto || c.Status != QaStatus.Implemented) continue;
                if (!attach.ContainsKey(c.Id)) orphans.Add(c.Id);
            }
            Assert.IsEmpty(orphans,
                "「구현됨」이라 선언했는데 그 ID가 박힌 테스트·스위트가 없다 — 대장이 거짓말을 하고 있다: "
                + string.Join(", ", orphans.ToArray()));
        }

        /// <summary>② 「미구현」이라 적힌 케이스에 구현이 붙었으면 상태를 올려야 한다.</summary>
        [Test]
        public void EveryMissingCase_HasNoAttachment()
        {
            SortedDictionary<string, List<string>> attach = Attachments();
            var stale = new List<string>();
            foreach (QaCase c in QaCases.ALL)
            {
                if (c.Status != QaStatus.Missing) continue;
                List<string> files;
                if (attach.TryGetValue(c.Id, out files))
                    stale.Add($"{c.Id} → {string.Join(" · ", files.ToArray())}");
            }
            Assert.IsEmpty(stale,
                "구현이 붙었는데 대장이 아직 「미구현」이다 — QaCases의 상태를 올려라: "
                + string.Join(" / ", stale.ToArray()));
        }

        /// <summary>③ 코드에 박힌 ID는 반드시 선언에 있다(오타·미등재를 잡는다).</summary>
        [Test]
        public void EveryAttachedId_IsDeclaredInTheRegistry()
        {
            var unknown = new List<string>();
            foreach (KeyValuePair<string, List<string>> kv in Attachments())
            {
                if (QaCases.Find(kv.Key) == null)
                    unknown.Add($"{kv.Key} ({string.Join(" · ", kv.Value.ToArray())})");
            }
            Assert.IsEmpty(unknown,
                "선언에 없는 케이스 ID가 코드에 박혀 있다 — QaCases에 올리거나 오타를 고쳐라: "
                + string.Join(" / ", unknown.ToArray()));
        }

        /// <summary>④ 폐기 번호는 아무 데도 안 붙어 있다 — 번호를 되살려 쓰면 여기서 깨진다.</summary>
        [Test]
        public void RetiredIds_AreNotAttachedAnywhere()
        {
            SortedDictionary<string, List<string>> attach = Attachments();
            var revived = new List<string>();
            foreach (QaCase c in QaCases.ALL)
            {
                if (c.Status != QaStatus.Retired) continue;
                List<string> files;
                if (attach.TryGetValue(c.Id, out files))
                    revived.Add($"{c.Id} → {string.Join(" · ", files.ToArray())}");
            }
            Assert.IsEmpty(revived,
                "폐기 번호가 다시 쓰였다 — 번호는 재사용하지 않는다: " + string.Join(" / ", revived.ToArray()));
        }

        /// <summary>
        /// 선언한 층과 실제로 붙은 자리가 같다 — 「EditMode라고 적고 파이썬에만 붙인」 어긋남을 잡는다.
        /// </summary>
        [Test]
        public void AttachmentLocation_MatchesTheDeclaredLayer()
        {
            SortedDictionary<string, List<string>> attach = Attachments();
            var wrong = new List<string>();
            foreach (QaCase c in QaCases.ALL)
            {
                List<string> files;
                if (!attach.TryGetValue(c.Id, out files)) continue;

                bool ok = false;
                foreach (string f in files)
                {
                    string name = Path.GetFileName(f);
                    switch (c.Layer)
                    {
                        case QaLayer.EditMode: ok |= f.EndsWith(".cs"); break;
                        case QaLayer.Wesqa: ok |= name.StartsWith("qa_v"); break;
                        case QaLayer.Pytest: ok |= name.StartsWith("test_"); break;
                        case QaLayer.Manual: break;
                    }
                }
                if (!ok) wrong.Add($"{c.Id}({c.Layer}) → {string.Join(" · ", files.ToArray())}");
            }
            Assert.IsEmpty(wrong,
                "선언한 층과 부착된 자리가 다르다 — QaCases의 층을 고치거나 부착을 옮겨라: "
                + string.Join(" / ", wrong.ToArray()));
        }

        // ───────────────────────────── 생성 문서 드리프트 ─────────────────────────────

        /// <summary>
        /// ⑥ 대장 마크다운이 **지금 선언으로 다시 생성한 것과 같다.**
        /// 다르면 옆에 새 파일을 뱉고 깨진다 — 사람이 손으로 맞출 일이 없다.
        /// </summary>
        [Test]
        public void Ledger_IsUpToDate()
        {
            string expected = QaCaseLedger.Render(Attachments());
            string actual = QaCaseLedger.ReadLedger();

            if (actual == expected) return;

            string sibling = QaCaseLedger.RepoRoot() + "/"
                + QaCaseLedger.LEDGER_PATH.Replace(".md", ".generated.md");
            Directory.CreateDirectory(Path.GetDirectoryName(sibling));
            File.WriteAllText(sibling, expected, new UTF8Encoding(false));

            Assert.Fail(
                "대장 문서가 선언과 어긋난다(문서는 생성물이다 — 손으로 고치지 마라).\n"
                + (actual == null ? "디스크에 파일이 없다.\n" : FirstDifference(actual, expected))
                + "지금 상태로 만든 전문을 옆에 뱉었다: " + sibling + "\n"
                + "메뉴 `Tools/CTC/Generate QA Case Ledger`를 눌러 갱신해라.");
        }

        private static string FirstDifference(string _actual, string _expected)
        {
            string[] a = _actual.Split('\n');
            string[] e = _expected.Split('\n');
            int n = a.Length < e.Length ? a.Length : e.Length;
            for (int i = 0; i < n; i++)
            {
                if (a[i] == e[i]) continue;
                return $"첫 차이 {i + 1}번째 줄\n  디스크: {a[i]}\n  선언  : {e[i]}\n";
            }
            return $"줄 수가 다르다 — 디스크 {a.Length} · 선언 {e.Length}\n";
        }
    }
}
