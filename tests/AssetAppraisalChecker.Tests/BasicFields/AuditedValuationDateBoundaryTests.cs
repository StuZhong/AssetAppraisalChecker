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
    public sealed class AuditedValuationDateBoundaryTests
    {
        private readonly BasicFieldExtractor extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_AuditedSingleCharacterBridge_ReturnsOneValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日是2026年1月2日");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedBridge_RawValueAndLocationPointOnlyToDate()
        {
            const string text = "完全虚构说明：评估基准日 是 2026年1月2日。";
            BasicFieldOccurrence occurrence = Extract(text).Single();
            int expectedStart = text.IndexOf("2026年1月2日", System.StringComparison.Ordinal);

            Assert.AreEqual("2026年1月2日", occurrence.RawValue);
            Assert.AreEqual("2026年1月2日", occurrence.RawText);
            Assert.AreEqual(expectedStart, occurrence.TextStartIndex);
            Assert.AreEqual("2026年1月2日".Length, occurrence.TextLength);
        }

        [TestMethod]
        public void Extract_UnapprovedSingleCharacterBridge_IsRejected()
        {
            AssertNoValuationDate("评估基准日于2026年1月2日");
        }

        [TestMethod]
        public void Extract_AnotherUnapprovedSingleCharacterBridge_IsRejected()
        {
            AssertNoValuationDate("评估基准日系2026年1月2日");
        }

        [TestMethod]
        public void Extract_UnknownMultiCharacterBridge_IsRejected()
        {
            AssertNoValuationDate("评估基准日应是2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedBridgeWithoutDate_IsRejected()
        {
            AssertNoValuationDate("本段只写明评估基准日是待补充。");
        }

        [TestMethod]
        public void Extract_AuditedBridgeWithInvalidDate_IsRejected()
        {
            AssertNoValuationDate("评估基准日是2026年2月30日");
        }

        [TestMethod]
        public void Extract_AuditedBridgeWithTwoDates_IsFailClosed()
        {
            AssertNoValuationDate("评估基准日是2026年1月2日，或2026年1月3日");
        }

        [TestMethod]
        public void Extract_AuditedBridgeAcrossSentence_IsRejected()
        {
            AssertNoValuationDate("评估基准日是。2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedBridgeAcrossSemicolon_IsRejected()
        {
            AssertNoValuationDate("评估基准日是；2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedBridgeWithExcessiveWhitespace_IsRejected()
        {
            AssertNoValuationDate("评估基准日是     2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedBridgeWithoutValuationDateLabel_IsRejected()
        {
            AssertNoValuationDate("本次日期是2026年1月2日");
        }

        [TestMethod]
        public void Extract_ExactAuditedRange_ReturnsOneValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_ExactAuditedRange_SelectsFirstDateOnly()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日");

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual("2026-01-02", occurrences[0].NormalizedValue.CanonicalValue);
            Assert.AreNotEqual("2026-01-05", occurrences[0].NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Extract_ExactAuditedRange_RawValueContainsOnlyFirstDate()
        {
            BasicFieldOccurrence occurrence = Extract(
                "评估基准日2026年1月2日起至2026年1月5日。").Single();

            Assert.AreEqual("2026年1月2日", occurrence.RawValue);
            Assert.AreEqual("2026年1月2日", occurrence.RawText);
        }

        [TestMethod]
        public void Extract_ExactAuditedRange_LocationPointsOnlyToFirstDate()
        {
            const string text = "完全虚构：评估基准日2026年1月2日起至2026年1月5日。";
            BasicFieldOccurrence occurrence = Extract(text).Single();
            int expectedStart = text.IndexOf("2026年1月2日", System.StringComparison.Ordinal);

            Assert.AreEqual(expectedStart, occurrence.TextStartIndex);
            Assert.AreEqual("2026年1月2日".Length, occurrence.TextLength);
            Assert.AreEqual(
                "2026年1月2日",
                text.Substring(occurrence.TextStartIndex, occurrence.TextLength));
        }

        [TestMethod]
        public void Extract_UnapprovedRangeToken_IsFailClosed()
        {
            AssertNoValuationDate("评估基准日2026年1月2日起到2026年1月5日");
        }

        [TestMethod]
        public void Extract_SecondRangeDateMissing_IsRejected()
        {
            AssertNoValuationDate("评估基准日2026年1月2日起至待补充");
        }

        [TestMethod]
        public void Extract_SecondRangeDateInvalid_IsRejected()
        {
            AssertNoValuationDate("评估基准日2026年1月2日起至2026年2月30日");
        }

        [TestMethod]
        public void Extract_RangeWithUnsafeTextAfterSecondDate_IsRejected()
        {
            AssertNoValuationDate("评估基准日2026年1月2日起至2026年1月5日继续有效");
        }

        [TestMethod]
        public void Extract_RangeWithSafeTerminalPunctuation_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日。后续为完全虚构说明。");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterWithFullWidthPeriod_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日止。");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterWithFullWidthComma_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日止，");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterAtElementEnd_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日止");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacter_SelectsAndLocatesOnlyFirstDate()
        {
            const string text =
                "完全虚构：评估基准日2026年1月2日起至2026年1月5日止。";
            BasicFieldOccurrence occurrence = Extract(text).Single();
            int expectedStart = text.IndexOf(
                "2026年1月2日",
                System.StringComparison.Ordinal);

            Assert.AreEqual("2026年1月2日", occurrence.RawValue);
            Assert.AreEqual("2026年1月2日", occurrence.RawText);
            Assert.AreEqual("2026-01-02", occurrence.NormalizedValue.CanonicalValue);
            Assert.AreEqual(expectedStart, occurrence.TextStartIndex);
            Assert.AreEqual("2026年1月2日".Length, occurrence.TextLength);
            Assert.AreEqual(1, Extract(text).Count(item =>
                item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterWithLimitedWhitespace_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日 2026年1月2日 起至 2026年1月5日    止    。");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_ExcessiveWhitespaceBeforeAuditedRangeClosingCharacter_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至2026年1月5日     止。");
        }

        [TestMethod]
        public void Extract_ExcessiveWhitespaceAfterAuditedRangeClosingCharacter_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至2026年1月5日止     。");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterFollowedByUnsafeCjkText_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至2026年1月5日止随后继续普通文字");
        }

        [TestMethod]
        public void Extract_UnapprovedRangeTokenQiDaoWithClosingCharacter_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起到2026年1月5日止。");
        }

        [TestMethod]
        public void Extract_UnapprovedRangeTokenZhiDaoWithClosingCharacter_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日至到2026年1月5日止。");
        }

        [TestMethod]
        public void Extract_UnapprovedRangeTokenJieZhiWithClosingCharacter_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日截至2026年1月5日止。");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterWithoutValuationDateLabel_IsRejected()
        {
            AssertNoValuationDate(
                "2026年1月2日起至2026年1月5日止。");
        }

        [TestMethod]
        public void Extract_RetiredReportDateRangeWithClosingCharacter_DoesNotBecomeValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "报告日：2026年1月2日起至2026年1月5日止。");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterWithInvalidSecondDate_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至2026年2月30日止。");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterWithMissingSecondDate_IsRejected()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至待补充止。");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacterFollowedByThirdDate_IsFailClosed()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至2026年1月5日止，另有日期2026年1月6日");
        }

        [TestMethod]
        public void Extract_AuditedRangeClosingCharacter_IsNotACommonSingleDateEndBoundary()
        {
            AssertNoValuationDate("评估基准日2026年1月2日止。");
            AssertNoValuationDate("评估基准日是2026年1月2日止。");
        }

        [TestMethod]
        public void Extract_OrdinaryContractRangeWithClosingCharacter_IsRejected()
        {
            AssertNoValuationDate(
                "合同期间：2026年1月2日起至2026年1月5日止。");
        }

        [TestMethod]
        public void Check_ABCTerminatedRangeEquivalentDates_ReturnThreeOccurrencesAndNoConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日",
                "评估基准日是2026年1月2日",
                "评估基准日2026年1月2日起至2026年1月5日止。");
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(1, occurrences.Select(item =>
                item.NormalizedValue.CanonicalValue).Distinct().Count());
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_ABCTerminatedRangeDifferentStart_ReturnsOneSeriousConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日",
                "评估基准日是2026年1月2日",
                "评估基准日2026年1月3日起至2026年1月5日止。");
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
        public void Extract_RangeWithLimitedHorizontalWhitespace_IsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日 2026年1月2日 起至 2026年1月5日");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_RangeWithExcessiveWhitespace_IsRejected()
        {
            AssertNoValuationDate("评估基准日2026年1月2日起至     2026年1月5日");
        }

        [TestMethod]
        public void Extract_OrdinaryTwoDatesWithoutAuditedRange_IsFailClosed()
        {
            AssertNoValuationDate("评估基准日2026年1月2日，相关事项日期2026年1月5日");
        }

        [TestMethod]
        public void Extract_AuditedRangeFollowedByThirdDate_IsFailClosed()
        {
            AssertNoValuationDate(
                "评估基准日2026年1月2日起至2026年1月5日，另有日期2026年1月6日");
        }

        [TestMethod]
        public void Extract_AuditedRangeWithoutValuationDateLabel_IsRejected()
        {
            AssertNoValuationDate("合同期间：2026年1月2日起至2026年1月5日");
        }

        [TestMethod]
        public void Extract_RetiredReportDateRange_DoesNotBecomeValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "报告日：2026年1月2日起至2026年1月5日");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_TraditionalInlineForm_IsNotDuplicated()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日");

            AssertSingleDate(occurrences, "2026年1月2日");
        }

        [TestMethod]
        public void Extract_RangeNeverCreatesSecondValuationDateOccurrence()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日2026年1月2日起至2026年1月5日");

            Assert.AreEqual(1, occurrences.Count(item =>
                item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Check_ABCEquivalentDates_ReturnThreeOccurrencesAndNoConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日",
                "评估基准日是2026年1月2日",
                "评估基准日2026年1月2日起至2026年1月5日");
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(1, occurrences.Select(item =>
                item.NormalizedValue.CanonicalValue).Distinct().Count());
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_ABCDifferentRangeStart_ReturnsOneSeriousConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2026年1月2日",
                "评估基准日是2026年1月2日",
                "评估基准日2026年1月3日起至2026年1月5日");
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
        public void Regression_ProjectNameRemainsUnchanged()
        {
            BasicFieldOccurrence occurrence = Extract(
                "项目名称：完全虚构资产评估项目").Single();

            Assert.AreEqual(BasicFieldType.ProjectName, occurrence.FieldType);
        }

        [TestMethod]
        public void Regression_StandaloneReportNumberRemainsUnchanged()
        {
            BasicFieldOccurrence occurrence = Extract(
                "云测试评报字〔2026〕第001号").Single();

            Assert.AreEqual(BasicFieldType.ReportNumber, occurrence.FieldType);
        }

        [TestMethod]
        public void Regression_MoneyComparisonChainRemainsUnchanged()
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

        private void AssertNoValuationDate(string text)
        {
            Assert.IsFalse(Extract(text).Any(item =>
                item.FieldType == BasicFieldType.ValuationDate));
        }

        private static void AssertSingleDate(
            IReadOnlyList<BasicFieldOccurrence> occurrences,
            string expectedRawValue)
        {
            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ValuationDate, occurrences[0].FieldType);
            Assert.AreEqual(expectedRawValue, occurrences[0].RawValue);
            Assert.AreEqual("2026-01-02", occurrences[0].NormalizedValue.CanonicalValue);
        }
    }
}
