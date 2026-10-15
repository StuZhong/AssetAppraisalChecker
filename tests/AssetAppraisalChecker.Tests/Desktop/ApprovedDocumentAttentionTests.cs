using System;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Tests.Document;
using AssetAppraisalChecker.TextQuality.Grammar;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class ApprovedDocumentAttentionTests
    {
        [TestMethod]
        [DataRow("ParagraphFormatOutlier")]
        [DataRow("FontStyleOutlier")]
        public void StatisticalOutlier_StaysDetailedWithoutChangingGovernance(string kind)
        {
            ReportCheckIssue record = Record(kind, IssueCategory.Formatting, IssueAuthority.HighConfidence, IssueSeverity.Normal);
            var view = new IssueViewModel(record);
            Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(view));
            Assert.AreEqual(IssueSeverity.Normal, view.Severity);
            Assert.AreEqual(IssueAuthority.HighConfidence, view.Authority);
            Assert.AreSame(record.Occurrences[0], view.Occurrences[0].SourceOccurrence);
            CollectionAssert.AreEqual(record.Occurrences[0].Evidence.ToArray(), view.Occurrences[0].SourceOccurrence.Evidence.ToArray());
        }

        [TestMethod]
        [DataRow("ConsecutiveSpaces")]
        [DataRow("ChineseTextSpacing")]
        [DataRow("LeadingSpace")]
        [DataRow("TrailingSpace")]
        [DataRow("Tab")]
        [DataRow("ConsecutiveBlankParagraphs")]
        [DataRow("MissingEndingPunctuation")]
        [DataRow("SequenceStyleMismatch")]
        [DataRow("Highlight")]
        [DataRow("Strikethrough")]
        public void LayoutAndWeakStyleRules_AreDetailedEvenWithConfirmedAuthority(string kind)
        {
            Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(
                Record(kind, IssueCategory.Mechanical, IssueAuthority.Confirmed, IssueSeverity.Normal))));
        }

        [TestMethod]
        [DataRow("DuplicateWord")]
        [DataRow("DuplicateParagraph")]
        [DataRow("RepeatedPunctuation")]
        [DataRow("UnpairedChineseBracket")]
        [DataRow("UnpairedBookTitle")]
        [DataRow("UnitConsistency")]
        public void DeterministicMechanicalRule_RemainsFocused(string kind)
        {
            Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(
                Record(kind, IssueCategory.Mechanical, IssueAuthority.HighConfidence, IssueSeverity.Normal, hardEvidence: true))));
        }

        [TestMethod]
        [DataRow("DuplicateWord")]
        [DataRow("UnpairedChineseBracket")]
        [DataRow("UnpairedBookTitle")]
        [DataRow("UnitConsistency")]
        public void ContextDependentRule_WithoutProvedEvidenceStaysDetailed(string kind)
        {
            // Prior fixture asserted identity alone. Admission now also requires source proof.
            Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(
                Record(kind, IssueCategory.Mechanical, IssueAuthority.HighConfidence, IssueSeverity.Normal))));
        }

        [TestMethod]
        public void ControlledLanguageRules_StayReviewSuggestedAndFocused_UnknownRulesDoNot()
        {
            foreach (string kind in ExpressionRuleCatalog.Default.Rules.Select(rule => "Grammar." + rule.Kind + "." + rule.RuleId)
                .Concat(new[] { "Spell.KnownTypo", "Spell.ConfusableProfessionalTerm" }))
            {
                var view = new IssueViewModel(Record(kind, IssueCategory.Language, IssueAuthority.ReviewSuggested, IssueSeverity.Reminder));
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(view), kind);
                Assert.AreEqual(IssueAuthority.ReviewSuggested, view.Authority);
                Assert.AreEqual(IssueSeverity.Reminder, view.Severity);
            }
            foreach (string kind in new[] { "Spell.ProfessionalTermNearMatch", "Grammar.DanglingSentenceEnding.UnknownRule", "FutureLanguageRule" })
                Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(
                    Record(kind, IssueCategory.Language, IssueAuthority.ReviewSuggested, IssueSeverity.Reminder))), kind);
        }

        [TestMethod]
        public void BusinessConflictNeedsConfirmedAuthority_ReviewResultIsNotSilentlyUpgraded()
        {
            ReportCheckIssue confirmed = Record("CrossDocument.AssessmentValue.Default", IssueCategory.Business,
                IssueAuthority.Confirmed, IssueSeverity.Serious, IssueType.MoneyConflict);
            ReportCheckIssue review = Record("CrossDocument.AssessmentValue.Default", IssueCategory.Business,
                IssueAuthority.ReviewSuggested, IssueSeverity.Reminder, IssueType.MoneyConflict);
            Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(confirmed)));
            Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(review)));
            Assert.AreEqual(IssueSeverity.Reminder, review.Severity);
            Assert.AreEqual(IssueAuthority.ReviewSuggested, review.Authority);
        }

        [TestMethod]
        [DataRow("项目名称：合成甲资产评估项目", "项目名称：合成乙资产评估项目", "项目名称")]
        [DataRow("报告编号：虚构评报字〔2026〕第001号", "报告编号：虚构评报字〔2026〕第002号", "报告编号")]
        [DataRow("评估基准日：2026年6月15日", "评估基准日：2026年5月31日", "评估基准日")]
        [DataRow("评估价值为100万元", "评估价值为200万元", "评估价值")]
        public async Task ProductionBusinessAndCrossDocumentConflicts_RemainFocused(string textA, string textB, string field)
        {
            using (SyntheticDocxFile a = SyntheticDocxFile.Create(P(textA)))
            using (SyntheticDocxFile b = SyntheticDocxFile.Create(P(textB)))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                ReportCheckIssue conflict = result.Issues.First(item => item.FieldName == field && item.Severity == IssueSeverity.Serious);
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(conflict)));
                Assert.AreEqual(IssueAuthority.Confirmed, conflict.Authority);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                    conflict.Occurrences.Select(item => item.DocumentLabel).Distinct().ToArray());
            }
        }

        [TestMethod]
        [DataRow("设备数量为 1 kg，单价为 30 元/台。", "UnitConsistency")]
        [DataRow("本项目采用评估评估方法。", "DuplicateWord")]
        [DataRow("本次评估涉及（合成设备。", "UnpairedChineseBracket")]
        [DataRow("本次评估参考《合成技术说明。", "UnpairedBookTitle")]
        [DataRow("本项目评估基准曰为合成日期。", "Spell.ConfusableProfessionalTerm")]
        [DataRow("本资产评估报吿仅供人工复核。", "Spell.KnownTypo")]
        public async Task ProductionHighValueDetection_RemainsFocused(string text, string kind)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(text)))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.IsTrue(result.IsSuccess);
                ReportCheckIssue issue = result.Issues.First(item => item.GovernanceKind == kind);
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(issue)));
                Assert.IsTrue(issue.Occurrences[0].Location.ParagraphIndex.HasValue);
            }
        }

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));

        private static ReportCheckIssue Record(string kind, IssueCategory category, IssueAuthority authority,
            IssueSeverity severity, IssueType type = IssueType.BasicFieldParseFailed, bool hardEvidence = false)
        {
            var occurrence = new ReportCheckOccurrence("合成原文", string.Empty,
                new DocumentLocation(DocumentPart.MainBody, 1, null, null, null, null),
                "正文段落 2", "正文", string.Empty, hardEvidence
                    ? new[] { "合成证据；字符偏移=1；长度=2", AssetAppraisalChecker.Mechanical.MechanicalEvidenceStrength.HardDeterministic }
                    : new[] { "合成证据；字符偏移=1；长度=2" },
                Array.Empty<string>(), string.Empty, false, documentLabel: "文档 B");
            return new ReportCheckIssue("TEST-" + kind, type, severity, "合成字段", "合成问题", "合成依据",
                "人工复核", new[] { occurrence }, 0, category: category, authority: authority, governanceKind: kind);
        }
    }
}
