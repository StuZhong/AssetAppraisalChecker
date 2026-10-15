using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class DualDocumentReportCheckServiceTests
    {
        private readonly ReportCheckService _service = new ReportCheckService();

        [TestMethod]
        public async Task CheckAsync_OnlyDocumentA_PreservesSingleDocumentMode()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估基准日：2026年6月15日"))
            {
                ReportCheckResult result = await _service.CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, null, null));

                Assert.IsTrue(result.IsSuccess);
                Assert.IsFalse(result.IsCrossDocumentCheck);
                Assert.AreEqual(DocumentReadStatus.Success, result.DocumentReadStatus);
            }
        }

        [TestMethod]
        public async Task CheckAsync_EquivalentDocumentPair_DoesNotCreateCrossDocumentIssue()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估基准日：2026年6月15日"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "评估基准日：2026-06-15"))
            {
                ReportCheckResult result = await CheckPair(documentA, documentB);

                Assert.IsTrue(result.IsSuccess);
                Assert.IsTrue(result.IsCrossDocumentCheck);
                Assert.AreEqual(0, result.Issues.Count);
            }
        }

        [TestMethod]
        public async Task CheckAsync_BenchmarkDateMismatch_ReportsNeutralCrossDocumentEvidence()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估基准日：2026年6月15日"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "评估基准日：2026年5月31日"))
            {
                ReportCheckIssue issue = (await CheckPair(documentA, documentB))
                    .Issues.Single(item => item.FieldName == "评估基准日");

                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                Assert.AreEqual("跨文档不一致", issue.Summary);
                CollectionAssert.AreEquivalent(
                    new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).Distinct().ToArray());
                Assert.IsTrue(issue.Occurrences.All(item =>
                    !string.IsNullOrWhiteSpace(item.LocationText)));
            }
        }

        [TestMethod]
        public async Task CheckAsync_AssessmentValueMismatch_ReportsCrossDocumentMoneyIssue()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估价值为998.22万元"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "评估价值合计人民币1000万元"))
            {
                ReportCheckIssue issue = (await CheckPair(documentA, documentB))
                    .Issues.Single(item => item.FieldName == "评估价值");

                Assert.AreEqual(IssueType.MoneyConflict, issue.IssueType);
                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                Assert.IsTrue(issue.Occurrences.Any(item =>
                    item.NormalizedValue.Contains("9,982,200.00")));
                Assert.IsTrue(issue.Occurrences.Any(item =>
                    item.NormalizedValue.Contains("10,000,000.00")));
            }
        }

        [TestMethod]
        public async Task CheckAsync_ExistingMoneyFieldMismatch_UsesCoreCrossDocumentComparison()
        {
            using (SyntheticDocxFile documentA = CreateMoneyDocument("170万元"))
            using (SyntheticDocxFile documentB = CreateMoneyDocument("160万元"))
            {
                ReportCheckIssue issue = (await CheckPair(documentA, documentB))
                    .Issues.Single(item => item.IssueType == IssueType.MoneyConflict);

                Assert.AreEqual("跨文档不一致", issue.Summary);
                CollectionAssert.AreEquivalent(
                    new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).Distinct().ToArray());
            }
        }

        [TestMethod]
        public async Task CheckAsync_EvaluationMethodMismatch_ReportsCrossDocumentIssue()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "房屋建筑物采用成本法。"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "房屋建筑物采用市场法。"))
            {
                ReportCheckIssue issue = (await CheckPair(documentA, documentB))
                    .Issues.Single(item => item.FieldName == "评估方法");

                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                Assert.IsTrue(issue.Occurrences.Any(item => item.NormalizedValue.Contains("成本法")));
                Assert.IsTrue(issue.Occurrences.Any(item => item.NormalizedValue.Contains("市场法")));
            }
        }

        [TestMethod]
        public async Task CheckAsync_CorruptDocumentB_FailsClosedWithoutSingleDocumentDowngrade()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估基准日：2026年6月15日"))
            using (SyntheticDocxFile documentB = SyntheticDocxFile.CreateRaw(
                Encoding.UTF8.GetBytes("合成的损坏 DOCX")))
            {
                ReportCheckResult result = await CheckPair(documentA, documentB);

                Assert.IsFalse(result.IsSuccess);
                Assert.IsTrue(result.IsCrossDocumentCheck);
                Assert.AreEqual(DocumentReadStatus.InvalidDocx, result.DocumentBReadStatus);
                StringAssert.Contains(result.Message, "文档 B 解析失败");
            }
        }

        [TestMethod]
        public async Task CheckAsync_DocumentPair_RemainsByteIdentical()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估价值为998.22万元"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "评估价值合计人民币1000万元"))
            {
                string hashABefore = Hash(documentA.FilePath);
                string hashBBefore = Hash(documentB.FilePath);

                ReportCheckResult result = await CheckPair(documentA, documentB);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(hashABefore, Hash(documentA.FilePath));
                Assert.AreEqual(hashBBefore, Hash(documentB.FilePath));
            }
        }

        [TestMethod]
        public async Task CheckAsync_SwappingDocumentOrder_PreservesBusinessConclusion()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "评估基准日：2026年6月15日"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "评估基准日：2026年5月31日"))
            {
                ReportCheckIssue forward = (await CheckPair(documentA, documentB))
                    .Issues.Single(item => item.FieldName == "评估基准日");
                ReportCheckIssue reverse = (await CheckPair(documentB, documentA))
                    .Issues.Single(item => item.FieldName == "评估基准日");

                Assert.AreEqual(forward.Severity, reverse.Severity);
                Assert.AreEqual(forward.Summary, reverse.Summary);
                CollectionAssert.AreEquivalent(
                    forward.Occurrences.Select(item => item.NormalizedValue).ToArray(),
                    reverse.Occurrences.Select(item => item.NormalizedValue).ToArray());
            }
        }

        [TestMethod]
        public async Task CheckAsync_SingleDocumentMechanicalIssue_IsShownWithDocumentASource()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "本项目采用评估评估方法。"))
            {
                ReportCheckResult result = await _service.CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, null, null));

                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.Summary == "发现疑似连续重复词语");
                Assert.AreEqual("文档 A", issue.Occurrences.Single().DocumentLabel);
                Assert.AreEqual(IssueSeverity.Normal, issue.Severity);
                Assert.AreEqual("重复内容", new IssueViewModel(issue).IssueTypeDisplay);
            }
        }

        [TestMethod]
        public async Task CheckAsync_DualDocumentMechanicalIssues_AreCheckedIndependently()
        {
            using (SyntheticDocxFile documentA = CreateDocument(
                "本项目采用评估评估方法。"))
            using (SyntheticDocxFile documentB = CreateDocument(
                "本次评估结论。。"))
            {
                ReportCheckResult result = await CheckPair(documentA, documentB);

                ReportCheckIssue duplicate = result.Issues.Single(item =>
                    item.Summary == "发现疑似连续重复词语");
                ReportCheckIssue punctuation = result.Issues.Single(item =>
                    item.Summary == "发现明显连续重复标点");
                Assert.AreEqual("文档 A", duplicate.Occurrences.Single().DocumentLabel);
                Assert.AreEqual("文档 B", punctuation.Occurrences.Single().DocumentLabel);
            }
        }

        private Task<ReportCheckResult> CheckPair(
            SyntheticDocxFile documentA,
            SyntheticDocxFile documentB)
        {
            return _service.CheckAsync(new ReportCheckRequest(
                documentA.FilePath,
                documentB.FilePath,
                null));
        }

        private static SyntheticDocxFile CreateDocument(string text)
        {
            return SyntheticDocxFile.Create(new Paragraph(new Run(new Text(text))));
        }

        private static SyntheticDocxFile CreateMoneyDocument(string amount)
        {
            return SyntheticDocxFile.CreateWithStyles(
                new[]
                {
                    new Style(new StyleName { Val = "heading 1" })
                    {
                        Type = StyleValues.Paragraph,
                        StyleId = "Heading1"
                    }
                },
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                    new Run(new Text("评估结论"))),
                new Paragraph(new Run(new Text("本项目不含税评估净值" + amount + "。"))));
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }
}
