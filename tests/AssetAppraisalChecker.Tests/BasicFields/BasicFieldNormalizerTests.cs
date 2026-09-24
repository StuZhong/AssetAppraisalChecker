using AssetAppraisalChecker.Core.BasicFields;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class BasicFieldNormalizerTests
    {
        [TestMethod]
        public void Normalize_EmptyTextValue_Fails()
        {
            BasicFieldNormalizationResult result = BasicFieldNormalizer.Normalize(
                BasicFieldType.ProjectName,
                "   ");

            Assert.AreEqual(BasicFieldParseStatus.Failed, result.Status);
        }

        [TestMethod]
        [DataRow("2026年7月31日")]
        [DataRow("2026-07-31")]
        [DataRow("2026/7/31")]
        [DataRow("2026.07.31")]
        public void Normalize_ValuationDateFormats_ProduceSameTypedDate(string rawValue)
        {
            BasicFieldNormalizationResult result = BasicFieldNormalizer.Normalize(
                BasicFieldType.ValuationDate,
                rawValue);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BasicFieldValueKind.Date, result.NormalizedValue.Kind);
            Assert.AreEqual("2026-07-31", result.NormalizedValue.CanonicalValue);
            Assert.AreEqual(2026, result.NormalizedValue.DateValue.Value.Year);
            Assert.AreEqual(7, result.NormalizedValue.DateValue.Value.Month);
            Assert.AreEqual(31, result.NormalizedValue.DateValue.Value.Day);
        }

        [TestMethod]
        public void Normalize_InvalidDate_FailsWithoutDateValue()
        {
            BasicFieldNormalizationResult result = BasicFieldNormalizer.Normalize(
                BasicFieldType.ValuationDate,
                "2026年2月30日");

            Assert.AreEqual(BasicFieldParseStatus.Failed, result.Status);
            Assert.IsNull(result.NormalizedValue);
            StringAssert.Contains(result.FailureReason, "未生成虚假日期标准值");
        }

        [TestMethod]
        public void Normalize_MixedDateSeparators_FailsConservatively()
        {
            BasicFieldNormalizationResult result = BasicFieldNormalizer.Normalize(
                BasicFieldType.ValuationDate,
                "2026-07/31");

            Assert.AreEqual(BasicFieldParseStatus.Failed, result.Status);
        }

        [TestMethod]
        public void Normalize_ProjectName_OnlyCollapsesWhitespace()
        {
            BasicFieldNormalizationResult result = BasicFieldNormalizer.Normalize(
                BasicFieldType.ProjectName,
                "  XX测试公司　拟资产处置  项目  ");

            Assert.AreEqual("XX测试公司 拟资产处置 项目", result.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Normalize_ReportNumber_NormalizesSpacesAndYearBracketsAsWholeString()
        {
            BasicFieldNormalizationResult result = BasicFieldNormalizer.Normalize(
                BasicFieldType.ReportNumber,
                " 云测试评报字 （2026） 第001号 ");

            Assert.AreEqual("云测试评报字〔2026〕第001号", result.NormalizedValue.CanonicalValue);
            Assert.AreEqual(BasicFieldValueKind.Text, result.NormalizedValue.Kind);
        }

    }
}
