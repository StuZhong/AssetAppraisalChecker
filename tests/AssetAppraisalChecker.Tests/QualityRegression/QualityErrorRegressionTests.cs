using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Tests.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.QualityRegression
{
    [TestClass]
    public sealed class QualityErrorRegressionTests
    {
        [TestMethod]
        [DataRow("QC-001")]
        [DataRow("QC-002")]
        [DataRow("QC-003")]
        [DataRow("QC-004-B")]
        [DataRow("QC-004-Q")]
        [DataRow("QC-004-T")]
        [DataRow("QC-005")]
        [DataRow("QC-006-T")]
        [DataRow("QC-006-C")]
        [DataRow("QC-007")]
        [DataRow("QC-008")]
        [DataRow("QC-009-C")]
        [DataRow("QC-009-I")]
        [DataRow("QC-009-D")]
        [DataRow("QC-009-H")]
        [DataRow("QC-009-S")]
        [DataRow("QC-010-F")]
        [DataRow("QC-010-S")]
        [DataRow("QC-011-L")]
        [DataRow("QC-011-I")]
        [DataRow("QC-012")]
        [DataRow("QC-013")]
        [DataRow("QC-014")]
        [DataRow("QC-015")]
        public async Task ProductionChain_PositiveAndLegalCounterexample_SingleDualAndSwapped(string caseId)
        {
            QualityRegressionCase sample = QualityErrorRegressionCorpus.Find(caseId);
            using (SyntheticDocxFile positive = sample.Create(true))
            using (SyntheticDocxFile legal = sample.Create(false))
            {
                string positiveHash = Hash(positive.FilePath), legalHash = Hash(legal.FilePath);
                var service = new ReportCheckService();
                // This is Desktop's production entry, not an isolated matching function.
                ReportCheckResult single = await service.CheckAsync(positive.FilePath);
                ReportCheckResult counterexample = await service.CheckAsync(legal.FilePath);
                ReportCheckResult dual = await service.CheckAsync(new ReportCheckRequest(positive.FilePath, legal.FilePath, null));
                ReportCheckResult swapped = await service.CheckAsync(new ReportCheckRequest(legal.FilePath, positive.FilePath, null));
                Assert.IsTrue(single.IsSuccess && counterexample.IsSuccess && dual.IsSuccess && swapped.IsSuccess,
                    caseId + ": technical execution must succeed, independently of detection.");
                Assert.IsTrue(dual.IsCrossDocumentCheck && swapped.IsCrossDocumentCheck);
                Assert.AreEqual(positiveHash, Hash(positive.FilePath), "Positive DOCX changed.");
                Assert.AreEqual(legalHash, Hash(legal.FilePath), "Legal DOCX changed.");

                string positiveStatus = ClassifyPositive(sample, single);
                string status = QualityIssues(counterexample).Length == 0 ? positiveStatus : "FalsePositive";
                ReportCheckIssue[] hits = Target(sample, single);
                Console.WriteLine("{0} | SourceType={1} | SourcePage=unverified | ExpectedChecker={2} | " +
                    "Expected={3} | Actual={4} | Status={5} | LegalQualityIssues={6} | Evidence={7} | SHA=unchanged",
                    caseId, sample.SourceType, sample.ExpectedChecker, sample.ExpectedResult,
                    string.Join(",", hits.Select(item => item.GovernanceKind)), status, QualityIssues(counterexample).Length,
                    string.Join("; ", hits.SelectMany(item => item.Occurrences).Select(item => item.DocumentLabel + ":" +
                            item.LocationText + ":" + string.Join(",", item.Evidence))));
                Console.WriteLine("PositiveStatus={0} | NegativeStatus={1} | LegalActual={2} | " +
                    "DualTargetCount={3} | SwappedTargetCount={4} | DualLegalIssues={5} | SwappedLegalIssues={6}",
                    positiveStatus, QualityIssues(counterexample).Length == 0 ? "Clean" : "FalsePositive",
                    string.Join(";", QualityIssues(counterexample).Select(item => item.GovernanceKind + ":" + item.Summary + ":" +
                        string.Join("/", item.Occurrences.Select(o => o.RawText + "@" + o.LocationText + ":" + string.Join(",", o.Evidence))))),
                    Target(sample, dual).Length, Target(sample, swapped).Length,
                    QualityIssues(dual).Count(item => item.Occurrences.Any(o => o.DocumentLabel == "文档 B")),
                    QualityIssues(swapped).Count(item => item.Occurrences.Any(o => o.DocumentLabel == "文档 A")));
                // Technical/safety checks also run for samples whose detection assertions fail.
                ValidateLanguageAuthority(single, counterexample, dual, swapped);
                CollectionAssert.AreEquivalent(BusinessSignature(dual), BusinessSignature(swapped), "A/B swap business conclusion changed.");

                // A missed issue/false positive fails, rather than silently changing the expected outcome.
                Assert.AreEqual("Detected", status, caseId + ": actual capability differs from frozen expectation.");
                ValidateHits(sample, hits, "文档 A");
                ValidateHits(sample, Target(sample, dual), "文档 A");
                ValidateHits(sample, Target(sample, swapped), "文档 B");
                Assert.AreEqual(0, QualityIssues(dual).Count(item => item.Occurrences.Any(o => o.DocumentLabel == "文档 B")), "Legal B false positive.");
                Assert.AreEqual(0, QualityIssues(swapped).Count(item => item.Occurrences.Any(o => o.DocumentLabel == "文档 A")), "Legal A false positive.");
                Assert.AreEqual(hits.Length, Target(sample, dual).Length);
                Assert.AreEqual(hits.Length, Target(sample, swapped).Length);
            }
        }

        [TestMethod]
        public async Task ProductionChain_NormalMixedAssessmentDocument_NoQualityFalsePositiveOrCrossDocumentConflict()
        {
            using (SyntheticDocxFile a = QualityErrorRegressionCorpus.CreateNormalDocument())
            using (SyntheticDocxFile b = QualityErrorRegressionCorpus.CreateNormalDocument())
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var service = new ReportCheckService();
                ReportCheckResult single = await service.CheckAsync(a.FilePath);
                ReportCheckResult dual = await service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(single.IsSuccess && dual.IsSuccess);
                Console.WriteLine("QC-NORMAL | singleQualityIssues={0} | dualQualityIssues={1} | Issues={2} | SHA=unchanged",
                    QualityIssues(single).Length, QualityIssues(dual).Length,
                    string.Join(";", dual.Issues.Select(item => item.GovernanceKind + ":" + item.Summary)));
                Assert.AreEqual(0, QualityIssues(single).Length, "Mixed normal single document false positive.");
                Assert.AreEqual(0, QualityIssues(dual).Length, "Cross-document duplicates must not be quality issues.");
                Assert.AreEqual(0, dual.SeriousCount, "Matching facts must not produce confirmed conflicts.");
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
                ValidateLanguageAuthority(single, dual);
            }
        }

        private static string ClassifyPositive(QualityRegressionCase sample, ReportCheckResult positive)
        {
            ReportCheckIssue[] hits = Target(sample, positive);
            if (hits.Length == 0) return "Missed";
            if (hits.Any(item => item.Occurrences.Count < sample.MinimumOccurrences ||
                !item.Occurrences.Any(o => HasExpectedLocation(sample, o)) ||
                item.Occurrences.Any(o => !o.Location.ParagraphIndex.HasValue && !o.Location.TableIndex.HasValue ||
                    !o.Evidence.Any(evidence => !string.IsNullOrWhiteSpace(evidence))) ||
                !item.Occurrences.Any(o => (o.RawText + string.Join(" ", o.Evidence)).Contains(sample.EvidenceText))))
                return "Inconclusive";
            return "Detected";
        }

        private static bool HasExpectedLocation(QualityRegressionCase sample, ReportCheckOccurrence occurrence) =>
            sample.GovernanceKind == "UnitConsistency"
                ? occurrence.Location.TableIndex == 0 && occurrence.Location.RowIndex == 1
                : occurrence.Location.ParagraphIndex == sample.ParagraphIndex;

        private static void ValidateHits(QualityRegressionCase sample, ReportCheckIssue[] hits, string label)
        {
            Assert.IsTrue(hits.Length > 0, sample.CaseId + ": no target issue.");
            foreach (ReportCheckIssue issue in hits)
            {
                Assert.AreEqual(sample.Category, issue.Category);
                Assert.AreEqual(sample.Authority, issue.Authority);
                Assert.AreNotEqual(IssueSeverity.Serious, issue.Severity);
                Assert.IsTrue(issue.Occurrences.Count >= sample.MinimumOccurrences);
                Assert.IsTrue(issue.Occurrences.Any(item => HasExpectedLocation(sample, item)));
                Assert.IsTrue(issue.Occurrences.All(item => item.DocumentLabel == label && item.Evidence.Count > 0 &&
                    !string.IsNullOrWhiteSpace(item.LocationText)));
                Assert.IsTrue(issue.Occurrences.Any(item => (item.RawText + string.Join(" ", item.Evidence)).Contains(sample.EvidenceText)),
                    sample.CaseId + ": missing expected error/property in evidence.");
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.ParagraphIndex.HasValue || item.Location.TableIndex.HasValue),
                    "Unresolved location is not a correctly detected case.");
                if (sample.GovernanceKind == "DuplicateParagraph")
                    Assert.AreEqual(2, issue.Occurrences.Select(item => item.Location.ParagraphIndex).Distinct().Count());
                var display = new IssueViewModel(issue);
                Assert.IsFalse(string.IsNullOrWhiteSpace(display.IssueTypeDisplay));
                Assert.IsFalse(string.IsNullOrWhiteSpace(display.CategoryDisplay));
                Assert.IsFalse(string.IsNullOrWhiteSpace(display.AuthorityDisplay));
                Assert.IsTrue(display.Occurrences.All(item => item.DocumentLabel == label && item.HasEvidence));
            }
        }

        private static void ValidateLanguageAuthority(params ReportCheckResult[] results)
        {
            foreach (ReportCheckIssue issue in results.SelectMany(item => item.Issues).Where(item => item.Category == IssueCategory.Language))
            {
                Assert.AreEqual(IssueAuthority.ReviewSuggested, issue.Authority);
                Assert.AreEqual(IssueSeverity.Reminder, issue.Severity);
            }
        }

        private static ReportCheckIssue[] Target(QualityRegressionCase sample, ReportCheckResult result) =>
            result.Issues.Where(item => item.GovernanceKind == sample.GovernanceKind).ToArray();
        private static ReportCheckIssue[] QualityIssues(ReportCheckResult result) => result.Issues.Where(item => item.Category != IssueCategory.Business).ToArray();
        private static string[] BusinessSignature(ReportCheckResult result) => result.Issues.Where(item => item.Category == IssueCategory.Business)
            .Select(item => item.GovernanceKind + "|" + item.FieldName + "|" + item.Severity + "|" + item.Authority).ToArray();
        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
