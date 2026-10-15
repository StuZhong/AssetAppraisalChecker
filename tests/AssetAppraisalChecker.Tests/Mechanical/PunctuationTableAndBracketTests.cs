using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class PunctuationTableAndBracketTests
    {
        private const string Introduction = "本次依据合成条件完成费用测算，相关费用率如下表所示：   ";

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TableIntroduction_DirectOrOneCaption_SupportsSplitRuns(bool caption)
        {
            var intro = new Paragraph(new Run(new Text(Introduction.Substring(0, 12))),
                new Run(new Text(Introduction.Substring(12)) { Space = SpaceProcessingModeValues.Preserve }));
            OpenXmlElement[] elements = caption
                ? new OpenXmlElement[] { intro, P("合成费用率来源表"), Table() }
                : new OpenXmlElement[] { intro, Table() };
            using (var file = SyntheticDocxFile.Create(elements))
                Assert.AreEqual(0, Check(file).Count(i => i.Kind == MechanicalIssueKind.MissingEndingPunctuation));
        }

        [TestMethod]
        [DataRow("InterveningProse")]
        [DataRow("Blank")]
        [DataRow("NonCaption")]
        [DataRow("NoTable")]
        public void TableIntroduction_DoesNotSearchPastUnverifiedStructure(string type)
        {
            OpenXmlElement[] elements = type == "InterveningProse"
                ? new OpenXmlElement[] { P(Introduction), P("合成费用率来源表"), P("此处还有其他说明。"), Table() }
                : type == "Blank" ? new OpenXmlElement[] { P(Introduction), new Paragraph(), P("合成费用率来源表"), Table() }
                : type == "NonCaption" ? new OpenXmlElement[] { P(Introduction), P("此处是普通说明。"), Table() }
                : new OpenXmlElement[] { P(Introduction), P("合成费用率来源表") };
            using (var file = SyntheticDocxFile.Create(elements))
                Assert.AreEqual(1, Check(file).Count(i => i.Kind == MechanicalIssueKind.MissingEndingPunctuation));
        }

        [TestMethod]
        public void CompleteProseMissingStop_IsStillDetectedEvenBeforeCaptionAndTable()
        {
            const string text = "本次已经完成合成资料核对，全部相关测算内容已经确认";
            using (var file = SyntheticDocxFile.Create(P(text), P("合成费用率来源表"), Table()))
            {
                var issue = Check(file).Single(i => i.Kind == MechanicalIssueKind.MissingEndingPunctuation);
                Assert.AreEqual(text, issue.Occurrences.Single().RawText);
            }
        }

        [TestMethod]
        [DataRow("合计(元）")]
        [DataRow("合计（元)")]
        public void MixedBracketWidth_IsNotMissingBracket_AndRetainsExactOffsets(string text)
        {
            using (var file = SyntheticDocxFile.Create(new Table(new TableRow(new TableCell(
                new Paragraph(new Run(new Text(text.Substring(0, 3))), new Run(new Text(text.Substring(3)))))))))
            {
                var issues = Check(file);
                Assert.AreEqual(0, issues.Count(i => i.Kind == MechanicalIssueKind.UnpairedChineseBracket));
                var issue = issues.Single(i => i.Kind == MechanicalIssueKind.BracketWidthMismatch);
                var occurrence = issue.Occurrences.Single();
                Assert.AreEqual(text, occurrence.RawText);
                Assert.IsTrue(occurrence.Evidence.Contains("左括号位置（从0开始）=2"));
                Assert.IsTrue(occurrence.Evidence.Contains("右括号位置（从0开始）=4"));
                Assert.IsTrue(occurrence.Evidence.Contains("字符偏移=4"));
                Assert.IsTrue(occurrence.Evidence.Contains("长度=1"));
                Assert.AreEqual(0, occurrence.Location.TableIndex);
                Assert.AreEqual(0, occurrence.Location.RowIndex);
                Assert.AreEqual(0, occurrence.Location.CellIndex);
                Assert.IsTrue(issue.Reason.Contains("并非缺少括号"));
                Assert.IsTrue(issue.SuggestedAction.Contains("无需补加括号"));
                Assert.AreEqual("标点问题", new MechanicalIssueMapper().Map(new[] { issue }, 0).Single().IssueTypeDisplayOverride);
            }
        }

        [TestMethod]
        [DataRow("合计（元）")]
        [DataRow("合计(元)")]
        [DataRow("说明（合成计算(元)口径）")]
        public void CorrectBracketPairs_AreNotReported(string text)
        {
            using (var file = SyntheticDocxFile.Create(P(text)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        [DataRow("合计（元", 2)]
        [DataRow("合计）元（", 2)]
        [DataRow("说明(元)及（测试", 6)]
        public void TrulyMissingOrReversedBrackets_AreStillDetected(string text, int offset)
        {
            using (var file = SyntheticDocxFile.Create(P(text)))
            {
                var issue = Check(file).Single(i => i.Kind == MechanicalIssueKind.UnpairedChineseBracket);
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Contains("字符偏移=" + offset));
            }
        }

        [TestMethod]
        public void ProductionDualDocument_PreservesBracketSourceAndInputHashes()
        {
            using (var a = SyntheticDocxFile.Create(P(Introduction), P("合成费用率来源表"), Table()))
            using (var b = SyntheticDocxFile.Create(new Table(new TableRow(new TableCell(P("合计(元）"))))))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var result = new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(a.FilePath, b.FilePath, null)).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.Issues.Count(i => i.GovernanceKind == "MissingEndingPunctuation"));
                Assert.AreEqual(0, result.Issues.Count(i => i.GovernanceKind == "UnpairedChineseBracket"));
                var issue = result.Issues.Single(i => i.GovernanceKind == "BracketWidthMismatch");
                Assert.AreEqual("文档 B", issue.Occurrences.Single().DocumentLabel);
                Assert.AreEqual(0, issue.Occurrences.Single().Location.TableIndex);
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[1], issue.Occurrences.Single());
                Assert.IsNotNull(target);
                Assert.AreEqual(4, target.Offset);
                Assert.AreEqual(1, target.Length);
                Assert.AreEqual("）", target.SpanText);
                Assert.IsTrue(WordTextMatcher.MatchesPrefix(target, "合计(元"));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        private static Table Table() => new Table(new TableRow(new TableCell(P("合成栏目"))));
        private static System.Collections.Generic.IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file)
        {
            var read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(read.IsSuccess);
            return new PunctuationChecker().Check(new MechanicalCheckContext(file.FilePath, read, "文档 B"));
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
