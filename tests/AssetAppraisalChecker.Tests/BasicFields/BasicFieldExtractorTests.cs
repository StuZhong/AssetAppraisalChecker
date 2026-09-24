using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class BasicFieldExtractorTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_FailedDocumentRead_ReturnsNoOccurrence()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                DocumentReadResult.Failed(DocumentReadStatus.InvalidDocx, "虚构读取失败。"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_ProjectNameLabel_ReturnsWholeConservativeTextValue()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                "项目名称：XX测试公司拟资产处置资产评估项目");

            Assert.AreEqual(BasicFieldType.ProjectName, occurrence.FieldType);
            Assert.AreEqual("XX测试公司拟资产处置资产评估项目", occurrence.RawValue);
            Assert.AreEqual(occurrence.RawValue, occurrence.NormalizedValue.CanonicalValue);
            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("明确标签")));
        }

        [TestMethod]
        public void Extract_ProjectNameContainingYear_DoesNotCreateDateOrCount()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "项目名称：2026年度XX测试设备资产评估项目"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ProjectName, occurrences[0].FieldType);
        }

        [TestMethod]
        public void Extract_ReportNumber_TreatsYearAndSequenceAsPartOfOneString()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "报告编号：云测试评报字〔2026〕第001号"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ReportNumber, occurrences[0].FieldType);
            Assert.AreEqual("云测试评报字〔2026〕第001号", occurrences[0].NormalizedValue.CanonicalValue);
            Assert.IsFalse(occurrences.Any(item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_ValuationDateLabel_ReturnsTypedDate()
        {
            BasicFieldOccurrence occurrence = ExtractSingle("评估基准日为2026年6月30日。");

            Assert.AreEqual(BasicFieldType.ValuationDate, occurrence.FieldType);
            Assert.AreEqual("2026-06-30", occurrence.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Extract_UnlabelledOrdinaryDate_DoesNotGuessValuationDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs("公司于2026年6月30日购入设备。"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_InvalidValuationDate_PreservesFailedOccurrence()
        {
            BasicFieldOccurrence occurrence = ExtractSingle("评估基准日：2026年2月30日");

            Assert.AreEqual(BasicFieldParseStatus.Failed, occurrence.ParseStatus);
            Assert.IsNull(occurrence.NormalizedValue);
            Assert.AreEqual("2026年2月30日", occurrence.RawValue);
        }

        [TestMethod]
        public void Extract_ProjectName_PreservesConservativeTextBoundary()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                "项目名称：测试资产评估项目，具体概况如下：");

            Assert.AreEqual(BasicFieldType.ProjectName, occurrence.FieldType);
            Assert.AreEqual("测试资产评估项目，具体概况如下：", occurrence.RawValue);
        }

        private BasicFieldOccurrence ExtractSingle(string text)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(text));
            Assert.AreEqual(1, occurrences.Count, text);
            return occurrences[0];
        }

    }
}
