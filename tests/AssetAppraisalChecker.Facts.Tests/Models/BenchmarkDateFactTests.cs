using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Tests.Models
{
    [TestClass]
    public sealed class BenchmarkDateFactTests
    {
        [TestMethod]
        public void StandardDateConstructor_PreservesValueSourceConfidenceAndEvidence()
        {
            DocumentLocation location = CreateLocation();
            var confidence = new FactConfidence(
                FactConfidenceBand.High,
                0.95m);

            var fact = new BenchmarkDateFact(
                "report.docx",
                location,
                "2026年7月31日",
                confidence,
                "2026-07-31");

            Assert.AreEqual(FactType.BenchmarkDate, fact.FactType);
            Assert.AreEqual("2026-07-31", fact.Value);
            Assert.AreEqual(new DateTime(2026, 7, 31), fact.NormalizedDate);
            Assert.AreEqual("report.docx", fact.SourceDocument);
            Assert.AreSame(location, fact.SourceLocation);
            Assert.AreEqual("2026年7月31日", fact.OriginalText);
            Assert.AreSame(confidence, fact.Confidence);
            Assert.HasCount(1, fact.Evidence);
            StringAssert.Contains(fact.Evidence[0], "report.docx");
            StringAssert.Contains(fact.Evidence[0], "ParagraphIndex=3");
            StringAssert.Contains(fact.Evidence[0], "2026年7月31日");
            StringAssert.Contains(fact.Evidence[0], "NormalizedDate=2026-07-31");
        }

        [TestMethod]
        public void Constructor_RejectsNonCanonicalOrInvalidDateValue()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact("2026/07/31"));
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact("2026-7-31"));
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact("2026-02-29"));
            Assert.ThrowsExactly<ArgumentException>(
                () => CreateFact(" "));
        }

        [TestMethod]
        public void ConstructorWithoutValue_HasNoNormalizedDateAndKeepsEvidence()
        {
            var fact = new BenchmarkDateFact(
                "report.docx",
                CreateLocation(),
                "评估基准日原文",
                new FactConfidence(FactConfidenceBand.Medium, 0.75m));

            Assert.IsNull(fact.Value);
            Assert.IsNull(fact.NormalizedDate);
            Assert.HasCount(1, fact.Evidence);
            StringAssert.Contains(fact.Evidence[0], "NormalizedDate=<missing>");
        }

        [TestMethod]
        public void Evidence_IsReadOnlySnapshot()
        {
            BenchmarkDateFact fact = CreateFact("2026-07-31");

            Assert.ThrowsExactly<NotSupportedException>(
                () => ((IList<string>)fact.Evidence).Add("external mutation"));
        }

        private static BenchmarkDateFact CreateFact(string value)
        {
            return new BenchmarkDateFact(
                "report.docx",
                CreateLocation(),
                "评估基准日：2026年7月31日",
                new FactConfidence(FactConfidenceBand.High, 0.95m),
                value);
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
