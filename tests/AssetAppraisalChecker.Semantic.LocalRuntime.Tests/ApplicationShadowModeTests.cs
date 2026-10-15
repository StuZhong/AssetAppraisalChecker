using System;
using System.Linq;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.LocalRuntime.Tests
{
    [TestClass]
    public sealed class ApplicationShadowModeTests
    {
        [TestMethod]
        public void Execute_ShadowRankingFailurePreservesBusinessResult()
        {
            var ranker = new ThrowingCandidateRanker();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                ranker);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用重置成本法。"),
                Input("explanation", "房屋建筑物按现行重建成本测算。"));

            Assert.AreEqual(1, ranker.CallCount);
            Assert.AreEqual(
                FactCheckExecutionStatus.CompletedWithWarning,
                execution.ExecutionStatus);
            Assert.HasCount(1, execution.Results);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
            Assert.IsTrue(execution.ExecutionEvidence.Any(item =>
                item.Contains("SemanticShadow | Status=Failed")));
        }

        [TestMethod]
        public void Execute_ShadowRankingSuccessAddsEvidenceWithoutChangingBusinessResult()
        {
            var ranker = new SuccessfulCandidateRanker();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                ranker);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用重置成本法。"),
                Input("explanation", "房屋建筑物按现行重建成本测算。"));

            Assert.AreEqual(1, ranker.CallCount);
            Assert.AreEqual(
                FactCheckExecutionStatus.CompletedWithWarning,
                execution.ExecutionStatus);
            Assert.HasCount(1, execution.Results);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
            Assert.IsTrue(execution.ExecutionEvidence.Any(item =>
                item.Contains("SemanticShadow | RequestId=")
                && item.Contains(" | Status=Ranked")));
            Assert.IsTrue(execution.ExecutionEvidence.Any(item =>
                item.Contains("FactPromotionAllowed=false")));
            Assert.IsFalse(execution.Results[0].Evidence.Any(item =>
                item.Contains("SemanticShadow")));
            Assert.IsTrue(ranker.LastShadowOnly);
        }

        [TestMethod]
        public void Execute_ActiveRankingIsConsumedAsEvidenceWithoutChangingWarning()
        {
            var ranker = new SuccessfulCandidateRanker();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                ranker,
                SemanticCandidateProcessingOptions.Active);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用重置成本法。"),
                Input("explanation", "房屋建筑物按现行重建成本测算。"));

            Assert.AreEqual(FactCheckExecutionStatus.CompletedWithWarning, execution.ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
            Assert.IsFalse(ranker.LastShadowOnly);
            Assert.IsTrue(execution.Results[0].Evidence.Any(item =>
                item.Contains("Decision=AcceptedEvidence")));
            Assert.IsTrue(execution.Results[0].Evidence.Any(item =>
                item.Contains("FactPromotionAllowed=false")));
        }

        [TestMethod]
        public void Execute_ActiveRuntimeFailureFailsExecutionWithoutBusinessFail()
        {
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                new ThrowingCandidateRanker(),
                SemanticCandidateProcessingOptions.Active);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用重置成本法。"),
                Input("explanation", "房屋建筑物按现行重建成本测算。"));

            Assert.AreEqual(FactCheckExecutionStatus.Failed, execution.ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
            Assert.IsFalse(execution.Results.Any(result =>
                result.Status == ConsistencyStatus.FAIL));
            Assert.Contains("failed closed", execution.ErrorInformation);
            Assert.IsTrue(execution.ExecutionEvidence.Any(item =>
                item.Contains("SemanticActive | Status=Failed")));
        }

        [TestMethod]
        public void Execute_AmbiguousSubjectAbstainsWithoutStrongSelection()
        {
            var ranker = new SuccessfulCandidateRanker();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                ranker,
                SemanticCandidateProcessingOptions.Active);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物和机器设备采用成本法。"),
                Input("explanation", "房屋建筑物和机器设备采用成本法。"));

            Assert.AreEqual(ConsistencyStatus.WARNING, execution.Results[0].Status);
            Assert.IsTrue(execution.Results[0].Evidence.Any(item =>
                item.Contains("Decision=Abstained")));
            Assert.IsFalse(execution.Results[0].Evidence.Any(item =>
                item.Contains("Decision=AcceptedEvidence")));
        }

        [TestMethod]
        public void Execute_ExplicitNegationIsProtectedWithoutRanking()
        {
            var ranker = new SuccessfulCandidateRanker();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                ranker,
                SemanticCandidateProcessingOptions.Active);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用成本法。"),
                Input("explanation", "房屋建筑物不采用成本法。"));

            Assert.AreEqual(ConsistencyStatus.FAIL, execution.Results[0].Status);
            Assert.AreEqual(0, ranker.CallCount);
        }

        [TestMethod]
        public void Execute_HardMethodConflictCannotBeOverridden()
        {
            var ranker = new SuccessfulCandidateRanker();
            var checker = new EvaluationMethodChecker(
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                null,
                ranker,
                SemanticCandidateProcessingOptions.Active);

            FactCheckExecutionEnvelope execution = checker.Execute(
                Input("report", "房屋建筑物采用市场法。"),
                Input("explanation", "房屋建筑物采用收益法。"));

            Assert.AreEqual(ConsistencyStatus.FAIL, execution.Results[0].Status);
            Assert.AreEqual(0, ranker.CallCount);
        }

        private static V2DocumentAnalysisInput Input(string source, string text)
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                0,
                null,
                null,
                null,
                new[] { "评估方法" });
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

        private sealed class ThrowingCandidateRanker : ISemanticCandidateRanker
        {
            public int CallCount { get; private set; }

            public SemanticCandidateRankingResult Rank(
                SemanticCandidateRequest request,
                SemanticCandidateSelectionContext selectionContext)
            {
                CallCount++;
                throw new InvalidOperationException("shadow-only failure");
            }
        }

        private sealed class SuccessfulCandidateRanker : ISemanticCandidateRanker
        {
            private const string Hash =
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            public int CallCount { get; private set; }

            public bool LastShadowOnly { get; private set; }

            public SemanticCandidateRankingResult Rank(
                SemanticCandidateRequest request,
                SemanticCandidateSelectionContext selectionContext)
            {
                CallCount++;
                LastShadowOnly = selectionContext.ShadowOnly;
                string candidate = request.AllowedNormalizedCandidates[0];
                var identity = new SemanticCandidateIdentity(
                    request.CandidateSetIdentity,
                    candidate);
                var item = new SemanticCandidateRankingItem(
                    identity,
                    candidate,
                    0.75,
                    1,
                    2.0,
                    1);
                return new SemanticCandidateRankingResult(
                    request.RequestId,
                    request.CandidateSetIdentity,
                    selectionContext,
                    SemanticCandidateRankingStatus.Ranked,
                    new[] { item },
                    "test-embedding",
                    "revision",
                    Hash,
                    "test-reranker",
                    "revision",
                    Hash,
                    "RankedForShadowEvidence");
            }
        }
    }
}
