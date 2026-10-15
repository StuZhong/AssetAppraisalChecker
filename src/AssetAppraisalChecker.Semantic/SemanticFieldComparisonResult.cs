using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticFieldComparisonResult
    {
        public SemanticFieldComparisonResult(
            string fieldName,
            string originalValue,
            string candidateValue,
            decimal similarityScore,
            decimal confidence,
            IEnumerable<string> evidence)
        {
            ValidateRequiredText(fieldName, nameof(fieldName), "字段名称不能为空。");
            ValidateRequiredText(originalValue, nameof(originalValue), "原始值不能为空。");
            ValidateRequiredText(candidateValue, nameof(candidateValue), "候选值不能为空。");
            ValidateUnitInterval(similarityScore, nameof(similarityScore));
            ValidateUnitInterval(confidence, nameof(confidence));

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<string> evidenceItems = evidence.ToList();
            if (evidenceItems.Count == 0)
            {
                throw new ArgumentException("语义字段比较证据不能为空。", nameof(evidence));
            }

            if (evidenceItems.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("语义字段比较证据不能包含空白项。", nameof(evidence));
            }

            FieldName = fieldName;
            OriginalValue = originalValue;
            CandidateValue = candidateValue;
            SimilarityScore = similarityScore;
            Confidence = confidence;
            Evidence = new ReadOnlyCollection<string>(evidenceItems);
        }

        public string FieldName { get; }

        public string OriginalValue { get; }

        public string CandidateValue { get; }

        public decimal SimilarityScore { get; }

        public decimal Confidence { get; }

        public IReadOnlyList<string> Evidence { get; }

        private static void ValidateRequiredText(
            string value,
            string parameterName,
            string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(message, parameterName);
            }
        }

        private static void ValidateUnitInterval(decimal value, string parameterName)
        {
            if (value < 0m || value > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "数值必须在 0 到 1 之间。");
            }
        }
    }
}
