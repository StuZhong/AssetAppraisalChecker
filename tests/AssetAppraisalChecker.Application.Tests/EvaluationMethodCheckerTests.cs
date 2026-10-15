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
    public sealed class EvaluationMethodCheckerTests
    {
        [TestMethod]
        public void Execute_MultipleSubjects_ReturnsSingleExecutionWithMultipleResults()
        {
            EvaluationMethodChecker checker = CreateChecker();

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用成本法；机器设备采用市场法。"),
                Input("explanation", "房屋建筑物采用成本法；机器设备采用收益法。"));

            Assert.AreEqual(FactCheckExecutionStatus.Succeeded, execution.ExecutionStatus);
            Assert.HasCount(2, execution.Results);
            Assert.AreEqual(ConsistencyStatus.PASS, execution.Results.Single(item => item.Identity.SubjectKey == "subject/v1:building").Status);
            Assert.AreEqual(ConsistencyStatus.FAIL, execution.Results.Single(item => item.Identity.SubjectKey == "subject/v1:machinery-equipment").Status);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = execution.ConsistencyResult);
        }

        [TestMethod]
        public void Execute_CostVersusReplacement_ReturnsCompletedWithWarning()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                Input("report", "房屋建筑物采用成本法。"),
                Input("explanation", "房屋建筑物采用重置成本法。"));

            Assert.AreEqual(FactCheckExecutionStatus.CompletedWithWarning, execution.ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
        }

        [TestMethod]
        public void Execute_AbsentInBothDocuments_ReturnsNotApplicable()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                Input("report", "合成测试文档。"),
                Input("explanation", "合成测试说明。"));

            Assert.AreEqual(FactCheckExecutionStatus.NotApplicable, execution.ExecutionStatus);
            Assert.HasCount(0, execution.Results);
            Assert.IsFalse(string.IsNullOrWhiteSpace(execution.NoResultReason));
        }

        [TestMethod]
        public void Execute_NonStandardDescription_FailsClosedWithWarning()
        {
            FactCheckExecutionEnvelope execution = CreateChecker().Execute(
                Input("report", "房屋建筑物按现行重建成本测算。"),
                Input("explanation", "房屋建筑物采用重置成本法。"));

            Assert.AreEqual(FactCheckExecutionStatus.CompletedWithWarning, execution.ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
        }

        [TestMethod]
        public void Execute_UnresolvedSemanticFailure_ReturnsFailedWithCompletedWarningResult()
        {
            var analyzer = new ThrowingSemanticAnalyzer();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                analyzer);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用重置成本法。"),
                Input("explanation", "房屋建筑物按现行重建成本测算。"));

            Assert.AreEqual(1, analyzer.CallCount);
            Assert.AreEqual(FactCheckExecutionStatus.Failed, execution.ExecutionStatus);
            Assert.HasCount(1, execution.Results);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
            StringAssert.Contains(execution.ErrorInformation, "受控失败");
        }

        private static EvaluationMethodChecker CreateChecker()
        {
            return new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer());
        }

        private static V2DocumentAnalysisInput Input(string source, string text)
        {
            var location = new DocumentLocation(DocumentPart.MainBody, 0, null, null, null, new[] { "评估方法" });
            var element = new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                0,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
            return new V2DocumentAnalysisInput(
                DocumentReadResult.Succeeded(new[] { element }, 1, 0),
                source);
        }

        private sealed class ThrowingSemanticAnalyzer : ISemanticAnalyzer
        {
            public int CallCount { get; private set; }

            public SemanticAnalysisResult Analyze(string textContent)
            {
                CallCount++;
                throw new InvalidOperationException("受控失败");
            }
        }
    }
}
