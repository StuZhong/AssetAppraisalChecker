using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Tests.Models
{
    [TestClass]
    public sealed class AssessmentValueFactTests
    {
        [TestMethod]
        public void DecimalConstructor_PreservesNormalizedAmountAndSource()
        {
            DocumentLocation location = CreateLocation();
            var confidence = new FactConfidence(
                FactConfidenceBand.High,
                0.95m);

            var fact = new AssessmentValueFact(
                "report.docx",
                location,
                "998.22万元",
                confidence,
                9982200.00m);

            Assert.AreEqual(FactType.AssessmentValue, fact.FactType);
            Assert.AreEqual(9982200.00m, fact.NormalizedAmountYuan);
            Assert.AreEqual("9982200.00", fact.Value);
            Assert.AreEqual("report.docx", fact.SourceDocument);
            Assert.AreSame(location, fact.SourceLocation);
            Assert.AreEqual("998.22万元", fact.OriginalText);
            Assert.AreSame(confidence, fact.Confidence);
        }

        [TestMethod]
        public void StringConstructor_PreservesLegacyValueAndParsesDecimalWhenPossible()
        {
            var fact = new AssessmentValueFact(
                "report.docx",
                CreateLocation(),
                "170万元",
                new FactConfidence(FactConfidenceBand.High, 0.95m),
                "1700000");

            Assert.AreEqual("1700000", fact.Value);
            Assert.AreEqual(1700000m, fact.NormalizedAmountYuan);
        }

        [TestMethod]
        public void ConstructorWithoutValue_HasNoNormalizedAmount()
        {
            var fact = new AssessmentValueFact(
                "report.docx",
                CreateLocation(),
                "评估价值原文",
                new FactConfidence(FactConfidenceBand.High, 0.95m));

            Assert.IsNull(fact.NormalizedAmountYuan);
        }

        private static DocumentLocation CreateLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                3,
                null,
                null,
                null,
                new[] { "评估结论" });
        }
    }
}
