using System;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.Application
{
    public static class DefaultFactCheckRegistryFactory
    {
        public static IFactCheckRegistry Create(ISemanticAnalyzer semanticAnalyzer)
        {
            if (semanticAnalyzer == null)
            {
                throw new ArgumentNullException(nameof(semanticAnalyzer));
            }

            return Create(
                new AssessmentObjectFactExtractor(),
                new AssessmentObjectSemanticEnhancer(semanticAnalyzer),
                new AssessmentObjectSemanticAwareComparer(semanticAnalyzer),
                new AssessmentValueFactExtractor(),
                new AssessmentValueComparer(),
                new BenchmarkDateFactExtractor(),
                new BenchmarkDateComparer(),
                new ReportNumberFactExtractor(),
                new ReportNumberComparer(),
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                semanticAnalyzer);
        }

        public static IFactCheckRegistry Create(
            ISemanticAnalyzer semanticAnalyzer,
            ISemanticCandidateRanker semanticCandidateRanker)
        {
            return Create(
                semanticAnalyzer,
                semanticCandidateRanker,
                SemanticCandidateProcessingOptions.Shadow);
        }

        public static IFactCheckRegistry Create(
            ISemanticAnalyzer semanticAnalyzer,
            ISemanticCandidateRanker semanticCandidateRanker,
            SemanticCandidateProcessingOptions semanticProcessingOptions)
        {
            if (semanticAnalyzer == null)
            {
                throw new ArgumentNullException(nameof(semanticAnalyzer));
            }

            if (semanticCandidateRanker == null)
            {
                throw new ArgumentNullException(nameof(semanticCandidateRanker));
            }

            if (semanticProcessingOptions == null)
            {
                throw new ArgumentNullException(nameof(semanticProcessingOptions));
            }

            return Create(
                new AssessmentObjectFactExtractor(),
                new AssessmentObjectSemanticEnhancer(semanticAnalyzer),
                new AssessmentObjectSemanticAwareComparer(semanticAnalyzer),
                new AssessmentValueFactExtractor(),
                new AssessmentValueComparer(),
                new BenchmarkDateFactExtractor(),
                new BenchmarkDateComparer(),
                new ReportNumberFactExtractor(),
                new ReportNumberComparer(),
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer(),
                semanticAnalyzer,
                semanticCandidateRanker,
                semanticProcessingOptions);
        }

        public static IFactCheckRegistry Create(
            AssessmentObjectFactExtractor assessmentObjectFactExtractor,
            AssessmentObjectSemanticEnhancer assessmentObjectSemanticEnhancer,
            AssessmentObjectSemanticAwareComparer assessmentObjectComparer,
            AssessmentValueFactExtractor assessmentValueFactExtractor,
            AssessmentValueComparer assessmentValueComparer)
        {
            return Create(
                assessmentObjectFactExtractor,
                assessmentObjectSemanticEnhancer,
                assessmentObjectComparer,
                assessmentValueFactExtractor,
                assessmentValueComparer,
                new BenchmarkDateFactExtractor(),
                new BenchmarkDateComparer(),
                new ReportNumberFactExtractor(),
                new ReportNumberComparer(),
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer());
        }

        public static IFactCheckRegistry Create(
            AssessmentObjectFactExtractor assessmentObjectFactExtractor,
            AssessmentObjectSemanticEnhancer assessmentObjectSemanticEnhancer,
            AssessmentObjectSemanticAwareComparer assessmentObjectComparer,
            AssessmentValueFactExtractor assessmentValueFactExtractor,
            AssessmentValueComparer assessmentValueComparer,
            BenchmarkDateFactExtractor benchmarkDateFactExtractor,
            BenchmarkDateComparer benchmarkDateComparer)
        {
            return Create(
                assessmentObjectFactExtractor,
                assessmentObjectSemanticEnhancer,
                assessmentObjectComparer,
                assessmentValueFactExtractor,
                assessmentValueComparer,
                benchmarkDateFactExtractor,
                benchmarkDateComparer,
                new ReportNumberFactExtractor(),
                new ReportNumberComparer(),
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer());
        }

        public static IFactCheckRegistry Create(
            AssessmentObjectFactExtractor assessmentObjectFactExtractor,
            AssessmentObjectSemanticEnhancer assessmentObjectSemanticEnhancer,
            AssessmentObjectSemanticAwareComparer assessmentObjectComparer,
            AssessmentValueFactExtractor assessmentValueFactExtractor,
            AssessmentValueComparer assessmentValueComparer,
            BenchmarkDateFactExtractor benchmarkDateFactExtractor,
            BenchmarkDateComparer benchmarkDateComparer,
            ReportNumberFactExtractor reportNumberFactExtractor,
            ReportNumberComparer reportNumberComparer)
        {
            return Create(
                assessmentObjectFactExtractor,
                assessmentObjectSemanticEnhancer,
                assessmentObjectComparer,
                assessmentValueFactExtractor,
                assessmentValueComparer,
                benchmarkDateFactExtractor,
                benchmarkDateComparer,
                reportNumberFactExtractor,
                reportNumberComparer,
                new EvaluationMethodFactExtractor(),
                new EvaluationMethodComparer());
        }

        public static IFactCheckRegistry Create(
            AssessmentObjectFactExtractor assessmentObjectFactExtractor,
            AssessmentObjectSemanticEnhancer assessmentObjectSemanticEnhancer,
            AssessmentObjectSemanticAwareComparer assessmentObjectComparer,
            AssessmentValueFactExtractor assessmentValueFactExtractor,
            AssessmentValueComparer assessmentValueComparer,
            BenchmarkDateFactExtractor benchmarkDateFactExtractor,
            BenchmarkDateComparer benchmarkDateComparer,
            ReportNumberFactExtractor reportNumberFactExtractor,
            ReportNumberComparer reportNumberComparer,
            EvaluationMethodFactExtractor evaluationMethodFactExtractor,
            EvaluationMethodComparer evaluationMethodComparer,
            ISemanticAnalyzer evaluationMethodSemanticAnalyzer = null,
            ISemanticCandidateRanker evaluationMethodCandidateRanker = null,
            SemanticCandidateProcessingOptions semanticProcessingOptions = null)
        {
            return new FactCheckRegistry(
                new IFactChecker[]
                {
                    new AssessmentObjectChecker(
                        assessmentObjectFactExtractor,
                        assessmentObjectSemanticEnhancer,
                        assessmentObjectComparer),
                    new AssessmentValueChecker(
                        assessmentValueFactExtractor,
                        assessmentValueComparer),
                    new BenchmarkDateChecker(
                        benchmarkDateFactExtractor,
                        benchmarkDateComparer),
                    new ReportNumberChecker(
                        reportNumberFactExtractor,
                        reportNumberComparer),
                    new EvaluationMethodChecker(
                        evaluationMethodFactExtractor,
                        evaluationMethodComparer,
                        evaluationMethodSemanticAnalyzer,
                        evaluationMethodCandidateRanker,
                        semanticProcessingOptions
                            ?? SemanticCandidateProcessingOptions.Shadow)
                });
        }
    }
}
