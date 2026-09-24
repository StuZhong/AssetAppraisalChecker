using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class ExpectedProjectNameResolverTests
    {
        private readonly ExpectedProjectNameResolver _resolver =
            new ExpectedProjectNameResolver();

        [TestMethod]
        public void Resolve_AllAuthoritativeValuesSame_ReturnsExpectedAndPreservesProvenance()
        {
            ReportLevelProjectNameCandidate cover = Candidate(
                "虚构集团拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                0);
            ReportLevelProjectNameCandidate summary = Candidate(
                "虚构集团拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Summary,
                10);
            ReportLevelProjectNameCandidate body = Candidate(
                "虚构集团拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Body,
                20);

            ExpectedProjectNameResolution result = _resolver.Resolve(
                new[] { cover, summary, body });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Resolved, result.Status);
            Assert.IsTrue(result.HasExpectedProjectName);
            Assert.AreEqual("虚构集团拟处置设备资产公允价值", result.ExpectedProjectName);
            Assert.AreEqual(1, result.DistinctCanonicalCount);
            Assert.AreEqual(3, result.Candidates.Count);
            Assert.AreEqual(3, result.ValueGroups[0].Candidates.Count);
            Assert.AreEqual(3, result.ValueGroups[0].AuthorityRoles.Count);
            Assert.AreSame(cover, result.ValueGroups[0].Candidates[0]);
            Assert.AreSame(cover.SourceParagraphs[0], result.Candidates[0].SourceParagraphs[0]);
        }

        [TestMethod]
        public void Resolve_TwoDistinctValues_ReturnsAmbiguousAndPreservesBothGroups()
        {
            ReportLevelProjectNameCandidate cover = Candidate(
                "虚构集团拟接收设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                0);
            ReportLevelProjectNameCandidate summary = Candidate(
                "虚构集团拟接收移交设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Summary,
                10);

            ExpectedProjectNameResolution result = _resolver.Resolve(
                new[] { cover, summary });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Ambiguous, result.Status);
            Assert.IsFalse(result.HasExpectedProjectName);
            Assert.IsNull(result.ExpectedProjectName);
            Assert.AreEqual(2, result.DistinctCanonicalCount);
            Assert.AreSame(cover, result.ValueGroups[0].Candidates.Single());
            Assert.AreSame(summary, result.ValueGroups[1].Candidates.Single());
        }

        [TestMethod]
        public void Resolve_MajorityValueDoesNotWin()
        {
            ExpectedProjectNameResolution result = _resolver.Resolve(new[]
            {
                Candidate("虚构甲公司拟处置设备资产公允价值", ReportLevelProjectNameAuthorityRole.Cover, 0),
                Candidate("虚构甲公司拟处置设备资产公允价值", ReportLevelProjectNameAuthorityRole.Summary, 10),
                Candidate("虚构乙公司拟处置设备资产公允价值", ReportLevelProjectNameAuthorityRole.Body, 20)
            });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Ambiguous, result.Status);
            Assert.IsNull(result.ExpectedProjectName);
            Assert.AreEqual(2, result.DistinctCanonicalCount);
        }

        [TestMethod]
        public void Resolve_CoverDoesNotOverrideAnotherAuthorityRole()
        {
            ExpectedProjectNameResolution result = _resolver.Resolve(new[]
            {
                Candidate("虚构甲公司拟处置设备资产公允价值", ReportLevelProjectNameAuthorityRole.Cover, 0),
                Candidate("虚构乙公司拟处置设备资产公允价值", ReportLevelProjectNameAuthorityRole.Summary, 10)
            });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Ambiguous, result.Status);
            Assert.IsNull(result.ExpectedProjectName);
        }

        [TestMethod]
        public void Resolve_WordingDifferenceDoesNotCreateSemanticEquivalence()
        {
            ExpectedProjectNameResolution result = _resolver.Resolve(new[]
            {
                Candidate("虚构公司拟接收客户设备资产公允价值", ReportLevelProjectNameAuthorityRole.Cover, 0),
                Candidate("虚构公司拟接收客户移交设备资产公允价值", ReportLevelProjectNameAuthorityRole.Body, 10)
            });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Ambiguous, result.Status);
            Assert.AreEqual(2, result.DistinctCanonicalCount);
            Assert.IsNull(result.ExpectedProjectName);
        }

        [TestMethod]
        public void Resolve_WhitespaceOnlyDifferenceUsesExistingCanonicalText()
        {
            ExpectedProjectNameResolution result = _resolver.Resolve(new[]
            {
                Candidate("虚构公司拟处置  设备资产公允价值", ReportLevelProjectNameAuthorityRole.Cover, 0),
                Candidate("虚构公司拟处置 设备资产公允价值", ReportLevelProjectNameAuthorityRole.Summary, 10)
            });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Resolved, result.Status);
            Assert.AreEqual("虚构公司拟处置 设备资产公允价值", result.ExpectedProjectName);
            Assert.AreEqual(1, result.DistinctCanonicalCount);
        }

        [TestMethod]
        public void Resolve_NoCandidates_ReturnsNoExpectedValue()
        {
            ExpectedProjectNameResolution result = _resolver.Resolve(
                new ReportLevelProjectNameCandidate[0]);

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.NoCandidates, result.Status);
            Assert.IsFalse(result.HasExpectedProjectName);
            Assert.IsNull(result.ExpectedProjectName);
            Assert.AreEqual(0, result.DistinctCanonicalCount);
        }

        [TestMethod]
        public void Resolve_NormalizationFailure_FailsClosedAndPreservesFailedCandidate()
        {
            ReportLevelProjectNameCandidate failed = Candidate(
                "虚构公司拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                0,
                BasicFieldNormalizationResult.Failed("虚构规范化失败。"));
            ReportLevelProjectNameCandidate valid = Candidate(
                "虚构公司拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Summary,
                10);

            ExpectedProjectNameResolution result = _resolver.Resolve(new[] { failed, valid });

            Assert.AreEqual(
                ExpectedProjectNameResolutionStatus.NormalizationFailed,
                result.Status);
            Assert.IsNull(result.ExpectedProjectName);
            Assert.AreEqual(1, result.FailedCandidates.Count);
            Assert.AreSame(failed, result.FailedCandidates[0]);
            Assert.AreEqual(2, result.Candidates.Count);
        }

        [TestMethod]
        public void Resolve_SingleAuthoritativeCandidate_ReturnsItsCanonicalValue()
        {
            ReportLevelProjectNameCandidate candidate = Candidate(
                "虚构公司拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Body,
                0);

            ExpectedProjectNameResolution result = _resolver.Resolve(new[] { candidate });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Resolved, result.Status);
            Assert.AreEqual(
                "虚构公司拟处置设备资产公允价值",
                result.ExpectedProjectName);
            Assert.AreSame(candidate, result.Candidates[0]);
        }

        [TestMethod]
        public void Resolve_CandidateOrderDoesNotChangeAmbiguousDecision()
        {
            ReportLevelProjectNameCandidate first = Candidate(
                "虚构甲公司拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Body,
                0);
            ReportLevelProjectNameCandidate second = Candidate(
                "虚构乙公司拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                10);

            ExpectedProjectNameResolution forward = _resolver.Resolve(new[] { first, second });
            ExpectedProjectNameResolution reverse = _resolver.Resolve(new[] { second, first });

            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Ambiguous, forward.Status);
            Assert.AreEqual(ExpectedProjectNameResolutionStatus.Ambiguous, reverse.Status);
            Assert.IsNull(forward.ExpectedProjectName);
            Assert.IsNull(reverse.ExpectedProjectName);
        }

        private static ReportLevelProjectNameCandidate Candidate(
            string text,
            ReportLevelProjectNameAuthorityRole role,
            int order,
            BasicFieldNormalizationResult normalization = null)
        {
            var paragraphs = new[]
            {
                Element("虚构项目名称第一段", order, role),
                Element("虚构项目名称第二段", order + 1, role)
            };
            DocumentElement anchor = Element(
                role == ReportLevelProjectNameAuthorityRole.Summary
                    ? "资产评估报告摘要"
                    : role == ReportLevelProjectNameAuthorityRole.Body
                        ? "资产评估报告正文"
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
                normalization ?? BasicFieldNormalizer.Normalize(BasicFieldType.ProjectName, text));
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
                        : new[] { "虚构报告结构" }),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
