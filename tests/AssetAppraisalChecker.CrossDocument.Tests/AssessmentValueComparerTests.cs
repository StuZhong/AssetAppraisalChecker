using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.CrossDocument.Tests
{
    [TestClass]
    public sealed class AssessmentValueComparerTests
    {
        [TestMethod]
        public void EqualNormalizedAmounts_ReturnPass()
        {
            var comparer = new AssessmentValueComparer();

            ConsistencyResult result = comparer.Compare(
                CreateFact("report", "998.22万元", 9982200.00m),
                CreateFact("explanation", "9,982,200元", 9982200.00m));

            Assert.AreEqual(FactType.AssessmentValue, result.FactType);
            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
            StringAssert.Contains(result.Reason, "精确相等");
        }

        [TestMethod]
        public void DifferentNormalizedAmounts_ReturnFailWithDifference()
        {
            var comparer = new AssessmentValueComparer();

            ConsistencyResult result = comparer.Compare(
                CreateFact("report", "998.22万元", 9982200.00m),
                CreateFact("explanation", "1000万元", 10000000.00m));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "17800");
        }

        [TestMethod]
        public void MissingFact_ReturnsWarning()
        {
            var comparer = new AssessmentValueComparer();

            ConsistencyResult result = comparer.Compare(
                CreateFact("report", "998.22万元", 9982200.00m),
                null);

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "缺少");
        }

        [TestMethod]
        public void MissingNormalizedAmount_ReturnsWarning()
        {
            var comparer = new AssessmentValueComparer();
            var factWithoutAmount = new AssessmentValueFact(
                "explanation",
                CreateLocation(),
                "评估价值暂未确定",
                CreateConfidence());

            ConsistencyResult result = comparer.Compare(
                CreateFact("report", "998.22万元", 9982200.00m),
                factWithoutAmount);

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "decimal");
        }

        [TestMethod]
        public void Result_PreservesEvidenceFromBothDocuments()
        {
            var comparer = new AssessmentValueComparer();

            ConsistencyResult result = comparer.Compare(
                CreateFact("report", "998.22万元", 9982200.00m),
                CreateFact("explanation", "9,982,200元", 9982200.00m));

            Assert.HasCount(2, result.Evidence);
            StringAssert.Contains(result.Evidence[0], "report | 998.22万元");
            StringAssert.Contains(result.Evidence[0], "NormalizedAmountYuan=9982200");
            StringAssert.Contains(result.Evidence[1], "explanation | 9,982,200元");
            StringAssert.Contains(result.Evidence[1], "NormalizedAmountYuan=9982200");
        }

        private static AssessmentValueFact CreateFact(
            string sourceDocument,
            string originalText,
            decimal normalizedAmountYuan)
        {
            return new AssessmentValueFact(
                sourceDocument,
                CreateLocation(),
                originalText,
                CreateConfidence(),
                normalizedAmountYuan);
        }

        private static DocumentLocation CreateLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                1,
                null,
                null,
                null,
                new[] { "评估结论" });
        }

        private static FactConfidence CreateConfidence()
        {
            return new FactConfidence(FactConfidenceBand.High, 0.95m);
        }
    }
}
