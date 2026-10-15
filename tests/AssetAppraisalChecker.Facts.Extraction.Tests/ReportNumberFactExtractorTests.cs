using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Extraction.Tests
{
    [TestClass]
    public sealed class ReportNumberFactExtractorTests
    {
        [TestMethod]
        [DataRow(
            "报告编号：云合成评报字（2026）第001号",
            "云合成评报字（2026）第001号")]
        [DataRow(
            "报告编号：云合成评报字(2026)第001号",
            "云合成评报字(2026)第001号")]
        [DataRow(
            "报告编号：云合成评报字[2026]第001号",
            "云合成评报字[2026]第001号")]
        [DataRow(
            "报告编号：云合成评报字【2026】第001号",
            "云合成评报字【2026】第001号")]
        [DataRow(
            "报告编号： 云合成评报字 〔 2026 〕 第 001 号 ",
            "云合成评报字 〔 2026 〕 第 001 号")]
        public void Extract_SupportedBracketsAndSpaces_ReturnCanonicalFact(
            string documentText,
            string expectedOriginalText)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(documentText, 2));

            IReadOnlyList<ReportNumberFact> facts =
                new ReportNumberFactExtractor().Extract(document, "report-a");

            Assert.HasCount(1, facts);
            Assert.AreEqual(FactType.ReportNumber, facts[0].FactType);
            Assert.AreEqual(
                "云合成评报字〔2026〕第001号",
                facts[0].NormalizedNumber);
            Assert.AreEqual(facts[0].NormalizedNumber, facts[0].Value);
            Assert.AreEqual(expectedOriginalText, facts[0].OriginalText);
        }

        [TestMethod]
        public void Extract_DocumentWithoutReportNumber_ReturnsEmptyCollection()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("合同编号：SYNTHETIC-2026-001", 0),
                CreateParagraph(
                    "本项目参考云合成评报字〔2026〕第001号报告进行分析。",
                    1));

            IReadOnlyList<ReportNumberFact> facts =
                new ReportNumberFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_ReportNumber_PreservesSourceTraceConfidenceAndEvidence()
        {
            const string sourceDocument = "report-source-id";
            const string text =
                "报告编号：云合成评报字（2026）第001号";
            DocumentElement element = CreateParagraph(text, 4);

            ReportNumberFact fact = new ReportNumberFactExtractor()
                .Extract(CreateDocument(element), sourceDocument)
                .Single();

            Assert.AreEqual(sourceDocument, fact.SourceDocument);
            Assert.AreSame(element.Location, fact.SourceLocation);
            Assert.AreEqual(
                "云合成评报字（2026）第001号",
                fact.OriginalText);
            Assert.AreEqual(FactConfidenceBand.High, fact.Confidence.Band);
            Assert.AreEqual(0.95m, fact.Confidence.Score);
            Assert.IsTrue(fact.Evidence.Any(
                item => item.IndexOf(
                    "CandidateBasis=DeterministicCoreCandidate",
                    StringComparison.Ordinal) >= 0));
            Assert.IsTrue(fact.Evidence.Any(
                item => item.IndexOf(
                    "CoreEvidence=",
                    StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void Extract_RepeatedReportNumber_PreservesAllOccurrences()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(
                    "报告编号：云合成评报字〔2026〕第001号",
                    0),
                CreateParagraph(
                    "云合成评报字（2026）第001号",
                    1));

            IReadOnlyList<ReportNumberFact> facts =
                new ReportNumberFactExtractor().Extract(document, "report-a");

            Assert.HasCount(2, facts);
            Assert.IsTrue(facts.All(
                fact => fact.NormalizedNumber ==
                    "云合成评报字〔2026〕第001号"));
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
                new DocumentNearbyContext(
                    string.Empty,
                    text,
                    string.Empty));
        }
    }
}
