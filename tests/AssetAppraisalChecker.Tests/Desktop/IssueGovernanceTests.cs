using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Mechanical;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class IssueGovernanceTests
    {
        [TestMethod]
        public void BusinessConflict_IsConfirmedBusinessIssue()
        {
            ReportCheckIssue issue = CreateIssue(
                "BUSINESS-1",
                IssueType.MoneyConflict,
                IssueCategory.Business,
                IssueAuthority.Confirmed,
                "MoneyConflict",
                "文档 A",
                0);

            Assert.AreEqual(IssueCategory.Business, issue.Category);
            Assert.AreEqual(IssueAuthority.Confirmed, issue.Authority);
        }

        [TestMethod]
        public void BasicFieldParseFailure_DefaultsToReviewSuggested()
        {
            ReportCheckIssue issue = new ReportCheckIssue(
                "PARSE-1",
                IssueType.BasicFieldParseFailed,
                IssueSeverity.Reminder,
                "字段",
                "需复核",
                "无法形成确定结论。",
                "请人工复核。",
                new[] { Occurrence("文档 A", 0) },
                0);

            Assert.AreEqual(IssueCategory.Business, issue.Category);
            Assert.AreEqual(IssueAuthority.ReviewSuggested, issue.Authority);
        }

        [TestMethod]
        public void UnitConflict_IsConfirmedMechanicalIssue()
        {
            ReportCheckIssue issue = MapMechanical(MechanicalIssueKind.UnitConsistency);

            Assert.AreEqual(IssueCategory.Mechanical, issue.Category);
            Assert.AreEqual(IssueAuthority.Confirmed, issue.Authority);
        }

        [TestMethod]
        [DataRow(MechanicalIssueKind.FontStyleOutlier)]
        [DataRow(MechanicalIssueKind.ParagraphFormatOutlier)]
        public void FormattingOutlier_IsHighConfidenceFormattingIssue(MechanicalIssueKind kind)
        {
            ReportCheckIssue issue = MapMechanical(kind);

            Assert.AreEqual(IssueCategory.Formatting, issue.Category);
            Assert.AreEqual(IssueAuthority.HighConfidence, issue.Authority);
        }

        [TestMethod]
        public void DuplicateContent_IsHighConfidenceMechanicalIssue()
        {
            ReportCheckIssue issue = MapMechanical(MechanicalIssueKind.DuplicateWord);

            Assert.AreEqual(IssueCategory.Mechanical, issue.Category);
            Assert.AreEqual(IssueAuthority.HighConfidence, issue.Authority);
        }

        [TestMethod]
        public void ExactDuplicateAtSameLocation_IsReportedOnce()
        {
            ReportCheckIssue first = CreateIssue(
                "ONE", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "ConsecutiveSpaces", "文档 A", 2);
            ReportCheckIssue second = CreateIssue(
                "TWO", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "ConsecutiveSpaces", "文档 A", 2);

            IReadOnlyList<ReportCheckIssue> result =
                new IssueGovernanceProcessor().Deduplicate(new[] { first, second });

            Assert.AreEqual(first.StableId, second.StableId);
            Assert.AreEqual(1, result.Count);
            Assert.AreSame(first, result[0]);
        }

        [TestMethod]
        public void DuplicateAndPunctuationAtSameLocation_AreBothPreserved()
        {
            ReportCheckIssue duplicate = CreateIssue(
                "DUP", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.HighConfidence,
                "DuplicateWord", "文档 A", 3);
            ReportCheckIssue punctuation = CreateIssue(
                "PUN", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "RepeatedPunctuation", "文档 A", 3);

            IReadOnlyList<ReportCheckIssue> result =
                new IssueGovernanceProcessor().Deduplicate(new[] { duplicate, punctuation });

            Assert.AreEqual(2, result.Count);
            Assert.AreNotEqual(duplicate.StableId, punctuation.StableId);
        }

        [TestMethod]
        public void ReviewSuggested_DoesNotChangeBusinessPassOrFail()
        {
            var pass = new V2AnalysisResult(new[]
            {
                Result(ConsistencyStatus.PASS)
            });
            var fail = new V2AnalysisResult(new[]
            {
                Result(ConsistencyStatus.FAIL)
            });
            ReportCheckIssue review = CreateIssue(
                "LANGUAGE-REVIEW", IssueType.BasicFieldParseFailed,
                IssueCategory.Language, IssueAuthority.ReviewSuggested,
                "FutureLanguageReview", "文档 A", 0);

            Assert.AreEqual(1, new IssueGovernanceProcessor().Deduplicate(new[] { review }).Count);
            Assert.AreEqual(ConsistencyStatus.PASS, pass.StatusSummary);
            Assert.AreEqual(ConsistencyStatus.FAIL, fail.StatusSummary);
        }

        [TestMethod]
        public void MechanicalGovernance_DoesNotModifyBusinessFact()
        {
            var fact = new BenchmarkDateFact(
                "DocumentA",
                Location(0),
                "评估基准日为2026年6月15日",
                new FactConfidence(FactConfidenceBand.High, 1m),
                "2026-06-15");
            string valueBefore = fact.Value;

            new IssueGovernanceProcessor().Deduplicate(new[]
            {
                MapMechanical(MechanicalIssueKind.UnitConsistency)
            });

            Assert.AreEqual(valueBefore, fact.Value);
            Assert.AreEqual(new DateTime(2026, 6, 15), fact.NormalizedDate);
        }

        [TestMethod]
        public void FormattingGovernance_DoesNotAffectCrossDocumentResult()
        {
            var analysis = new V2AnalysisResult(new[]
            {
                Result(ConsistencyStatus.FAIL)
            });

            new IssueGovernanceProcessor().Deduplicate(new[]
            {
                MapMechanical(MechanicalIssueKind.FontStyleOutlier)
            });

            Assert.AreEqual(ConsistencyStatus.FAIL, analysis.StatusSummary);
            Assert.AreEqual(ConsistencyStatus.FAIL, analysis.Results.Single().Status);
        }

        [TestMethod]
        public void SameIssueInDocumentAAndB_HasDifferentStableIdentity()
        {
            ReportCheckIssue documentA = CreateIssue(
                "A", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "RepeatedPunctuation", "文档 A", 1);
            ReportCheckIssue documentB = CreateIssue(
                "B", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "RepeatedPunctuation", "文档 B", 1);

            Assert.AreNotEqual(documentA.StableId, documentB.StableId);
            Assert.AreEqual(2, new IssueGovernanceProcessor()
                .Deduplicate(new[] { documentA, documentB }).Count);
        }

        [TestMethod]
        public void StableIdentity_SameInput_IsDeterministic()
        {
            ReportCheckIssue first = CreateIssue(
                "RANDOM-SOURCE-ID-1", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "Tab", "文档 A", 4);
            ReportCheckIssue second = CreateIssue(
                "RANDOM-SOURCE-ID-2", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "Tab", "文档 A", 4);

            Assert.AreEqual(first.StableId, second.StableId);
            StringAssert.StartsWith(first.StableId, "ISSUE-");
            Assert.AreEqual(70, first.StableId.Length);
        }

        [TestMethod]
        public void StableIdentity_DifferentLocation_IsDifferent()
        {
            ReportCheckIssue first = CreateIssue(
                "ONE", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "Tab", "文档 A", 4);
            ReportCheckIssue second = CreateIssue(
                "TWO", IssueType.BasicFieldParseFailed,
                IssueCategory.Mechanical, IssueAuthority.Confirmed,
                "Tab", "文档 A", 5);

            Assert.AreNotEqual(first.StableId, second.StableId);
        }

        [TestMethod]
        public void DesktopViewModel_UsesChineseGovernanceLabels()
        {
            var viewModel = new IssueViewModel(CreateIssue(
                "FORMAT", IssueType.BasicFieldParseFailed,
                IssueCategory.Formatting, IssueAuthority.HighConfidence,
                "FontStyleOutlier", "文档 A", 1));

            Assert.AreEqual("格式", viewModel.CategoryDisplay);
            Assert.AreEqual("高置信异常", viewModel.AuthorityDisplay);
            Assert.IsFalse(viewModel.IsReviewSuggested);
            Assert.IsFalse(viewModel.CategoryDisplay.Contains("Formatting"));
        }

        private static ReportCheckIssue MapMechanical(MechanicalIssueKind kind)
        {
            var mechanical = new MechanicalIssue(
                "SyntheticChecker",
                kind,
                MechanicalIssueSeverity.Normal,
                "合成字段",
                "合成问题",
                "用于治理映射测试。",
                "请人工核对。",
                new[]
                {
                    new MechanicalIssueOccurrence(
                        "文档 A",
                        "合成原文",
                        Location(1),
                        new[] { "合成证据" })
                });
            return new MechanicalIssueMapper().Map(new[] { mechanical }, 0).Single();
        }

        private static ReportCheckIssue CreateIssue(
            string issueId,
            IssueType issueType,
            IssueCategory category,
            IssueAuthority authority,
            string governanceKind,
            string documentLabel,
            int paragraphIndex)
        {
            return new ReportCheckIssue(
                issueId,
                issueType,
                IssueSeverity.Normal,
                "合成字段",
                "合成摘要",
                "合成原因",
                "请人工复核。",
                new[] { Occurrence(documentLabel, paragraphIndex) },
                0,
                BasicFieldType.Unknown,
                null,
                null,
                category,
                authority,
                governanceKind);
        }

        private static ReportCheckOccurrence Occurrence(
            string documentLabel,
            int paragraphIndex)
        {
            return new ReportCheckOccurrence(
                "合成原文",
                "合成标准值",
                Location(paragraphIndex),
                "正文第 " + (paragraphIndex + 1) + " 段",
                "正文",
                string.Empty,
                new[] { "Evidence=synthetic" },
                new string[0],
                string.Empty,
                false,
                null,
                documentLabel);
        }

        private static DocumentLocation Location(int paragraphIndex)
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                paragraphIndex,
                null,
                null,
                null,
                new string[0]);
        }

        private static ConsistencyResult Result(ConsistencyStatus status)
        {
            return new ConsistencyResult(
                FactType.BenchmarkDate,
                status,
                "合成一致性判断。",
                new[] { "DocumentA | synthetic", "DocumentB | synthetic" });
        }
    }
}
