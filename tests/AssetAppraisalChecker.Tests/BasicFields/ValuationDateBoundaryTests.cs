using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class ValuationDateBoundaryTests
    {
        private readonly BasicFieldExtractor extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_ExistingColonForm_RemainsSupportedWithoutDuplicate()
        {
            const string text = "评估基准日：2026年1月2日";
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(text);

            AssertSingleValuationDate(occurrences, "2026年1月2日");
            Assert.AreEqual(
                text.IndexOf("2026年1月2日", System.StringComparison.Ordinal),
                occurrences[0].TextStartIndex);
            Assert.AreEqual("2026年1月2日".Length, occurrences[0].TextLength);
            Assert.AreEqual("2026年1月2日", occurrences[0].RawText);
        }

        [TestMethod]
        public void Extract_ExistingWeiSeparator_RemainsSupportedWithoutDuplicate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日为2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_TightlyAdjacentDate_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_LimitedWhitespaceBeforeAdjacentDate_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日　 2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_QueDingWeiBridge_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日确定为2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_XuanQuBridge_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日选取2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_SheDingWeiBridge_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日设定为2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_ExistingLongerValuationDateLabel_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日期2026年1月2日");

            AssertSingleValuationDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Check_ThreeEquivalentBoundaryForms_ReturnOneCanonicalValueAndNoConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日",
                "评估基准日确定为2026年1月2日",
                "评估基准日2026年1月2日");

            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(1, occurrences.Select(item =>
                item.NormalizedValue.CanonicalValue).Distinct().Count());
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_ThirdDifferentDate_ReturnsOneSeriousConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日",
                "评估基准日确定为2026年1月2日",
                "评估基准日2026年1月3日");

            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(2, occurrences.Select(item =>
                item.NormalizedValue.CanonicalValue).Distinct().Count());
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.BasicFieldConflict, result.Issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Serious, result.Issues[0].Severity);
        }

        [TestMethod]
        public void Extract_LocationAndTextRangePointOnlyToDateBody()
        {
            const string text = "完全虚构说明：评估基准日确定为2026年1月2日，后续说明。";
            BasicFieldOccurrence occurrence = Extract(text).Single();
            int expectedStart = text.IndexOf("2026年1月2日", System.StringComparison.Ordinal);

            Assert.AreEqual(0, occurrence.Location.ParagraphIndex);
            Assert.AreEqual(expectedStart, occurrence.TextStartIndex);
            Assert.AreEqual("2026年1月2日".Length, occurrence.TextLength);
            Assert.AreEqual(
                "2026年1月2日",
                text.Substring(occurrence.TextStartIndex, occurrence.TextLength));
        }

        [TestMethod]
        public void Extract_RawValueAndRawTextContainOnlyDateBody()
        {
            BasicFieldOccurrence occurrence = Extract(
                "评估基准日设定为2026年1月2日。").Single();

            Assert.AreEqual("2026年1月2日", occurrence.RawValue);
            Assert.AreEqual("2026年1月2日", occurrence.RawText);
        }

        [TestMethod]
        public void Extract_RetiredReportDateLabel_DoesNotBecomeValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("报告日为2026年1月2日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_UnseparatedReportDateNeverBecomesValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("报告日2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_ContractDateNeverBecomesValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("合同签订日为2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_EstablishmentDateNeverBecomesValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("成立日期为2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_OrdinaryNarrativeDateNeverBecomesValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("完全虚构企业于2026年1月2日购入设备。");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_DateAcrossSentenceBoundary_IsRejected()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日另见说明。报告日为2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_DateAcrossSemicolonBoundary_IsRejected()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日另见说明；报告日为2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_DateAcrossLineBreak_IsRejected()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：\r\n2026年1月2日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_TooMuchPadding_IsRejected()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日     2026年1月2日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_ArbitraryLongBridge_IsRejected()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日根据本次完全虚构业务安排确定为2026年1月2日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_MultipleDatesInSameClause_IsFailClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日确定为2026年1月2日，或2026年1月3日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_NoDateValue_IsRejected()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("本段仅讨论评估基准日的概念。");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_InvalidSupplementalDate_IsRejectedWithoutFalseValue()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract("评估基准日确定为2026年2月30日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_DiscussionThenOtherDate_IsNotBoundToValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日相关事项详见附件，报告日为2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_ContractNarrativeDate_IsNotBoundToValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日与合同签订日存在差异，合同签订日为2026年1月2日");

            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Regression_ProjectNameExtraction_RemainsUnchanged()
        {
            BasicFieldOccurrence occurrence = Extract("项目名称：完全虚构资产评估项目").Single();

            Assert.AreEqual(BasicFieldType.ProjectName, occurrence.FieldType);
            Assert.AreEqual("完全虚构资产评估项目", occurrence.RawValue);
        }

        [TestMethod]
        public void Regression_StandaloneReportNumber_RemainsUnchanged()
        {
            BasicFieldOccurrence occurrence = Extract("云测试评报字〔2026〕第001号").Single();

            Assert.AreEqual(BasicFieldType.ReportNumber, occurrence.FieldType);
            Assert.AreEqual("云测试评报字〔2026〕第001号", occurrence.RawValue);
        }

        [TestMethod]
        public void Regression_MoneyComparisonChain_RemainsUnchanged()
        {
            DocumentReadResult document = BasicFieldTestFactory.Paragraphs(
                "本项目不含税评估净值为100万元。",
                "本项目不含税评估净值为100万元。");
            IReadOnlyList<MoneyOccurrence> moneyOccurrences =
                new MoneyOccurrenceExtractor().Extract(document);

            ConsistencyCheckResult result = new ConsistencyEngine().Check(moneyOccurrences);

            Assert.AreEqual(2, moneyOccurrences.Count);
            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.SkippedOccurrenceCount);
            Assert.AreEqual(0, result.Issues.Count);
        }

        private IReadOnlyList<BasicFieldOccurrence> Extract(params string[] paragraphs)
        {
            return extractor.Extract(BasicFieldTestFactory.Paragraphs(paragraphs));
        }

        private static void AssertSingleValuationDate(
            IReadOnlyList<BasicFieldOccurrence> occurrences,
            string expectedRawValue)
        {
            Assert.AreEqual(1, occurrences.Count);
            BasicFieldOccurrence occurrence = occurrences[0];
            Assert.AreEqual(BasicFieldType.ValuationDate, occurrence.FieldType);
            Assert.AreEqual(expectedRawValue, occurrence.RawValue);
            Assert.AreEqual("2026-01-02", occurrence.NormalizedValue.CanonicalValue);
        }
    }
}
