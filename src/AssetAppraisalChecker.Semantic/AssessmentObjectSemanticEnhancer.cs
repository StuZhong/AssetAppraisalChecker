using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class AssessmentObjectSemanticEnhancer
    {
        private readonly ISemanticAnalyzer _semanticAnalyzer;

        public AssessmentObjectSemanticEnhancer(ISemanticAnalyzer semanticAnalyzer)
        {
            _semanticAnalyzer = semanticAnalyzer ??
                throw new ArgumentNullException(nameof(semanticAnalyzer));
        }

        public IReadOnlyList<SemanticFieldComparisonResult> Enhance(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact)
        {
            if (originalFact == null)
            {
                throw new ArgumentNullException(nameof(originalFact));
            }

            if (candidateFact == null)
            {
                throw new ArgumentNullException(nameof(candidateFact));
            }

            List<SemanticFieldComparisonResult> results =
                new List<SemanticFieldComparisonResult>();

            AddComparison(
                results,
                "AssetType",
                originalFact.AssetType,
                candidateFact.AssetType);
            AddComparison(
                results,
                "EconomicAction",
                originalFact.EconomicAction,
                candidateFact.EconomicAction);
            AddComparison(
                results,
                "Scope",
                originalFact.Scope,
                candidateFact.Scope);

            return new ReadOnlyCollection<SemanticFieldComparisonResult>(results);
        }

        private void AddComparison(
            ICollection<SemanticFieldComparisonResult> results,
            string fieldName,
            string originalValue,
            string candidateValue)
        {
            if (string.IsNullOrWhiteSpace(originalValue) ||
                string.IsNullOrWhiteSpace(candidateValue) ||
                string.Equals(originalValue, candidateValue, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                SemanticAnalysisResult analysisResult = _semanticAnalyzer.Analyze(
                    BuildSemanticInput(fieldName, originalValue, candidateValue));

                if (analysisResult == null)
                {
                    throw new InvalidOperationException("Semantic 分析器未返回结果。");
                }

                results.Add(new SemanticFieldComparisonResult(
                    fieldName,
                    originalValue,
                    candidateValue,
                    analysisResult.SimilarityScore,
                    analysisResult.Confidence,
                    analysisResult.Evidence));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    string.Concat("字段 ", fieldName, " 的 Semantic 分析失败。"),
                    exception);
            }
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
    }
}
