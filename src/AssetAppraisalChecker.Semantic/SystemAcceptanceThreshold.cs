using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SystemAcceptanceThreshold
    {
        public SystemAcceptanceThreshold(
            string policyVersion,
            decimal minimumModelConfidence,
            decimal? minimumSimilarityScore,
            decimal ambiguityThreshold,
            bool isProductionApproved)
        {
            SemanticSourceLocation.RequireText(policyVersion, nameof(policyVersion));
            ValidateScore(minimumModelConfidence, nameof(minimumModelConfidence));
            if (minimumSimilarityScore.HasValue)
            {
                ValidateScore(minimumSimilarityScore.Value, nameof(minimumSimilarityScore));
            }

            ValidateScore(ambiguityThreshold, nameof(ambiguityThreshold));
            PolicyVersion = policyVersion;
            MinimumModelConfidence = minimumModelConfidence;
            MinimumSimilarityScore = minimumSimilarityScore;
            AmbiguityThreshold = ambiguityThreshold;
            IsProductionApproved = isProductionApproved;
        }

        public string PolicyVersion { get; }

        public decimal MinimumModelConfidence { get; }

        public decimal? MinimumSimilarityScore { get; }

        public decimal AmbiguityThreshold { get; }

        public bool IsProductionApproved { get; }

        private static void ValidateScore(decimal value, string parameterName)
        {
            if (value < 0m || value > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "阈值必须在 0 到 1 之间。");
            }
        }
    }
}
