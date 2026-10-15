using System.Collections.Generic;
using System.Globalization;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Extraction.Tests
{
    [TestClass]
    public sealed class AssessmentValueFactExtractorTests
    {
        [TestMethod]
        [DataRow("评估价值为998.22万元", "9982200.00")]
        [DataRow("评估价值合计人民币1000万元", "10000000.00")]
        [DataRow("评估价值为9,982,200元", "9982200.00")]
        public void Extract_ExplicitAssessmentValue_ReturnsNormalizedFact(
            string text,
            string expectedValue)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(text, 0));

            IReadOnlyList<AssessmentValueFact> facts =
                new AssessmentValueFactExtractor().Extract(document, "report-a");

            Assert.HasCount(1, facts);
            Assert.AreEqual(FactType.AssessmentValue, facts[0].FactType);
            Assert.AreEqual(expectedValue, facts[0].Value);
            Assert.AreEqual(
                decimal.Parse(expectedValue, CultureInfo.InvariantCulture),
                facts[0].NormalizedAmountYuan);
        }

        [TestMethod]
        public void Extract_TextWithoutMoney_ReturnsEmptyCollection()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("评估价值暂未确定。", 0));

            IReadOnlyList<AssessmentValueFact> facts =
                new AssessmentValueFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_AssessmentValue_PreservesSourceLocation()
        {
            DocumentElement element = CreateParagraph(
                "评估价值为998.22万元",
                4);
            DocumentReadResult document = CreateDocument(element);

            AssessmentValueFact fact =
                new AssessmentValueFactExtractor()
                    .Extract(document, "report-a")[0];

            Assert.AreSame(element.Location, fact.SourceLocation);
            Assert.AreEqual(4, fact.SourceLocation.ParagraphIndex);
        }

        [TestMethod]
        public void Extract_AssessmentValue_PreservesOriginalMoneyText()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("评估价值为998.22万元", 0));

            AssessmentValueFact fact =
                new AssessmentValueFactExtractor()
                    .Extract(document, "report-a")[0];

            Assert.AreEqual("998.22万元", fact.OriginalText);
        }

        [TestMethod]
        public void Extract_AssessmentValue_ReturnsLegalConfidence()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("评估价值为998.22万元", 0));

            AssessmentValueFact fact =
                new AssessmentValueFactExtractor()
                    .Extract(document, "report-a")[0];

            Assert.AreNotEqual(FactConfidenceBand.None, fact.Confidence.Band);
            Assert.IsTrue(fact.Confidence.Score >= 0m);
            Assert.IsTrue(fact.Confidence.Score <= 1m);
        }

        [TestMethod]
        [DataRow("含税评估价值为998.22万元")]
        [DataRow("不含税评估价值为998.22万元")]
        [DataRow("评估净值为998.22万元")]
        [DataRow("评估总值为998.22万元")]
        [DataRow("单项资产评估价值为998.22万元")]
        [DataRow("计算案例：评估价值为998.22万元")]
        public void Extract_UnsupportedAssessmentValueQualification_ReturnsEmptyCollection(
            string text)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(text, 0));

            IReadOnlyList<AssessmentValueFact> facts =
                new AssessmentValueFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        private static DocumentReadResult CreateDocument(
            params DocumentElement[] elements)
        {
            return DocumentReadResult.Succeeded(
                elements,
                elements.Length,
                0);
        }

        private static DocumentElement CreateParagraph(
            string text,
            int paragraphIndex)
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                paragraphIndex,
                null,
                null,
                null,
                new[] { "评估结论" });

            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                paragraphIndex,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
