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
    public sealed class BenchmarkDateCheckerTests
    {
        [TestMethod]
        public void EqualDates_ReturnSucceededPassEnvelope()
        {
            BenchmarkDateChecker checker = CreateChecker();

            FactCheckExecutionEnvelope execution = checker.Execute(
                CreateInput("report", "评估基准日：2026年7月31日"),
                CreateInput("explanation", "评估基准日：2026-07-31"));

            Assert.AreEqual(BenchmarkDateChecker.Name, execution.CheckerName);
            Assert.AreEqual(FactType.BenchmarkDate, execution.FactType);
            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                execution.ExecutionStatus);
            Assert.AreEqual(
                ConsistencyStatus.PASS,
                execution.ConsistencyResult.Status);
            Assert.IsTrue(
                execution.Evidence.Any(
                    item => item.IndexOf(
                        "NormalizedDate=2026-07-31",
                        StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void DifferentDates_ReturnSucceededFailEnvelope()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput("report", "评估基准日：2026年7月31日"),
                CreateInput("explanation", "评估基准日：2026.08.01"));

            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                execution.ExecutionStatus);
            Assert.AreEqual(
                ConsistencyStatus.FAIL,
                execution.ConsistencyResult.Status);
        }

        [TestMethod]
        public void MissingDate_ReturnsCompletedWithWarningEnvelope()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput("report", "评估基准日：2026/07/31"),
                CreateInput("explanation", "本说明未列示评估基准日。"));

            Assert.AreEqual(
                FactCheckExecutionStatus.CompletedWithWarning,
                execution.ExecutionStatus);
            Assert.AreEqual(
                ConsistencyStatus.WARNING,
                execution.ConsistencyResult.Status);
            StringAssert.Contains(execution.ConsistencyResult.Reason, "缺少");
        }

        [TestMethod]
        public void BothDocumentsWithoutDate_ReturnNotApplicable()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                CreateInput("report", "本报告未列示相关日期。"),
                CreateInput("explanation", "本说明未列示相关日期。"));

            Assert.AreEqual(
                FactCheckExecutionStatus.NotApplicable,
                execution.ExecutionStatus);
            Assert.IsNull(execution.ConsistencyResult);
            Assert.IsEmpty(execution.Evidence);
        }

        [TestMethod]
        public void DefaultApplicationPipeline_ExecutesBenchmarkDateWithoutSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report", "评估基准日：2026年7月31日"),
                CreateInput("explanation", "评估基准日：2026-07-31"));

            Assert.AreEqual(0, analyzer.CallCount);
            Assert.HasCount(1, result.Results);
            Assert.AreEqual(FactType.BenchmarkDate, result.Results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            Assert.HasCount(5, result.Executions);
            FactCheckExecutionEnvelope dateExecution = result.Executions.Single(
                execution => execution.FactType == FactType.BenchmarkDate);
            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                dateExecution.ExecutionStatus);
        }

        private static BenchmarkDateChecker CreateChecker()
        {
            return new BenchmarkDateChecker(
                new BenchmarkDateFactExtractor(),
                new BenchmarkDateComparer());
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
                new DocumentNearbyContext(string.Empty, text, string.Empty));
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
                    "BenchmarkDate 检查禁止调用 Semantic。");
            }
        }
    }
}
