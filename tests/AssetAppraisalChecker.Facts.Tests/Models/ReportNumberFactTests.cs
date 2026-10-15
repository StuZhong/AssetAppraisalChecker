using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Tests.Models
{
    [TestClass]
    public sealed class ReportNumberFactTests
    {
        [TestMethod]
        public void Constructor_PreservesNormalizedNumberSourceConfidenceAndEvidence()
        {
            DocumentLocation location = CreateLocation();
            var confidence = new FactConfidence(
                FactConfidenceBand.High,
                0.95m);
            var sourceEvidence = new List<string>
            {
                "明确报告编号标签后的值。"
            };

            var fact = new ReportNumberFact(
                "report.docx",
                location,
                "云合成评报字（2026）第001号",
                confidence,
                "云合成评报字〔2026〕第001号",
                sourceEvidence);

            sourceEvidence.Add("external mutation");

            Assert.AreEqual(FactType.ReportNumber, fact.FactType);
            Assert.AreEqual(
                "云合成评报字〔2026〕第001号",
                fact.Value);
            Assert.AreEqual(fact.Value, fact.NormalizedNumber);
            Assert.AreEqual("report.docx", fact.SourceDocument);
            Assert.AreSame(location, fact.SourceLocation);
            Assert.AreEqual(
                "云合成评报字（2026）第001号",
                fact.OriginalText);
            Assert.AreSame(confidence, fact.Confidence);
            Assert.HasCount(2, fact.Evidence);
            StringAssert.Contains(fact.Evidence[0], "ParagraphIndex=3");
            StringAssert.Contains(
                fact.Evidence[0],
                "NormalizedNumber=云合成评报字〔2026〕第001号");
            StringAssert.Contains(
                fact.Evidence[0],
                "CandidateBasis=DeterministicCoreCandidate");
            StringAssert.Contains(fact.Evidence[1], "CoreEvidence=");
        }

        [TestMethod]
        public void Constructor_RejectsNonCanonicalOrEmptyNumber()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact("云合成评报字（2026）第001号"));
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact("云合成评报字 〔2026〕 第001号"));
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact(" "));
        }

        [TestMethod]
        public void Evidence_IsReadOnlySnapshot()
        {
            ReportNumberFact fact = CreateFact(
                "云合成评报字〔2026〕第001号");

            Assert.ThrowsExactly<NotSupportedException>(
                () => ((IList<string>)fact.Evidence).Add("mutation"));
        }

        [TestMethod]
        public void Constructor_RejectsNullOrEmptyExtractionEvidenceItems()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new ReportNumberFact(
                    "report.docx",
                    CreateLocation(),
                    "云合成评报字〔2026〕第001号",
                    CreateConfidence(),
                    "云合成评报字〔2026〕第001号",
                    null));
            Assert.ThrowsExactly<ArgumentException>(
                () => new ReportNumberFact(
                    "report.docx",
                    CreateLocation(),
                    "云合成评报字〔2026〕第001号",
                    CreateConfidence(),
                    "云合成评报字〔2026〕第001号",
                    new[] { "" }));
        }

        private static ReportNumberFact CreateFact(string normalizedNumber)
        {
            return new ReportNumberFact(
                "report.docx",
                CreateLocation(),
                "报告编号原文",
                CreateConfidence(),
                normalizedNumber);
        }

        private static FactConfidence CreateConfidence()
        {
            return new FactConfidence(FactConfidenceBand.High, 0.95m);
        }

        private static DocumentLocation CreateLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                3,
                null,
                null,
                null,
                new[] { "基本事项" });
        }
    }
}
