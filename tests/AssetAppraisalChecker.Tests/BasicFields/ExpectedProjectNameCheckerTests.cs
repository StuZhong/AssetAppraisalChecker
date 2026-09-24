using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class ExpectedProjectNameCheckerTests
    {
        private const string ProjectA = "虚构甲公司拟处置设备资产公允价值";
        private const string ProjectB = "虚构乙公司拟处置设备资产公允价值";
        private const string ProjectWrong = "虚构错误公司拟处置设备资产公允价值";
        private const string ProjectCorrect = "虚构正确公司拟处置设备资产公允价值";

        private readonly ExpectedProjectNameChecker checker =
            new ExpectedProjectNameChecker();

        [TestMethod]
        public void Check_ThreeEqualCandidatesAndEqualExpected_AllMatch()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectA,
                ThreeCandidates(ProjectA, ProjectA, ProjectA));

            Assert.AreEqual(ProjectNameExpectedCheckStatus.ValidExpectedValue, result.Status);
            Assert.AreEqual(3, result.MatchCount);
            Assert.AreEqual(0, result.MismatchCount);
            Assert.IsTrue(result.AllMatched);
            Assert.AreEqual(ProjectA, result.ExpectedCanonicalText);
        }

        [TestMethod]
        public void Check_ThreeEqualWrongCandidatesAgainstCorrectExpected_AllMismatch()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectCorrect,
                ThreeCandidates(ProjectWrong, ProjectWrong, ProjectWrong));

            Assert.AreEqual(0, result.MatchCount);
            Assert.AreEqual(3, result.MismatchCount);
            Assert.IsFalse(result.AllMatched);
        }

        [TestMethod]
        public void Check_OnlyCoverWrong_PreservesCoverMismatch()
        {
            AssertSingleMismatch(
                ThreeCandidates(ProjectB, ProjectA, ProjectA),
                ReportLevelProjectNameAuthorityRole.Cover);
        }

        [TestMethod]
        public void Check_OnlySummaryWrong_PreservesSummaryMismatch()
        {
            AssertSingleMismatch(
                ThreeCandidates(ProjectA, ProjectB, ProjectA),
                ReportLevelProjectNameAuthorityRole.Summary);
        }

        [TestMethod]
        public void Check_OnlyBodyWrong_PreservesBodyMismatch()
        {
            AssertSingleMismatch(
                ThreeCandidates(ProjectA, ProjectA, ProjectB),
                ReportLevelProjectNameAuthorityRole.Body);
        }

        [TestMethod]
        public void Check_ABBWithExpectedA_ReturnsTwoMismatchesWithoutVoting()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectA,
                ThreeCandidates(ProjectA, ProjectB, ProjectB));

            Assert.AreEqual(1, result.MatchCount);
            Assert.AreEqual(2, result.MismatchCount);
            Assert.IsTrue(result.Comparisons.Single(item =>
                item.AuthorityRole == ReportLevelProjectNameAuthorityRole.Cover).IsMatch);
        }

        [TestMethod]
        public void Check_ABBWithExpectedB_ReturnsOneMismatchWithoutVoting()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectB,
                ThreeCandidates(ProjectA, ProjectB, ProjectB));

            Assert.AreEqual(2, result.MatchCount);
            Assert.AreEqual(1, result.MismatchCount);
            Assert.IsTrue(result.Comparisons.Single(item =>
                item.AuthorityRole == ReportLevelProjectNameAuthorityRole.Cover).IsMismatch);
        }

        [TestMethod]
        public void Check_CandidateInputOrderChanged_ReturnsSameOrderedOutcomes()
        {
            IReadOnlyList<ReportLevelProjectNameCandidate> candidates =
                ThreeCandidates(ProjectA, ProjectB, ProjectB);
            ProjectNameExpectedCheckResult forward = checker.Check(ProjectB, candidates);
            ProjectNameExpectedCheckResult reverse = checker.Check(
                ProjectB,
                candidates.Reverse());

            CollectionAssert.AreEqual(
                forward.Comparisons.Select(Describe).ToArray(),
                reverse.Comparisons.Select(Describe).ToArray());
        }

        [TestMethod]
        public void Check_CanonicalComparisonUsesOrdinalExactEquality()
        {
            const string expected = "ABC虚构公司拟处置设备资产公允价值";
            ReportLevelProjectNameCandidate candidate = Candidate(
                "abc虚构公司拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                0);

            ProjectNameExpectedCheckResult result = checker.Check(
                expected,
                new[] { candidate });

            Assert.AreEqual(0, result.MatchCount);
            Assert.AreEqual(1, result.MismatchCount);
        }

        [TestMethod]
        public void Check_NullExpected_ReturnsMissingExpectedValue()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                null,
                ThreeCandidates(ProjectA, ProjectA, ProjectA));

            Assert.AreEqual(ProjectNameExpectedCheckStatus.MissingExpectedValue, result.Status);
            Assert.IsFalse(result.HasValidExpectedValue);
            Assert.AreEqual(0, result.Comparisons.Count);
        }

        [TestMethod]
        public void Check_WhitespaceExpected_ReturnsExpectedNormalizationFailed()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                "   ",
                ThreeCandidates(ProjectA, ProjectA, ProjectA));

            Assert.AreEqual(
                ProjectNameExpectedCheckStatus.ExpectedNormalizationFailed,
                result.Status);
            Assert.AreEqual(0, result.Comparisons.Count);
        }

        [TestMethod]
        public void Check_ValidExpectedWithoutCandidates_ReturnsNoAuthoritativeCandidates()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectA,
                new ReportLevelProjectNameCandidate[0]);

            Assert.AreEqual(
                ProjectNameExpectedCheckStatus.NoAuthoritativeCandidates,
                result.Status);
            Assert.AreEqual(ProjectA, result.ExpectedCanonicalText);
            Assert.AreEqual(0, result.Comparisons.Count);
        }

        [TestMethod]
        public void Check_CandidateNormalizationFailure_FailsClosed()
        {
            ReportLevelProjectNameCandidate failed = Candidate(
                ProjectA,
                ReportLevelProjectNameAuthorityRole.Cover,
                0,
                BasicFieldNormalizationResult.Failed("虚构候选规范化失败。"));
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectA,
                new[]
                {
                    failed,
                    Candidate(ProjectA, ReportLevelProjectNameAuthorityRole.Summary, 10)
                });

            Assert.AreEqual(
                ProjectNameExpectedCheckStatus.CandidateNormalizationFailed,
                result.Status);
            Assert.AreEqual(0, result.Comparisons.Count);
            Assert.AreSame(failed, result.FailedCandidates.Single());
        }

        [TestMethod]
        public void Check_ComparisonPreservesCompleteCandidateProvenance()
        {
            ReportLevelProjectNameCandidate candidate = Candidate(
                ProjectB,
                ReportLevelProjectNameAuthorityRole.Body,
                20);
            ProjectNameExpectedCandidateComparison comparison = checker
                .Check(ProjectA, new[] { candidate })
                .Comparisons.Single();

            Assert.AreSame(candidate, comparison.Candidate);
            Assert.AreSame(candidate.SourceParagraphs[0], comparison.SourceParagraphs[0]);
            Assert.AreSame(candidate.SourceParagraphs[1], comparison.SourceParagraphs[1]);
            Assert.AreSame(candidate.Anchor, comparison.Anchor);
            Assert.AreEqual(candidate.ExtractionPattern, comparison.ExtractionPattern);
        }

        [TestMethod]
        public void Check_ComparisonPreservesEachAuthorityRole()
        {
            ProjectNameExpectedCheckResult result = checker.Check(
                ProjectA,
                ThreeCandidates(ProjectA, ProjectB, ProjectA));

            CollectionAssert.AreEqual(
                new[]
                {
                    ReportLevelProjectNameAuthorityRole.Cover,
                    ReportLevelProjectNameAuthorityRole.Summary,
                    ReportLevelProjectNameAuthorityRole.Body
                },
                result.Comparisons.Select(item => item.AuthorityRole).ToArray());
        }

        [TestMethod]
        public void Coordinator_LegacyProjectNameOccurrenceDoesNotEnterExpectedComparison()
        {
            List<DocumentElement> elements = Paragraphs("项目名称：虚构旧路径项目");
            int legacyCount = new BasicFieldExtractor()
                .Extract(elements)
                .Count(item => item.FieldType == BasicFieldType.ProjectName);
            ProjectNameExpectedCheckResult result =
                new ReportLevelProjectNameCheckCoordinator().Check(ProjectA, elements);

            Assert.AreEqual(1, legacyCount);
            Assert.AreEqual(
                ProjectNameExpectedCheckStatus.NoAuthoritativeCandidates,
                result.Status);
            Assert.AreEqual(0, result.Candidates.Count);
            Assert.AreEqual(0, result.Comparisons.Count);
        }

        [TestMethod]
        public void Coordinator_CertifiedReportLevelBlockComparesWithManualExpected()
        {
            List<DocumentElement> elements = Paragraphs(
                "虚构甲公司拟处置",
                "设备资产公允价值",
                "资产评估报告");

            ProjectNameExpectedCheckResult result =
                new ReportLevelProjectNameCheckCoordinator().Check(ProjectA, elements);

            Assert.AreEqual(ProjectNameExpectedCheckStatus.ValidExpectedValue, result.Status);
            Assert.AreEqual(1, result.Candidates.Count);
            Assert.AreEqual(1, result.MatchCount);
            Assert.IsTrue(result.AllMatched);
        }

        [TestMethod]
        public void Regression_GenericBasicFieldConsistencyEngineRemainsOccurrenceBased()
        {
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(new[]
                {
                    BasicFieldTestFactory.Occurrence(
                        BasicFieldType.ProjectName,
                        ProjectA,
                        0),
                    BasicFieldTestFactory.Occurrence(
                        BasicFieldType.ProjectName,
                        ProjectB,
                        1)
                });

            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.BasicFieldConflict, result.Issues[0].IssueType);
        }

        [TestMethod]
        public void Regression_ReportNumberNormalizationRemainsUnchanged()
        {
            const string raw = "虚构评报字（2026）第001号";
            string before = BasicFieldNormalizer.Normalize(
                BasicFieldType.ReportNumber,
                raw).NormalizedValue.CanonicalValue;
            checker.Check(ProjectA, ThreeCandidates(ProjectA, ProjectA, ProjectA));
            string after = BasicFieldNormalizer.Normalize(
                BasicFieldType.ReportNumber,
                raw).NormalizedValue.CanonicalValue;

            Assert.AreEqual("虚构评报字〔2026〕第001号", before);
            Assert.AreEqual(before, after);
        }

        [TestMethod]
        public void Regression_ValuationDateNormalizationRemainsUnchanged()
        {
            const string raw = "2026年1月2日";
            string before = BasicFieldNormalizer.Normalize(
                BasicFieldType.ValuationDate,
                raw).NormalizedValue.CanonicalValue;
            checker.Check(ProjectA, ThreeCandidates(ProjectA, ProjectA, ProjectA));
            string after = BasicFieldNormalizer.Normalize(
                BasicFieldType.ValuationDate,
                raw).NormalizedValue.CanonicalValue;

            Assert.AreEqual("2026-01-02", before);
            Assert.AreEqual(before, after);
        }

        [TestMethod]
        public void Regression_MoneyParsingRemainsUnchanged()
        {
            MoneyParseResult before = ArabicMoneyParser.Parse("38.19万元");
            checker.Check(ProjectA, ThreeCandidates(ProjectA, ProjectA, ProjectA));
            MoneyParseResult after = ArabicMoneyParser.Parse("38.19万元");

            Assert.IsTrue(before.IsSuccess);
            Assert.IsTrue(after.IsSuccess);
            Assert.AreEqual(381900.00m, before.NormalizedAmountYuan.Value);
            Assert.AreEqual(before.NormalizedAmountYuan, after.NormalizedAmountYuan);
        }

        private void AssertSingleMismatch(
            IReadOnlyList<ReportLevelProjectNameCandidate> candidates,
            ReportLevelProjectNameAuthorityRole expectedRole)
        {
            ProjectNameExpectedCheckResult result = checker.Check(ProjectA, candidates);

            Assert.AreEqual(1, result.MismatchCount);
            Assert.AreEqual(
                expectedRole,
                result.Comparisons.Single(item => item.IsMismatch).AuthorityRole);
        }

        private static string Describe(ProjectNameExpectedCandidateComparison comparison)
        {
            return comparison.AuthorityRole + ":" + comparison.Outcome;
        }

        private static IReadOnlyList<ReportLevelProjectNameCandidate> ThreeCandidates(
            string cover,
            string summary,
            string body)
        {
            return new[]
            {
                Candidate(cover, ReportLevelProjectNameAuthorityRole.Cover, 0),
                Candidate(summary, ReportLevelProjectNameAuthorityRole.Summary, 10),
                Candidate(body, ReportLevelProjectNameAuthorityRole.Body, 20)
            };
        }

        private static ReportLevelProjectNameCandidate Candidate(
            string text,
            ReportLevelProjectNameAuthorityRole role,
            int order,
            BasicFieldNormalizationResult normalization = null)
        {
            List<DocumentElement> paragraphs = Paragraphs(
                "虚构完整项目名称第一段",
                "虚构完整项目名称第二段");
            paragraphs[0] = Element(paragraphs[0].Text, order, role);
            paragraphs[1] = Element(paragraphs[1].Text, order + 1, role);
            DocumentElement anchor = Element(
                role == ReportLevelProjectNameAuthorityRole.Summary
                    ? "资产评估报告摘要"
                    : "资产评估报告",
                order + 2,
                role,
                role == ReportLevelProjectNameAuthorityRole.Cover
                    ? DocumentElementType.Paragraph
                    : DocumentElementType.Heading);
            return new ReportLevelProjectNameCandidate(
                text,
                role,
                paragraphs,
                anchor,
                ReportLevelProjectNameExtractionPattern.Pattern1TwoParagraphDirect,
                normalization ?? BasicFieldNormalizer.Normalize(
                    BasicFieldType.ProjectName,
                    text));
        }

        private static List<DocumentElement> Paragraphs(params string[] texts)
        {
            return texts.Select((text, index) => Element(
                text,
                index,
                ReportLevelProjectNameAuthorityRole.Cover)).ToList();
        }

        private static DocumentElement Element(
            string text,
            int order,
            ReportLevelProjectNameAuthorityRole role,
            DocumentElementType elementType = DocumentElementType.Paragraph)
        {
            return new DocumentElement(
                elementType,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    order,
                    null,
                    null,
                    null,
                    role == ReportLevelProjectNameAuthorityRole.Cover
                        ? null
                        : new[] { "虚构权威结构" }),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
