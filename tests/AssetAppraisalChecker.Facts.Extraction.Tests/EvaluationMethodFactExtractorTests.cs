using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Extraction.Tests
{
    [TestClass]
    public sealed class EvaluationMethodFactExtractorTests
    {
        [TestMethod]
        public void Extract_ExplicitCostMethod_ReturnsFact()
        {
            EvaluationMethodFact fact = ExtractSingle("本次评估采用成本法。");

            Assert.AreEqual(FactType.EvaluationMethod, fact.FactType);
            Assert.AreEqual("成本法", fact.Value);
        }

        [TestMethod]
        public void Extract_ExplicitMarketMethod_ReturnsFact()
        {
            EvaluationMethodFact fact = ExtractSingle("本次评估采用市场法。");

            Assert.AreEqual(FactType.EvaluationMethod, fact.FactType);
            Assert.AreEqual("市场法", fact.Value);
        }

        [TestMethod]
        public void Extract_ExplicitIncomeMethod_ReturnsFact()
        {
            EvaluationMethodFact fact = ExtractSingle("本次评估采用收益法。");

            Assert.AreEqual(FactType.EvaluationMethod, fact.FactType);
            Assert.AreEqual("收益法", fact.Value);
        }

        [TestMethod]
        public void Extract_TextWithoutSupportedMethod_ReturnsEmptyCollection()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("本次评估使用经审核的计算参数。", 0));

            IReadOnlyList<EvaluationMethodFact> facts =
                new EvaluationMethodFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_EvaluationMethod_PreservesCompleteEvidence()
        {
            const string sourceDocument = "report-source-id";
            DocumentElement element = CreateParagraph(
                "本次评估采用市场法。",
                3);
            DocumentReadResult document = CreateDocument(element);

            EvaluationMethodFact fact =
                new EvaluationMethodFactExtractor()
                    .Extract(document, sourceDocument)[0];

            Assert.AreEqual(FactType.EvaluationMethod, fact.FactType);
            Assert.AreEqual("市场法", fact.Value);
            Assert.AreEqual(sourceDocument, fact.SourceDocument);
            Assert.AreSame(element.Location, fact.SourceLocation);
            Assert.AreEqual("采用市场法", fact.OriginalText);
        }

        [TestMethod]
        public void Extract_EvaluationMethod_ReturnsConfidenceInUnitInterval()
        {
            EvaluationMethodFact fact = ExtractSingle("本次评估采用收益法。");

            Assert.AreNotEqual(FactConfidenceBand.None, fact.Confidence.Band);
            Assert.IsTrue(fact.Confidence.Score >= 0m);
            Assert.IsTrue(fact.Confidence.Score <= 1m);
        }

        [TestMethod]
        public void Extract_ReplacementCostMethod_ReturnsDistinctStructuredFact()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("本次评估采用重置成本法。", 0));

            IReadOnlyList<EvaluationMethodFact> facts =
                new EvaluationMethodFactExtractor().Extract(document, "report-a");

            Assert.HasCount(1, facts);
            Assert.AreEqual(EvaluationMethodCode.ReplacementCostMethod, facts[0].MethodCode);
            Assert.AreEqual("重置成本法", facts[0].Method);
        }

        [TestMethod]
        [DataRow("本次评估不采用成本法。")]
        [DataRow("本次评估未采用市场法。")]
        public void Extract_ImmediatelyNegatedMethod_DoesNotReturnFact(string text)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(text, 0));

            IReadOnlyList<EvaluationMethodFact> facts =
                new EvaluationMethodFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_SubjectBoundMultipleSubjects_ReturnsIndependentFacts()
        {
            DocumentReadResult document = CreateDocument(CreateParagraph(
                "房屋建筑物采用成本法；机器设备采用市场法。",
                0));

            IReadOnlyList<EvaluationMethodFact> facts =
                new EvaluationMethodFactExtractor().Extract(document, "report-a");

            Assert.HasCount(2, facts);
            Assert.AreEqual("subject/v1:building", facts[0].NormalizedSubjectKey);
            Assert.AreEqual("subject/v1:machinery-equipment", facts[1].NormalizedSubjectKey);
        }

        [TestMethod]
        public void Extract_SameSubjectMultipleMethods_PreservesCompleteSet()
        {
            DocumentReadResult document = CreateDocument(CreateParagraph(
                "房屋建筑物采用成本法和市场法。",
                0));

            IReadOnlyList<EvaluationMethodFact> facts =
                new EvaluationMethodFactExtractor().Extract(document, "report-a");

            Assert.HasCount(2, facts);
            Assert.IsTrue(facts.All(item => item.SetCompleteness == EvaluationMethodSetCompleteness.Complete));
            Assert.AreEqual(facts[0].MethodSetId, facts[1].MethodSetId);
        }

        [TestMethod]
        public void Extract_NegatedThenAdopted_OnlyReturnsAdoptedMethod()
        {
            DocumentReadResult document = CreateDocument(CreateParagraph(
                "房屋建筑物不采用成本法，采用市场法。",
                0));
            var extractor = new EvaluationMethodFactExtractor();

            EvaluationMethodExtractionResult result =
                extractor.ExtractWithDiagnostics(document, "report-a");

            Assert.HasCount(1, result.Facts);
            Assert.AreEqual(EvaluationMethodCode.MarketApproach, result.Facts[0].MethodCode);
            Assert.IsTrue(result.Statements.Any(item =>
                item.MethodCode == EvaluationMethodCode.CostApproach
                && item.Applicability == EvaluationMethodApplicability.Rejected));
        }

        [TestMethod]
        public void Extract_CompetingSubjectCandidates_FailsClosed()
        {
            DocumentReadResult document = CreateDocument(CreateParagraph(
                "房屋建筑物和机器设备采用成本法。",
                0));

            EvaluationMethodExtractionResult result =
                new EvaluationMethodFactExtractor().ExtractWithDiagnostics(document, "report-a");

            Assert.IsEmpty(result.Facts);
            Assert.HasCount(1, result.Statements);
            Assert.AreEqual("subject/v1:unresolved", result.Statements[0].NormalizedSubjectKey);
        }

        [TestMethod]
        public void Extract_NonStandardDescription_DoesNotGuessAdoptedMethod()
        {
            DocumentReadResult document = CreateDocument(CreateParagraph(
                "房屋建筑物按现行重建成本测算。",
                0));

            EvaluationMethodExtractionResult result =
                new EvaluationMethodFactExtractor().ExtractWithDiagnostics(document, "report-a");

            Assert.IsEmpty(result.Facts);
            Assert.AreEqual(EvaluationMethodApplicability.CandidateUnknown, result.Statements[0].Applicability);
        }

        private static EvaluationMethodFact ExtractSingle(string text)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(text, 0));

            IReadOnlyList<EvaluationMethodFact> facts =
                new EvaluationMethodFactExtractor().Extract(document, "report-a");

            Assert.HasCount(1, facts);
            return facts[0];
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
                new[] { "评估方法" });

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
