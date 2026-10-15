using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Tests.Models
{
    [TestClass]
    public sealed class BusinessFactModelTests
    {
        [TestMethod]
        public void ConcreteFacts_ExposeRequiredSourceAndConfidenceFields()
        {
            DocumentLocation location = CreateLocation();
            FactConfidence confidence = new FactConfidence(FactConfidenceBand.High, 0.95m);

            IReadOnlyList<BusinessFact> facts = new BusinessFact[]
            {
                new AssessmentObjectFact("report.docx", location, "评估对象原文", confidence),
                new AssessmentScopeFact("report.docx", location, "评估范围原文", confidence),
                new EvaluationMethodFact("report.docx", location, "评估方法原文", confidence),
                new AssessmentValueFact("report.docx", location, "评估价值原文", confidence),
                new BenchmarkDateFact("report.docx", location, "评估基准日原文", confidence)
            };

            foreach (BusinessFact fact in facts)
            {
                Assert.AreEqual("report.docx", fact.SourceDocument);
                Assert.AreSame(location, fact.SourceLocation);
                Assert.IsFalse(string.IsNullOrWhiteSpace(fact.OriginalText));
                Assert.AreSame(confidence, fact.Confidence);
            }
        }

        [TestMethod]
        public void ConcreteFacts_UseTheirFixedFactTypes()
        {
            DocumentLocation location = CreateLocation();
            FactConfidence confidence = new FactConfidence(FactConfidenceBand.High, 0.95m);

            Assert.AreEqual(
                FactType.AssessmentObject,
                new AssessmentObjectFact("report.docx", location, "对象", confidence).FactType);
            Assert.AreEqual(
                FactType.AssessmentScope,
                new AssessmentScopeFact("report.docx", location, "范围", confidence).FactType);
            Assert.AreEqual(
                FactType.EvaluationMethod,
                new EvaluationMethodFact("report.docx", location, "方法", confidence).FactType);
            Assert.AreEqual(
                FactType.AssessmentValue,
                new AssessmentValueFact("report.docx", location, "价值", confidence).FactType);
            Assert.AreEqual(
                FactType.BenchmarkDate,
                new BenchmarkDateFact("report.docx", location, "基准日", confidence).FactType);
        }

        [TestMethod]
        public void Constructor_RejectsMissingSourceDocument()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new AssessmentObjectFact(
                    " ",
                    CreateLocation(),
                    "评估对象原文",
                    new FactConfidence(FactConfidenceBand.High, 0.95m)));
        }

        [TestMethod]
        public void Constructor_RejectsMissingSourceLocation()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new AssessmentObjectFact(
                    "report.docx",
                    null,
                    "评估对象原文",
                    new FactConfidence(FactConfidenceBand.High, 0.95m)));
        }

        [TestMethod]
        public void Constructor_RejectsMissingOriginalText()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new AssessmentObjectFact(
                    "report.docx",
                    CreateLocation(),
                    " ",
                    new FactConfidence(FactConfidenceBand.High, 0.95m)));
        }

        [TestMethod]
        public void Constructor_RejectsMissingConfidence()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new AssessmentObjectFact(
                    "report.docx",
                    CreateLocation(),
                    "评估对象原文",
                    null));
        }

        [TestMethod]
        public void FactConfidence_PreservesBandAndScore()
        {
            FactConfidence confidence = new FactConfidence(FactConfidenceBand.Medium, 0.75m);

            Assert.AreEqual(FactConfidenceBand.Medium, confidence.Band);
            Assert.AreEqual(0.75m, confidence.Score);
        }

        [TestMethod]
        public void FactConfidence_RejectsScoreOutsideUnitInterval()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new FactConfidence(FactConfidenceBand.Low, -0.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new FactConfidence(FactConfidenceBand.High, 1.01m));
        }

        [TestMethod]
        public void FactConfidence_RejectsUnknownBand()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new FactConfidence((FactConfidenceBand)999, 0.50m));
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
