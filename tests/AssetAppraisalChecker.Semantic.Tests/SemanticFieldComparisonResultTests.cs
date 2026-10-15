using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class SemanticFieldComparisonResultTests
    {
        [TestMethod]
        public void Constructor_WithValidValues_CreatesResultAndPreservesFields()
        {
            SemanticFieldComparisonResult result = CreateResult(
                "AssetType",
                "供配电资产",
                "配电设施",
                0.92m,
                0.88m,
                new[] { "报告A正文第3段", "报告B表格1行2列" });

            Assert.AreEqual("AssetType", result.FieldName);
            Assert.AreEqual("供配电资产", result.OriginalValue);
            Assert.AreEqual("配电设施", result.CandidateValue);
            Assert.AreEqual(0.92m, result.SimilarityScore);
            Assert.AreEqual(0.88m, result.Confidence);
            CollectionAssert.AreEqual(
                new[] { "报告A正文第3段", "报告B表格1行2列" },
                new List<string>(result.Evidence));
        }

        [TestMethod]
        public void SimilarityScore_AcceptsInclusiveRangeBoundaries()
        {
            Assert.AreEqual(0m, CreateResult(similarityScore: 0m).SimilarityScore);
            Assert.AreEqual(1m, CreateResult(similarityScore: 1m).SimilarityScore);
        }

        [TestMethod]
        public void SimilarityScore_RejectsValuesOutsideRange()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => CreateResult(similarityScore: -0.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => CreateResult(similarityScore: 1.01m));
        }

        [TestMethod]
        public void Evidence_IsAnImmutableSnapshot()
        {
            List<string> evidence = new List<string> { "原始证据" };
            SemanticFieldComparisonResult result = CreateResult(evidence: evidence);

            evidence[0] = "外部已修改";
            evidence.Add("外部新增项");

            Assert.AreEqual(1, result.Evidence.Count);
            Assert.AreEqual("原始证据", result.Evidence[0]);
            Assert.IsFalse(result.Evidence is IList<string> mutableEvidence
                && !mutableEvidence.IsReadOnly);
        }

        [TestMethod]
        public void Confidence_AcceptsInclusiveRangeBoundaries()
        {
            Assert.AreEqual(0m, CreateResult(confidence: 0m).Confidence);
            Assert.AreEqual(1m, CreateResult(confidence: 1m).Confidence);
        }

        [TestMethod]
        public void Confidence_RejectsValuesOutsideRange()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => CreateResult(confidence: -0.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => CreateResult(confidence: 1.01m));
        }

        [TestMethod]
        public void Constructor_RejectsInvalidRequiredInputs()
        {
            Assert.ThrowsExactly<ArgumentException>(() => CreateResult(fieldName: " "));
            Assert.ThrowsExactly<ArgumentException>(() => CreateResult(originalValue: null));
            Assert.ThrowsExactly<ArgumentException>(() => CreateResult(candidateValue: string.Empty));
            Assert.ThrowsExactly<ArgumentNullException>(
                () => new SemanticFieldComparisonResult(
                    "AssetType",
                    "供配电资产",
                    "配电设施",
                    0.90m,
                    0.85m,
                    null));
            Assert.ThrowsExactly<ArgumentException>(() => CreateResult(evidence: new string[0]));
            Assert.ThrowsExactly<ArgumentException>(() => CreateResult(evidence: new[] { " " }));
        }

        private static SemanticFieldComparisonResult CreateResult(
            string fieldName = "AssetType",
            string originalValue = "供配电资产",
            string candidateValue = "配电设施",
            decimal similarityScore = 0.90m,
            decimal confidence = 0.85m,
            IEnumerable<string> evidence = null)
        {
            return new SemanticFieldComparisonResult(
                fieldName,
                originalValue,
                candidateValue,
                similarityScore,
                confidence,
                evidence ?? new[] { "测试证据" });
        }
    }
}
