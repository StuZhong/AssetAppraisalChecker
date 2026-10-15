using System;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class V2AssessmentValueDocumentAnalysisPipelineTests
    {
        [TestMethod]
        public void IdenticalAssessmentValues_ReturnPassWithoutSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report", "本项目评估价值为998.22万元。"),
                CreateInput("explanation", "本项目评估价值为998.22万元。"));

            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            Assert.HasCount(1, result.Results);
            Assert.AreEqual(FactType.AssessmentValue, result.Results[0].FactType);
            Assert.AreEqual(0, analyzer.CallCount);
        }

        [TestMethod]
        public void EquivalentUnits_ReturnPassWithoutSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report", "本项目评估价值为998.22万元。"),
                CreateInput("explanation", "本项目评估价值为9,982,200元。"));

            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            StringAssert.Contains(result.Results[0].Reason, "精确相等");
            Assert.AreEqual(0, analyzer.CallCount);
        }

        [TestMethod]
        public void ConflictingAssessmentValues_ReturnFailWithoutSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report", "本项目评估价值为998.22万元。"),
                CreateInput("explanation", "本项目评估价值为1000万元。"));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.StatusSummary);
            StringAssert.Contains(result.Results[0].Reason, "17800");
            Assert.AreEqual(0, analyzer.CallCount);
        }

        [TestMethod]
        public void MissingAssessmentValue_ReturnsWarningWithoutSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report", "本项目评估价值为998.22万元。"),
                CreateInput("explanation", "本说明未列示评估价值。"));

            Assert.AreEqual(ConsistencyStatus.WARNING, result.StatusSummary);
            Assert.AreEqual(FactType.AssessmentValue, result.Results[0].FactType);
            StringAssert.Contains(result.Results[0].Reason, "缺少");
            Assert.AreEqual(0, analyzer.CallCount);
        }

        [TestMethod]
        public void Analyze_ExecutesCompleteAssessmentValueChainAndPreservesEvidence()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report", "本项目评估价值为998.22万元。"),
                CreateInput("explanation", "本项目评估价值为9,982,200元。"));

            Assert.HasCount(1, result.Results);
            Assert.AreEqual(FactType.AssessmentValue, result.Results[0].FactType);
            Assert.IsTrue(ContainsEvidence(result, "report | 998.22万元"));
            Assert.IsTrue(ContainsEvidence(result, "explanation | 9,982,200元"));
            Assert.IsTrue(ContainsEvidence(result, "NormalizedAmountYuan=9982200"));
            Assert.AreEqual(0, analyzer.CallCount);
        }

        private static V2DocumentAnalysisInput CreateInput(
            string sourceDocument,
            string text)
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                0,
                null,
                null,
                null,
                new[] { "评估结论" });
            var element = new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                0,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[] { element },
                1,
                0);

            return new V2DocumentAnalysisInput(document, sourceDocument);
        }

        private static bool ContainsEvidence(
            V2AnalysisResult result,
            string expected)
        {
            foreach (string evidence in result.Evidence)
            {
                if (evidence.IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RecordingSemanticAnalyzer : ISemanticAnalyzer
        {
            public int CallCount { get; private set; }

            public SemanticAnalysisResult Analyze(string textContent)
            {
                CallCount++;
                return new SemanticAnalysisResult(
                    "unexpected-assessment-value-semantic-call",
                    null,
                    0m,
                    0m,
                    new[] { "Semantic 不应参与金额比较。" });
            }
        }
    }
}
