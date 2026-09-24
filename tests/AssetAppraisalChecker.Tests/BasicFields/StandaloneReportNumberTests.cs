using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class StandaloneReportNumberTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_StandaloneReportNumber_ReturnsOneOccurrence()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                "测试资评报字〔2026〕第0001号");

            Assert.AreEqual(BasicFieldType.ReportNumber, occurrence.FieldType);
            Assert.AreEqual("测试资评报字〔2026〕第0001号", occurrence.RawValue);
            Assert.AreEqual("测试资评报字〔2026〕第0001号",
                occurrence.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Check_SameStandaloneReportNumberThreeTimes_ReturnsNoConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "测试资评报字〔2026〕第0001号",
                    "测试资评报字〔2026〕第0001号",
                    "测试资评报字〔2026〕第0001号"));

            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(1, occurrences
                .Select(item => item.NormalizedValue.CanonicalValue)
                .Distinct()
                .Count());
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_DifferentStandaloneReportNumber_ReturnsConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "测试资评报字〔2026〕第0001号",
                    "测试资评报字〔2026〕第0001号",
                    "测试资评报字〔2026〕第0002号"));

            BasicFieldIssue issue = new BasicFieldConsistencyEngine()
                .Check(occurrences)
                .Issues
                .Single();

            Assert.AreEqual(IssueType.BasicFieldConflict, issue.IssueType);
            Assert.AreEqual(BasicFieldType.ReportNumber, issue.FieldType);
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
        }

        [TestMethod]
        [DataRow("测试资评报字〔2026〕第0001号")]
        [DataRow("示例评报字（2026）第12号")]
        [DataRow("XYZ资评报字[2026]第123号")]
        [DataRow("XYZ资评报字(2026)第123号")]
        [DataRow("测试评报字【2026】第8号")]
        public void Extract_SupportedPairedYearBrackets_AreRecognized(string text)
        {
            Assert.AreEqual(BasicFieldType.ReportNumber, ExtractSingle(text).FieldType);
        }

        [TestMethod]
        public void Extract_LeadingAndTrailingWhitespace_PreservesExactLocation()
        {
            const string text = "  测试资评报字〔2026〕第0001号  ";
            const string expected = "测试资评报字〔2026〕第0001号";

            BasicFieldOccurrence occurrence = ExtractSingle(text);

            Assert.AreEqual(2, occurrence.TextStartIndex);
            Assert.AreEqual(expected.Length, occurrence.TextLength);
            Assert.AreEqual(expected, occurrence.RawText);
            Assert.AreEqual(
                expected,
                text.Substring(occurrence.TextStartIndex, occurrence.TextLength));
        }

        [TestMethod]
        public void Extract_ReasonableInternalWhitespace_NormalizesStably()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                "测试资评报字 〔 2026 〕 第 0001 号");

            Assert.AreEqual(
                "测试资评报字〔2026〕第0001号",
                occurrence.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Extract_ExplicitReportNumberLabel_DoesNotDuplicateOccurrence()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "报告编号：测试资评报字〔2026〕第0001号"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ReportNumber, occurrences[0].FieldType);
        }

        [TestMethod]
        public void Extract_LongParagraphReferencingAnotherReportNumber_IsNotStandalone()
        {
            AssertNoReportNumber(
                "本项目参考测试资评报字〔2026〕第0001号报告进行分析。");
        }

        [TestMethod]
        [DataRow("2026年8月12日")]
        [DataRow("第123号")]
        [DataRow("2026-001")]
        [DataRow("合同编号：TEST-2026-001")]
        [DataRow("设备编号：ABC2026001")]
        [DataRow("联系电话：13800000000")]
        [DataRow("证件号码：530000202608120001")]
        [DataRow("评估价值为100万元")]
        [DataRow("测试资评报字2026第0001号")]
        [DataRow("测试资评报字〔2026）第0001号")]
        public void Extract_NonReportNumberText_IsRejected(string text)
        {
            AssertNoReportNumber(text);
        }

        [TestMethod]
        public void Extract_StandaloneRule_DoesNotChangeOtherCoreBasicFields()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "测试资评报字〔2026〕第0001号",
                    "评估基准日：2026年6月30日"));

            Assert.AreEqual(2, occurrences.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    BasicFieldType.ReportNumber,
                    BasicFieldType.ValuationDate
                },
                occurrences.Select(item => item.FieldType).ToArray());
        }

        private BasicFieldOccurrence ExtractSingle(string text)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(text));
            Assert.AreEqual(1, occurrences.Count, text);
            return occurrences[0];
        }

        private void AssertNoReportNumber(string text)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(text));
            Assert.IsFalse(occurrences.Any(
                item => item.FieldType == BasicFieldType.ReportNumber));
        }
    }
}
