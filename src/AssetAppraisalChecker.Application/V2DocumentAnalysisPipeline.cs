using System;
using System.Collections.Generic;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.Application
{
    public sealed class V2DocumentAnalysisPipeline : IV2DocumentAnalysisPipeline
    {
        private const string PipelineCheckerName = "V2DocumentAnalysisPipeline";

        private readonly IFactCheckRegistry _registry;

        public V2DocumentAnalysisPipeline(ISemanticAnalyzer semanticAnalyzer)
            : this(DefaultFactCheckRegistryFactory.Create(semanticAnalyzer))
        {
        }

        public V2DocumentAnalysisPipeline(
            ISemanticAnalyzer semanticAnalyzer,
            ISemanticCandidateRanker semanticCandidateRanker)
            : this(DefaultFactCheckRegistryFactory.Create(
                semanticAnalyzer,
                semanticCandidateRanker))
        {
        }

        public V2DocumentAnalysisPipeline(
            ISemanticAnalyzer semanticAnalyzer,
            ISemanticCandidateRanker semanticCandidateRanker,
            SemanticCandidateProcessingOptions semanticProcessingOptions)
            : this(DefaultFactCheckRegistryFactory.Create(
                semanticAnalyzer,
                semanticCandidateRanker,
                semanticProcessingOptions))
        {
        }

        public V2DocumentAnalysisPipeline(
            AssessmentObjectFactExtractor factExtractor,
            AssessmentObjectSemanticEnhancer semanticEnhancer,
            AssessmentObjectSemanticAwareComparer comparer)
            : this(
                DefaultFactCheckRegistryFactory.Create(
                    factExtractor,
                    semanticEnhancer,
                    comparer,
                    new AssessmentValueFactExtractor(),
                    new AssessmentValueComparer()))
        {
        }

        public V2DocumentAnalysisPipeline(
            AssessmentObjectFactExtractor factExtractor,
            AssessmentObjectSemanticEnhancer semanticEnhancer,
            AssessmentObjectSemanticAwareComparer comparer,
            AssessmentValueFactExtractor assessmentValueFactExtractor,
            AssessmentValueComparer assessmentValueComparer)
            : this(
                DefaultFactCheckRegistryFactory.Create(
                    factExtractor,
                    semanticEnhancer,
                    comparer,
                    assessmentValueFactExtractor,
                    assessmentValueComparer))
        {
        }

        public V2DocumentAnalysisPipeline(IFactCheckRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public V2AnalysisResult Analyze(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            if (firstDocument == null)
            {
                throw new ArgumentNullException(nameof(firstDocument));
            }

            if (secondDocument == null)
            {
                throw new ArgumentNullException(nameof(secondDocument));
            }

            if (!firstDocument.DocumentReadResult.IsSuccess ||
                !secondDocument.DocumentReadResult.IsSuccess)
            {
                ConsistencyResult warning = CreatePipelineWarningResult(
                    "至少一个文档读取失败，未执行评估对象比较。",
                    firstDocument,
                    secondDocument);
                FactCheckExecutionEnvelope execution =
                    FactCheckExecutionEnvelope.Failed(
                        PipelineCheckerName,
                        warning,
                        "至少一个 DocumentReadResult 不是成功状态。");

                return new V2AnalysisResult(
                    new[] { warning },
                    new[] { execution });
            }

            var results = new List<ConsistencyResult>();
            var executions = new List<FactCheckExecutionEnvelope>();

            foreach (IFactChecker checker in _registry.Checkers)
            {
                FactCheckExecutionEnvelope execution = ExecuteSafely(
                    checker,
                    firstDocument,
                    secondDocument);
                executions.Add(execution);

                foreach (ConsistencyResult result in execution.Results)
                {
                    results.Add(result);
                }
            }

            if (results.Count == 0
                && !executions.Exists(execution =>
                    execution.ExecutionStatus == FactCheckExecutionStatus.Failed))
            {
                ConsistencyResult warning = CreatePipelineWarningResult(
                    "两个文档均未提取到当前支持的 V2 事实，无法执行一致性比较。",
                    firstDocument,
                    secondDocument);
                results.Add(warning);
            }

            return new V2AnalysisResult(results, executions);
        }

        private static FactCheckExecutionEnvelope ExecuteSafely(
            IFactChecker checker,
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            try
            {
                FactCheckExecutionEnvelope execution = checker.Execute(
                    firstDocument,
                    secondDocument);
                if (execution == null)
                {
                    throw new InvalidOperationException(
                        "FactCheck 返回了 null Execution Envelope。");
                }

                if (!string.Equals(
                        execution.CheckerName,
                        checker.CheckerName,
                        StringComparison.Ordinal) ||
                    execution.FactType != checker.FactType)
                {
                    throw new InvalidOperationException(
                        "FactCheck 返回的身份与 Registry 注册信息不一致。");
                }

                return execution;
            }
            catch (Exception exception)
            {
                return FactCheckExecutionEnvelope.Failed(
                    checker.CheckerName,
                    checker.FactType,
                    new ConsistencyResult[0],
                    exception.Message,
                    CreateExecutionFailureEvidence(
                        firstDocument,
                        secondDocument));
            }
        }

        private static IEnumerable<string> CreateExecutionFailureEvidence(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            return new[]
            {
                string.Concat(
                    firstDocument.SourceDocument,
                    " | DocumentStatus=",
                    firstDocument.DocumentReadResult.Status),
                string.Concat(
                    secondDocument.SourceDocument,
                    " | DocumentStatus=",
                    secondDocument.DocumentReadResult.Status)
            };
        }

        private static ConsistencyResult CreatePipelineWarningResult(
            string reason,
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            return new ConsistencyResult(
                FactType.AssessmentObject,
                ConsistencyStatus.WARNING,
                reason,
                new[]
                {
                    string.Concat(
                        firstDocument.SourceDocument,
                        " | DocumentStatus=",
                        firstDocument.DocumentReadResult.Status),
                    string.Concat(
                        secondDocument.SourceDocument,
                        " | DocumentStatus=",
                        secondDocument.DocumentReadResult.Status)
                });
        }
    }
}
