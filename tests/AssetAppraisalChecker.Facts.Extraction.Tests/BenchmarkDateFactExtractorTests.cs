using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Extraction.Tests
{
    [TestClass]
    public sealed class BenchmarkDateFactExtractorTests
    {
        [TestMethod]
        [DataRow("评估基准日：2026年7月31日", "2026年7月31日")]
        [DataRow("评估基准日：2026-07-31", "2026-07-31")]
        [DataRow("评估基准日：2026/7/31", "2026/7/31")]
        [DataRow("评估基准日：2026.07.31", "2026.07.31")]
        public void Extract_SupportedDateFormats_ReturnNormalizedFact(
            string documentText,
            string expectedOriginalText)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(documentText, 2));

            IReadOnlyList<BenchmarkDateFact> facts =
                new BenchmarkDateFactExtractor().Extract(document, "report-a");

            Assert.HasCount(1, facts);
            Assert.AreEqual(FactType.BenchmarkDate, facts[0].FactType);
            Assert.AreEqual("2026-07-31", facts[0].Value);
            Assert.AreEqual(expectedOriginalText, facts[0].OriginalText);
            Assert.IsTrue(facts[0].NormalizedDate.HasValue);
        }

        [TestMethod]
        public void Extract_DocumentWithoutBenchmarkDate_ReturnsEmptyCollection()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("报告出具日期：2026年7月31日", 0),
                CreateParagraph("本报告未列示相关日期。", 1));

            IReadOnlyList<BenchmarkDateFact> facts =
                new BenchmarkDateFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_BenchmarkDate_PreservesCompleteSourceTraceAndEvidence()
        {
            const string sourceDocument = "report-source-id";
            const string text = "评估基准日：2026/07/31";
            DocumentElement element = CreateParagraph(text, 4);
            DocumentReadResult document = CreateDocument(element);

            BenchmarkDateFact fact =
                new BenchmarkDateFactExtractor()
                    .Extract(document, sourceDocument)[0];

            Assert.AreEqual(sourceDocument, fact.SourceDocument);
            Assert.AreSame(element.Location, fact.SourceLocation);
            Assert.AreEqual("2026/07/31", fact.OriginalText);
            Assert.AreEqual(FactConfidenceBand.High, fact.Confidence.Band);
            Assert.AreEqual(0.95m, fact.Confidence.Score);
            Assert.HasCount(1, fact.Evidence);
            StringAssert.Contains(fact.Evidence[0], sourceDocument);
            StringAssert.Contains(fact.Evidence[0], "ParagraphIndex=4");
            StringAssert.Contains(fact.Evidence[0], "NormalizedDate=2026-07-31");
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
                new[] { "基本事项" });

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
