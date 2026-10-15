using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticAnalysisResult
    {
        public SemanticAnalysisResult(
            string inputIdentifier,
            string semanticRepresentation,
            decimal similarityScore,
            decimal confidence,
            IEnumerable<string> evidence)
        {
            if (string.IsNullOrWhiteSpace(inputIdentifier))
            {
                throw new ArgumentException("语义分析输入标识不能为空。", nameof(inputIdentifier));
            }

            ValidateUnitInterval(similarityScore, nameof(similarityScore));
            ValidateUnitInterval(confidence, nameof(confidence));

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<string> evidenceItems = evidence.ToList();
            if (evidenceItems.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("语义分析证据不能包含空白项。", nameof(evidence));
            }

            InputIdentifier = inputIdentifier;
            SemanticRepresentation = semanticRepresentation;
            SimilarityScore = similarityScore;
            Confidence = confidence;
            Evidence = new ReadOnlyCollection<string>(evidenceItems);
        }

        public string InputIdentifier { get; }

        public string SemanticRepresentation { get; }

        public decimal SimilarityScore { get; }

        public decimal Confidence { get; }

        public IReadOnlyList<string> Evidence { get; }

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
