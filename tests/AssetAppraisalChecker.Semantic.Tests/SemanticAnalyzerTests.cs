using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class SemanticAnalyzerTests
    {
        [TestMethod]
        public void SemanticAnalyzerInterface_AnalyzesConfiguredText()
        {
            ISemanticAnalyzer analyzer = new MockSemanticAnalyzer();

            SemanticAnalysisResult result = analyzer.Analyze(MockSemanticAnalyzer.DefaultInputText);

            Assert.AreEqual("mock-semantic-input-001", result.InputIdentifier);
            Assert.AreEqual("mock-semantic-representation-v1", result.SemanticRepresentation);
            Assert.AreEqual(0.80m, result.SimilarityScore);
            Assert.AreEqual(0.95m, result.Confidence);
            Assert.AreEqual(1, result.Evidence.Count);
            Assert.AreEqual(MockSemanticAnalyzer.DefaultInputText, result.Evidence[0]);
        }

        [TestMethod]
        public void MockSemanticAnalyzer_ReturnsStableResultForRepeatedInput()
        {
            ISemanticAnalyzer analyzer = new MockSemanticAnalyzer();

            SemanticAnalysisResult first = analyzer.Analyze(MockSemanticAnalyzer.DefaultInputText);
            SemanticAnalysisResult second = analyzer.Analyze(MockSemanticAnalyzer.DefaultInputText);

            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void MockSemanticAnalyzer_RejectsInputOtherThanConfiguredText()
        {
            ISemanticAnalyzer analyzer = new MockSemanticAnalyzer();

            Assert.ThrowsExactly<ArgumentException>(() => analyzer.Analyze("其他文本"));
            Assert.ThrowsExactly<ArgumentNullException>(() => analyzer.Analyze(null));
        }

        [TestMethod]
        public void SimilarityScore_AcceptsInclusiveRangeBoundaries()
        {
            Assert.AreEqual(0m, CreateResult(0m, 0.50m).SimilarityScore);
            Assert.AreEqual(1m, CreateResult(1m, 0.50m).SimilarityScore);
        }

        [TestMethod]
        public void SimilarityScore_RejectsValuesOutsideRange()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateResult(-0.01m, 0.50m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateResult(1.01m, 0.50m));
        }

        [TestMethod]
        public void Confidence_AcceptsInclusiveRangeBoundaries()
        {
            Assert.AreEqual(0m, CreateResult(0.50m, 0m).Confidence);
            Assert.AreEqual(1m, CreateResult(0.50m, 1m).Confidence);
        }

        [TestMethod]
        public void Confidence_RejectsValuesOutsideRange()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateResult(0.50m, -0.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateResult(0.50m, 1.01m));
        }

        [TestMethod]
        public void SemanticRepresentation_CanBeNull()
        {
            SemanticAnalysisResult result = new SemanticAnalysisResult(
                "input-001",
                null,
                0.50m,
                0.50m,
                new string[0]);

            Assert.IsNull(result.SemanticRepresentation);
        }

        [TestMethod]
        public void Evidence_IsAnImmutableSnapshot()
        {
            List<string> evidence = new List<string> { "原始证据" };
            SemanticAnalysisResult result = new SemanticAnalysisResult(
                "input-001",
                null,
                0.50m,
                0.50m,
                evidence);

            evidence[0] = "已修改证据";

            Assert.AreEqual("原始证据", result.Evidence[0]);
        }

        private static SemanticAnalysisResult CreateResult(
            decimal similarityScore,
            decimal confidence)
        {
            return new SemanticAnalysisResult(
                "input-001",
                null,
                similarityScore,
                confidence,
                new[] { "测试证据" });
        }
    }
}
