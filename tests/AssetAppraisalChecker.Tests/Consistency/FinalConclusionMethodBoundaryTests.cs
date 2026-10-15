using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class FinalConclusionMethodBoundaryTests
    {
        [TestMethod]
        [DataRow("资产基础法")]
        [DataRow("收益法")]
        [DataRow("市场法")]
        [DataRow("重置成本法")]
        public void MethodCalculations_AreRetainedButNotComparedAsFinal(string method)
        {
            using (var file = Create(
                "本项目采用" + method + "测算的评估价值为160万元。",
                "本项目最终采用的评估价值为170万元。"))
            {
                var document = new DocxDocumentReader().Read(file.FilePath);
                var occurrences = new MoneyOccurrenceExtractor().Extract(document);
                Assert.AreEqual(2, occurrences.Count);
                Assert.AreEqual(0, new ConsistencyEngine().Check(occurrences).Issues.Count);
                var facts = new AssessmentValueFactExtractor().Extract(document, "synthetic");
                Assert.AreEqual(1, facts.Count);
                Assert.AreEqual(1700000m, facts[0].NormalizedAmountYuan);
            }
        }

        [TestMethod]
        public void LocalClause_DoesNotExcludeSeparateFinalConclusion()
        {
            using (var file = Create(
                "本项目采用收益法测算的评估价值为160万元；本项目最终采用的评估价值为170万元。",
                "本项目最终采用的评估价值为180万元。"))
            {
                var occurrences = new MoneyOccurrenceExtractor().Extract(new DocxDocumentReader().Read(file.FilePath));
                var issue = new ConsistencyEngine().Check(occurrences).Issues.Single();
                CollectionAssert.AreEquivalent(new[] { 1700000m, 1800000m },
                    issue.Occurrences.Select(item => item.NormalizedAmountYuan).ToArray());
            }
        }

        [TestMethod]
        [DataRow("评估价值")]
        [DataRow("评估原值")]
        [DataRow("评估净值")]
        public void ExplicitlyAdoptedSingleMethod_RemainsComparable(string label)
        {
            using (var file = Create(
                "本项目最终采用收益法测算的" + label + "为170万元。",
                "本项目最终采用的" + label + "为160万元。"))
            {
                var occurrences = new MoneyOccurrenceExtractor().Extract(new DocxDocumentReader().Read(file.FilePath));
                Assert.AreEqual(1, new ConsistencyEngine().Check(occurrences).Issues.Count);
            }
        }

        [TestMethod]
        [DataRow("本项目未最终采用收益法测算的评估价值为160万元。")]
        [DataRow("本项目收益法和市场法测算的评估价值为160万元并作为最终评估结论。")]
        public void NegatedOrAmbiguousAdoption_DoesNotPromoteMethodResult(string calculation)
        {
            using (var file = Create(calculation, "本项目最终采用的评估价值为170万元。"))
            {
                var document = new DocxDocumentReader().Read(file.FilePath);
                Assert.AreEqual(0, new ConsistencyEngine().Check(new MoneyOccurrenceExtractor().Extract(document)).Issues.Count);
                Assert.AreEqual(1, new AssessmentValueFactExtractor().Extract(document, "synthetic").Count);
            }
        }

        [TestMethod]
        public async Task SummaryBodyAndExplanation_EquivalentUnitsAndChineseAmounts_NoConflictAndReadOnly()
        {
            using (var a = SyntheticDocxFile.CreateWithStyles(new[] {
                new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "Heading1" }
            }, Heading("评估摘要"), new Paragraph(new Run(new Text("本项目不含税评估净值为人民币壹佰柒拾万元整。"))),
                Heading("评估结论"), new Paragraph(new Run(new Text("本项目不含税评估净值为170万元。")))))
            using (var b = Create("本项目不含税评估净值为1,700,000元。"))
            {
                string beforeA = Hash(a.FilePath), beforeB = Hash(b.FilePath);
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.Issues.Count(item => item.IssueType == IssueType.MoneyConflict));
                Assert.AreEqual(beforeA, Hash(a.FilePath));
                Assert.AreEqual(beforeB, Hash(b.FilePath));
            }
        }

        [TestMethod]
        public async Task FinalConclusionMutation_CrossDocumentConflictRetainsBothSourcesAndLocation()
        {
            using (var a = Create("本项目不含税评估净值为170万元。", "本项目不含税评估净值为170万元。"))
            using (var b = Create("本项目不含税评估净值为160万元。"))
            {
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                var issue = result.Issues.Single(item => item.IssueType == IssueType.MoneyConflict);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).Distinct().ToArray());
                Assert.IsTrue(issue.Occurrences.All(item => !string.IsNullOrEmpty(item.LocationText)));
            }
        }

        [TestMethod]
        public async Task DifferentMethodCalculations_DoNotCreateDesktopFinalValueConflict()
        {
            using (var a = Create("本项目不含税评估净值为170万元。"))
            using (var b = Create("本项目采用资产基础法测算的不含税评估净值为170万元。",
                "本项目采用收益法测算的不含税评估净值为160万元。", "本项目不含税评估净值为170万元。"))
            {
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.Issues.Count(item => item.IssueType == IssueType.MoneyConflict));
            }
        }

        private static SyntheticDocxFile Create(params string[] paragraphs)
        {
            return SyntheticDocxFile.CreateWithStyles(new[] {
                new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "Heading1" }
            }, new[] { Heading("评估结论") }
                .Concat(paragraphs.Select(text => new Paragraph(new Run(new Text(text))))).ToArray());
        }

        private static Paragraph Heading(string text)
        {
            return new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text(text)));
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return System.BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
