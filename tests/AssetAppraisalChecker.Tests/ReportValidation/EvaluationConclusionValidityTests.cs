using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.ReportValidation;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.ReportValidation
{
    [TestClass]
    public sealed class EvaluationConclusionValidityTests
    {
        [TestMethod]
        [DataRow("2025年9月30日", "2026年9月29日")]
        [DataRow("2025年11月30日", "2026年11月29日")]
        [DataRow("2024年2月29日", "2025年2月28日")]
        [DataRow("2023年7月31日", "2024年7月30日")]
        [DataRow("2025年9月30日", "2026年10月7日")]
        [DataRow("2025年9月30日", "2026年9月20日")]
        public void CalendarYear_LeapYear_AndSmallDeviations_DoNotProduceIssue(string start, string end)
        {
            using (var file = SyntheticDocxFile.Create(P("评估基准日：" + start),
                P("评估结论使用有效期：自评估基准日后一年内有效，即自" + start + "至" + end + "止。")))
            {
                var result = Check(file.FilePath);
                Assert.IsTrue(result.IsEvaluable);
                Assert.AreEqual(0, result.Findings.Count);
            }
        }

        [TestMethod]
        [DataRow("2025年9月29日", EvaluationConclusionValidityAnomaly.ReversedRange)]
        [DataRow("2025年10月30日", EvaluationConclusionValidityAnomaly.ClearlyShortPeriod)]
        [DataRow("2027年9月29日", EvaluationConclusionValidityAnomaly.ClearlyLongPeriod)]
        public void ExplicitGrossAnomaly_IsDetectedWithAllThreeDates(string end, EvaluationConclusionValidityAnomaly expected)
        {
            const string textStart = "2025年9月30日";
            string text = "评估结论有效期：自" + textStart + "起至" + end + "止。";
            using (var file = SyntheticDocxFile.Create(P("评估基准日为" + textStart), P(text)))
            {
                var finding = Check(file.FilePath).Findings.Single();
                Assert.AreEqual(expected, finding.Anomaly);
                Assert.AreEqual(textStart, finding.Benchmark.RawText);
                Assert.AreEqual(end, finding.End.RawText);
                Assert.AreEqual(textStart, text.Substring(finding.Start.Offset, finding.Start.RawText.Length));
                Assert.AreEqual(end, text.Substring(finding.End.Offset, finding.End.RawText.Length));
            }
        }

        [TestMethod]
        [DataRow("许可证有效期：自2025年9月30日至2025年10月30日止。")]
        [DataRow("合同有效期：自2025年9月30日至2027年9月29日止。")]
        [DataRow("法规有效期：自2025年9月30日至2027年9月29日止。")]
        [DataRow("评估结论使用有效期另有约定，自2025年9月30日至2027年9月29日止。")]
        [DataRow("例如评估结论使用有效期自2025年9月30日至2025年10月30日止。")]
        public void OtherValidityOrUncertainMeaning_Abstains(string text)
        {
            using (var file = SyntheticDocxFile.Create(P("评估基准日：2025年9月30日"), P(text)))
            {
                Assert.IsFalse(Check(file.FilePath).IsEvaluable);
                Assert.AreEqual(0, Check(file.FilePath).Findings.Count);
            }
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("评估基准日：2025年2月30日")]
        [DataRow("评估基准日：2024年9月30日")]
        [DataRow("评估基准日：2025年9月30日；评估基准日：2025年10月1日")]
        public void MissingInvalidConflictingOrUnboundBenchmark_Abstains(string benchmark)
        {
            using (var file = SyntheticDocxFile.Create(P(benchmark),
                P("评估结论使用有效期自2025年9月30日至2025年10月30日止。")))
            {
                Assert.IsFalse(Check(file.FilePath).IsEvaluable);
            }
        }

        [TestMethod]
        public void MissingEnd_CompetingRanges_AndUnboundChapterContext_Abstain()
        {
            foreach (var body in new[]
            {
                new[] { P("评估结论使用有效期为一年，自2025年9月30日起。") },
                new[] { P("评估结论使用有效期自2025年9月30日至2025年10月30日止。"),
                    P("评估结论使用有效期自2025年9月30日至2026年9月29日止。") },
                new[] { P("评估结论使用有效期自2025年9月30日至2025年10月30日止。评估结论使用有效期自2025年9月30日至2026年9月29日止。") },
                new[] { P("评估结论使用有效期"), P("合同有效期自2025年9月30日至2025年10月30日止。") }
            })
            using (var file = SyntheticDocxFile.Create(new[] { P("评估基准日：2025年9月30日") }.Concat(body).ToArray()))
            {
                Assert.IsFalse(Check(file.FilePath).IsEvaluable);
            }
        }

        [TestMethod]
        public void TemplateChapterAndSplitRuns_AreRecognizedWithoutBorrowingOtherValidity()
        {
            using (var file = SyntheticDocxFile.Create(P("评估基准日：2025年11月30日"),
                P("十二、评估结论使用有效期"),
                new Paragraph(new Run(new Text("使用有效期为一年，自评估基准日2025年")),
                    new Run(new Text("11月30日起至2026年11月29日止。根据有关法规执行。"))),
                P("合同有效期：自2025年11月30日至2027年11月29日止。")))
            {
                Assert.IsTrue(Check(file.FilePath).IsEvaluable);
                Assert.AreEqual(0, Check(file.FilePath).Findings.Count);
            }
        }

        [TestMethod]
        public void NextDayStart_IsNotAnExactDayCountError_AndHeadingDoesNotLeakAcrossCells()
        {
            using (var file = SyntheticDocxFile.Create(P("评估基准日：2025年9月30日"),
                P("评估结论使用有效期自2025年10月1日至2026年9月30日止。")))
            {
                Assert.IsTrue(Check(file.FilePath).IsEvaluable);
                Assert.AreEqual(0, Check(file.FilePath).Findings.Count);
            }
            using (var file = SyntheticDocxFile.Create(P("评估基准日：2025年9月30日"),
                new Table(new TableRow(new TableCell(P("评估结论使用有效期")),
                    new TableCell(P("使用有效期为一年，自2025年9月30日至2025年10月30日止。"))))))
            {
                Assert.IsFalse(Check(file.FilePath).IsEvaluable);
                Assert.AreEqual(0, Check(file.FilePath).Findings.Count);
            }
        }

        [TestMethod]
        public async Task SingleAndDualProductionChain_PreservesLabelsDateSpansAndHashes()
        {
            using (var a = SyntheticDocxFile.Create(P("评估基准日：2025年9月30日"),
                P("评估结论使用有效期自2025年9月30日至2025年10月30日止。")))
            using (var b = SyntheticDocxFile.Create(P("评估基准日：2025年9月30日"),
                P("评估结论使用有效期自2025年9月30日至2027年9月29日止。")))
            {
                string beforeA = Hash(a.FilePath), beforeB = Hash(b.FilePath);
                var service = new ReportCheckService();
                foreach (var request in new[] { new ReportCheckRequest(a.FilePath, null),
                    new ReportCheckRequest(a.FilePath, b.FilePath, null), new ReportCheckRequest(b.FilePath, a.FilePath, null) })
                {
                    var result = await service.CheckAsync(request);
                    Assert.IsTrue(result.IsSuccess);
                    var findings = result.Issues.Where(item => item.IssueType == IssueType.EvaluationConclusionValidityAnomaly).ToArray();
                    Assert.AreEqual(request.HasDocumentB ? 2 : 1, findings.Length);
                    foreach (var finding in findings)
                    {
                        Assert.AreEqual(IssueSeverity.Normal, finding.Severity);
                        Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(finding)));
                        Assert.AreEqual(3, finding.Occurrences.Count);
                        foreach (var occurrence in finding.Occurrences)
                        {
                            var source = SourceNavigationService.FindSource(result.SourceDocuments, occurrence);
                            Assert.IsNotNull(source);
                            var target = new SourceLocationResolver().Resolve(source, occurrence);
                            Assert.IsNotNull(target);
                            Assert.AreEqual(occurrence.RawText, target.SpanText);
                            string expectedPath = finding.GovernanceKind.EndsWith("ClearlyShortPeriod", StringComparison.Ordinal)
                                ? a.FilePath : b.FilePath;
                            Assert.AreEqual(expectedPath, source.FullPath);
                        }
                    }
                }
                Assert.AreEqual(beforeA, Hash(a.FilePath));
                Assert.AreEqual(beforeB, Hash(b.FilePath));
            }
        }

        // Explicit local read-only validation; no private paths/content are stored in Git.
        public static void AuditApprovedReports(string pathA, string pathB)
        {
            string hashA = Hash(pathA), hashB = Hash(pathB);
            foreach (var pair in new[] { Tuple.Create("A", pathA), Tuple.Create("B", pathB) })
            {
                var check = Check(pair.Item2);
                Console.WriteLine("Approved {0}: ReliableDates={1}; NewValidityIssues={2}", pair.Item1,
                    check.IsEvaluable, check.Findings.Count);
                Assert.AreEqual(0, check.Findings.Count, "Approved document must not gain a validity false positive.");
            }
            var result = new ReportCheckService().CheckAsync(new ReportCheckRequest(pathA, pathB, null)).GetAwaiter().GetResult();
            Assert.IsTrue(result.IsSuccess);
            Console.WriteLine("Approved pair: NewValidityIssues={0}; Focused={1}; Detail={2}",
                result.Issues.Count(item => item.IssueType == IssueType.EvaluationConclusionValidityAnomaly),
                result.Issues.Count(item => !IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(item))), result.Issues.Count);
            Assert.AreEqual(hashA, Hash(pathA));
            Assert.AreEqual(hashB, Hash(pathB));
            Console.WriteLine("Approved SHA256: A unchanged=True; B unchanged=True");
        }

        private static EvaluationConclusionValidityResult Check(string path)
        {
            var document = new DocxDocumentReader().Read(path);
            Assert.IsTrue(document.IsSuccess, "Synthetic/authorized input must be readable.");
            return new EvaluationConclusionValidityChecker().Check(document.Elements);
        }

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
