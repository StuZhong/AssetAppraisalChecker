using System;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class ReportNumberCheckerTests
    {
        [TestMethod]
        public void EquivalentFormatting_ReturnSucceededPassEnvelope()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput(
                    "report",
                    "报告编号：云合成评报字（2026）第001号"),
                CreateInput(
                    "explanation",
                    "报告编号：云合成评报字[2026]第001号"));

            Assert.AreEqual(ReportNumberChecker.Name, execution.CheckerName);
            Assert.AreEqual(FactType.ReportNumber, execution.FactType);
            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                execution.ExecutionStatus);
            Assert.AreEqual(
                ConsistencyStatus.PASS,
                execution.ConsistencyResult.Status);
            Assert.IsTrue(execution.Evidence.Any(
                item => item.IndexOf(
                    "NormalizedNumber=云合成评报字〔2026〕第001号",
                    StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void DifferentNumbers_ReturnSucceededFailEnvelope()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput(
                    "report",
                    "报告编号：云合成评报字〔2026〕第001号"),
                CreateInput(
                    "explanation",
                    "报告编号：云合成评报字〔2026〕第002号"));

            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                execution.ExecutionStatus);
            Assert.AreEqual(
                ConsistencyStatus.FAIL,
                execution.ConsistencyResult.Status);
        }

        [TestMethod]
        public void MissingNumber_ReturnCompletedWithWarningEnvelope()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput(
                    "report",
                    "报告编号：云合成评报字〔2026〕第001号"),
                CreateInput(
                    "explanation",
                    "本合成说明仅描述评估范围和方法。"));

            Assert.AreEqual(
                FactCheckExecutionStatus.CompletedWithWarning,
                execution.ExecutionStatus);
            Assert.AreEqual(
                ConsistencyStatus.WARNING,
                execution.ConsistencyResult.Status);
            StringAssert.Contains(execution.ConsistencyResult.Reason, "缺少");
        }

        [TestMethod]
        public void BothDocumentsWithoutNumber_ReturnNotApplicable()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput("report", "本报告仅包含合成测试文本。"),
                CreateInput("explanation", "本说明仅包含合成测试文本。"));

            Assert.AreEqual(
                FactCheckExecutionStatus.NotApplicable,
                execution.ExecutionStatus);
            Assert.IsNull(execution.ConsistencyResult);
            Assert.IsEmpty(execution.Evidence);
        }

        [TestMethod]
        public void DefaultApplicationPipeline_ExecutesReportNumberWithoutSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput(
                    "report",
                    "报告编号：云合成评报字（2026）第001号"),
                CreateInput(
                    "explanation",
                    "报告编号：云合成评报字[2026]第001号"));

            Assert.AreEqual(0, analyzer.CallCount);
            Assert.HasCount(1, result.Results);
            Assert.AreEqual(FactType.ReportNumber, result.Results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            Assert.HasCount(5, result.Executions);
            FactCheckExecutionEnvelope numberExecution =
                result.Executions.Single(
                    execution => execution.FactType == FactType.ReportNumber);
            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                numberExecution.ExecutionStatus);
        }

        private static ReportNumberChecker CreateChecker()
        {
            return new ReportNumberChecker(
                new ReportNumberFactExtractor(),
                new ReportNumberComparer());
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
                new[] { "基本事项" });
            var element = new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                0,
                string.Empty,
                new DocumentNearbyContext(
                    string.Empty,
                    text,
                    string.Empty));
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[] { element },
                1,
                0);

            return new V2DocumentAnalysisInput(document, sourceDocument);
        }

        private sealed class RecordingSemanticAnalyzer : ISemanticAnalyzer
        {
            public int CallCount { get; private set; }

            public SemanticAnalysisResult Analyze(string textContent)
            {
                CallCount++;
                throw new InvalidOperationException(
                    "ReportNumber 检查禁止调用 Semantic。");
            }
        }
    }
}
