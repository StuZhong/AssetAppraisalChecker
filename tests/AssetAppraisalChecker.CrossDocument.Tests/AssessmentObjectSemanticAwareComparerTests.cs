using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.CrossDocument.Tests
{
    [TestClass]
    public sealed class AssessmentObjectSemanticAwareComparerTests
    {
        [TestMethod]
        public void CompletelyIdenticalObjects_ReturnPass()
        {
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());

            ConsistencyResult result = comparer.Compare(
                CreateFact("report-a.docx"),
                CreateFact("report-b.docx"));

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
        }

        [TestMethod]
        public void SemanticallyEquivalentAssetType_ReturnsPass()
        {
            AssessmentObjectFact original = CreateFact(
                "report-a.docx",
                assetType: "供配电资产");
            AssessmentObjectFact candidate = CreateFact(
                "report-b.docx",
                assetType: "配电设施资产");
            AssessmentObjectSemanticAwareComparer comparer = CreateComparer(
                "AssetType",
                original.AssetType,
                candidate.AssetType);

            ConsistencyResult result = comparer.Compare(original, candidate);

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
        }

        [TestMethod]
        public void SemanticallyEquivalentEconomicAction_ReturnsPass()
        {
            AssessmentObjectFact original = CreateFact(
                "report-a.docx",
                economicAction: "接收");
            AssessmentObjectFact candidate = CreateFact(
                "report-b.docx",
                economicAction: "承接");
            AssessmentObjectSemanticAwareComparer comparer = CreateComparer(
                "EconomicAction",
                original.EconomicAction,
                candidate.EconomicAction);

            ConsistencyResult result = comparer.Compare(original, candidate);

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
        }

        [TestMethod]
        public void SemanticallyEquivalentScope_ReturnsPass()
        {
            AssessmentObjectFact original = CreateFact(
                "report-a.docx",
                scope: "全部供配电资产");
            AssessmentObjectFact candidate = CreateFact(
                "report-b.docx",
                scope: "供配电资产全体");
            AssessmentObjectSemanticAwareComparer comparer = CreateComparer(
                "Scope",
                original.Scope,
                candidate.Scope);

            ConsistencyResult result = comparer.Compare(original, candidate);

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
        }

        [TestMethod]
        public void QuantityConflict_ReturnsFailWithoutSemanticOverride()
        {
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());

            ConsistencyResult result = comparer.Compare(
                CreateFact("report-a.docx", quantity: 6m),
                CreateFact("report-b.docx", quantity: 7m));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "Quantity");
        }

        [TestMethod]
        public void OwnerConflict_ReturnsFailWithoutSemanticOverride()
        {
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());

            ConsistencyResult result = comparer.Compare(
                CreateFact("report-a.docx", owner: "甲方虚构资产有限公司"),
                CreateFact("report-b.docx", owner: "乙方虚构资产有限公司"));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "Owner");
        }

        [TestMethod]
        public void SemanticFailure_ReturnsWarningWithoutGuessing()
        {
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());

            ConsistencyResult result = comparer.Compare(
                CreateFact("report-a.docx", assetType: "供配电资产"),
                CreateFact("report-b.docx", assetType: "配电设施资产"));

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "Semantic");
        }

        [TestMethod]
        public void SemanticEvidence_IsPreservedInConsistencyResult()
        {
            AssessmentObjectFact original = CreateFact(
                "report-a.docx",
                scope: "全部供配电资产");
            AssessmentObjectFact candidate = CreateFact(
                "report-b.docx",
                scope: "供配电资产全体");
            AssessmentObjectSemanticAwareComparer comparer = CreateComparer(
                "Scope",
                original.Scope,
                candidate.Scope,
                evidence: new[] { "左侧语义证据", "右侧语义证据" });

            ConsistencyResult result = comparer.Compare(original, candidate);

            CollectionAssert.Contains(
                new List<string>(result.Evidence),
                "左侧语义证据");
            CollectionAssert.Contains(
                new List<string>(result.Evidence),
                "右侧语义证据");
        }

        [TestMethod]
        public void LowSemanticConfidence_ReturnsWarning()
        {
            AssessmentObjectFact original = CreateFact(
                "report-a.docx",
                assetType: "供配电资产");
            AssessmentObjectFact candidate = CreateFact(
                "report-b.docx",
                assetType: "配电设施资产");
            AssessmentObjectSemanticAwareComparer comparer = CreateComparer(
                "AssetType",
                original.AssetType,
                candidate.AssetType,
                similarityScore: 0.95m,
                confidence: 0.50m);

            ConsistencyResult result = comparer.Compare(original, candidate);

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
        }

        [TestMethod]
        public void NullInput_IsRejected()
        {
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());
            AssessmentObjectFact fact = CreateFact("report.docx");

            Assert.ThrowsExactly<ArgumentNullException>(() => comparer.Compare(null, fact));
            Assert.ThrowsExactly<ArgumentNullException>(() => comparer.Compare(fact, null));
        }

        [TestMethod]
        public void MissingFields_ReturnWarning()
        {
            AssessmentObjectFact incomplete = new AssessmentObjectFact(
                "report-a.docx",
                CreateLocation(),
                "评估对象信息不完整",
                CreateConfidence());
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());

            ConsistencyResult result = comparer.Compare(
                incomplete,
                CreateFact("report-b.docx"));

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
        }

        [TestMethod]
        public void MultipleSemanticCandidates_ReturnWarning()
        {
            AssessmentObjectFact original = CreateFact(
                "report-a.docx",
                assetType: "供配电资产");
            AssessmentObjectFact candidate = CreateFact(
                "report-b.docx",
                assetType: "配电设施资产");
            AssessmentObjectSemanticAwareComparer comparer =
                new AssessmentObjectSemanticAwareComparer(new MockSemanticAnalyzer());
            SemanticFieldComparisonResult first = CreateSemanticResult(
                "AssetType",
                original.AssetType,
                candidate.AssetType);
            SemanticFieldComparisonResult second = CreateSemanticResult(
                "AssetType",
                original.AssetType,
                candidate.AssetType);

            ConsistencyResult result = comparer.CompareWithSemanticResults(
                original,
                candidate,
                new[] { first, second });

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "多候选");
        }

        private static AssessmentObjectSemanticAwareComparer CreateComparer(
            string fieldName,
            string originalValue,
            string candidateValue,
            decimal similarityScore = 0.90m,
            decimal confidence = 0.90m,
            IEnumerable<string> evidence = null)
        {
            string expectedInput = BuildSemanticInput(
                fieldName,
                originalValue,
                candidateValue);
            SemanticAnalysisResult analysisResult = new SemanticAnalysisResult(
                "assessment-object-comparison",
                null,
                similarityScore,
                confidence,
                evidence ?? new[] { "固定 Semantic 测试证据" });

            return new AssessmentObjectSemanticAwareComparer(
                new MockSemanticAnalyzer(expectedInput, analysisResult));
        }

        private static SemanticFieldComparisonResult CreateSemanticResult(
            string fieldName,
            string originalValue,
            string candidateValue)
        {
            return new SemanticFieldComparisonResult(
                fieldName,
                originalValue,
                candidateValue,
                0.90m,
                0.90m,
                new[] { "候选测试证据" });
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
            string sourceDocument,
            string assetType = "供配电资产",
            string owner = "虚构测试单位",
            string economicAction = "接收",
            decimal quantity = 6m,
            string scope = "6家客户对应的全部资产")
        {
            return new AssessmentObjectFact(
                sourceDocument,
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
