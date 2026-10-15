using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class AssessmentObjectSemanticEnhancerTests
    {
        [TestMethod]
        public void AssetTypeDifference_UsesSemanticAnalyzer()
        {
            AssessmentObjectFact original = CreateFact(assetType: "供配电资产");
            AssessmentObjectFact candidate = CreateFact(assetType: "配电设施");
            AssessmentObjectSemanticEnhancer enhancer = CreateEnhancer(
                "AssetType",
                original.AssetType,
                candidate.AssetType);

            IReadOnlyList<SemanticFieldComparisonResult> results = enhancer.Enhance(
                original,
                candidate);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("AssetType", results[0].FieldName);
        }

        [TestMethod]
        public void EconomicActionDifference_UsesSemanticAnalyzer()
        {
            AssessmentObjectFact original = CreateFact(economicAction: "接收");
            AssessmentObjectFact candidate = CreateFact(economicAction: "承接");
            AssessmentObjectSemanticEnhancer enhancer = CreateEnhancer(
                "EconomicAction",
                original.EconomicAction,
                candidate.EconomicAction);

            IReadOnlyList<SemanticFieldComparisonResult> results = enhancer.Enhance(
                original,
                candidate);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("EconomicAction", results[0].FieldName);
        }

        [TestMethod]
        public void ScopeDifference_UsesSemanticAnalyzer()
        {
            AssessmentObjectFact original = CreateFact(scope: "全部供配电资产");
            AssessmentObjectFact candidate = CreateFact(scope: "供配电资产全体");
            AssessmentObjectSemanticEnhancer enhancer = CreateEnhancer(
                "Scope",
                original.Scope,
                candidate.Scope);

            IReadOnlyList<SemanticFieldComparisonResult> results = enhancer.Enhance(
                original,
                candidate);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Scope", results[0].FieldName);
        }

        [TestMethod]
        public void QuantityDifference_DoesNotUseSemanticAnalyzer()
        {
            AssessmentObjectFact original = CreateFact(quantity: 6m);
            AssessmentObjectFact candidate = CreateFact(quantity: 7m);
            AssessmentObjectSemanticEnhancer enhancer =
                new AssessmentObjectSemanticEnhancer(new MockSemanticAnalyzer());

            IReadOnlyList<SemanticFieldComparisonResult> results = enhancer.Enhance(
                original,
                candidate);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void OwnerDifference_DoesNotUseSemanticAnalyzer()
        {
            AssessmentObjectFact original = CreateFact(owner: "甲方测试单位");
            AssessmentObjectFact candidate = CreateFact(owner: "乙方测试单位");
            AssessmentObjectSemanticEnhancer enhancer =
                new AssessmentObjectSemanticEnhancer(new MockSemanticAnalyzer());

            IReadOnlyList<SemanticFieldComparisonResult> results = enhancer.Enhance(
                original,
                candidate);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void Enhance_ReturnsFieldComparisonResultWithAnalyzerValues()
        {
            AssessmentObjectFact original = CreateFact(assetType: "供配电资产");
            AssessmentObjectFact candidate = CreateFact(assetType: "配电设施");
            AssessmentObjectSemanticEnhancer enhancer = CreateEnhancer(
                "AssetType",
                original.AssetType,
                candidate.AssetType,
                similarityScore: 0.91m,
                confidence: 0.87m);

            SemanticFieldComparisonResult result = enhancer.Enhance(
                original,
                candidate)[0];

            Assert.AreEqual("AssetType", result.FieldName);
            Assert.AreEqual("供配电资产", result.OriginalValue);
            Assert.AreEqual("配电设施", result.CandidateValue);
            Assert.AreEqual(0.91m, result.SimilarityScore);
            Assert.AreEqual(0.87m, result.Confidence);
        }

        [TestMethod]
        public void Enhance_PreservesAnalyzerEvidence()
        {
            AssessmentObjectFact original = CreateFact(scope: "全部供配电资产");
            AssessmentObjectFact candidate = CreateFact(scope: "供配电资产全体");
            AssessmentObjectSemanticEnhancer enhancer = CreateEnhancer(
                "Scope",
                original.Scope,
                candidate.Scope,
                evidence: new[] { "报告A正文第3段", "报告B正文第5段" });

            SemanticFieldComparisonResult result = enhancer.Enhance(
                original,
                candidate)[0];

            CollectionAssert.AreEqual(
                new[] { "报告A正文第3段", "报告B正文第5段" },
                new List<string>(result.Evidence));
        }

        [TestMethod]
        public void Enhance_RejectsNullFacts()
        {
            AssessmentObjectSemanticEnhancer enhancer =
                new AssessmentObjectSemanticEnhancer(new MockSemanticAnalyzer());
            AssessmentObjectFact fact = CreateFact();

            Assert.ThrowsExactly<ArgumentNullException>(() => enhancer.Enhance(null, fact));
            Assert.ThrowsExactly<ArgumentNullException>(() => enhancer.Enhance(fact, null));
        }

        [TestMethod]
        public void MissingSemanticFields_ReturnsNoResults()
        {
            AssessmentObjectFact incompleteFact = new AssessmentObjectFact(
                "report-a.docx",
                CreateLocation(),
                "评估对象信息不完整",
                CreateConfidence());
            AssessmentObjectSemanticEnhancer enhancer =
                new AssessmentObjectSemanticEnhancer(new MockSemanticAnalyzer());

            IReadOnlyList<SemanticFieldComparisonResult> results = enhancer.Enhance(
                incompleteFact,
                CreateFact());

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void AnalyzerFailure_ThrowsWithoutCreatingGuessedResult()
        {
            AssessmentObjectFact original = CreateFact(assetType: "供配电资产");
            AssessmentObjectFact candidate = CreateFact(assetType: "配电设施");
            AssessmentObjectSemanticEnhancer enhancer =
                new AssessmentObjectSemanticEnhancer(new MockSemanticAnalyzer());

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => enhancer.Enhance(original, candidate));

            StringAssert.Contains(exception.Message, "AssetType");
            Assert.IsInstanceOfType<ArgumentException>(exception.InnerException);
        }

        private static AssessmentObjectSemanticEnhancer CreateEnhancer(
            string fieldName,
            string originalValue,
            string candidateValue,
            decimal similarityScore = 0.90m,
            decimal confidence = 0.85m,
            IEnumerable<string> evidence = null)
        {
            string input = BuildSemanticInput(fieldName, originalValue, candidateValue);
            SemanticAnalysisResult fixedResult = new SemanticAnalysisResult(
                "assessment-object-test-input",
                null,
                similarityScore,
                confidence,
                evidence ?? new[] { "固定语义测试证据" });

            return new AssessmentObjectSemanticEnhancer(
                new MockSemanticAnalyzer(input, fixedResult));
        }

        private static string BuildSemanticInput(
            string fieldName,
            string originalValue,
            string candidateValue)
        {
            return string.Concat(
                "FieldName=",
                fieldName,
                "\nOriginalValue=",
                originalValue,
                "\nCandidateValue=",
                candidateValue);
        }

        private static AssessmentObjectFact CreateFact(
            string assetType = "供配电资产",
            string owner = "虚构测试单位",
            string economicAction = "接收",
            decimal quantity = 6m,
            string scope = "6家客户对应的全部资产")
        {
            return new AssessmentObjectFact(
                "report.docx",
                CreateLocation(),
                string.Concat(owner, economicAction, scope),
                CreateConfidence(),
                assetType,
                owner,
                economicAction,
                quantity,
                scope);
        }

        private static DocumentLocation CreateLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                1,
                null,
                null,
                null,
                new[] { "评估对象" });
        }

        private static FactConfidence CreateConfidence()
        {
            return new FactConfidence(FactConfidenceBand.High, 0.95m);
        }
    }
}
