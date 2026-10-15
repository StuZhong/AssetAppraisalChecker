using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateAcceptanceContext
    {
        public SemanticCandidateAcceptanceContext(
            SystemAcceptanceThreshold threshold,
            bool requiresSubject,
            string expectedSubjectKey,
            bool hasNegation,
            bool hasHardRuleConflict,
            bool evidenceSufficient,
            bool similarityScoreRequired)
        {
            Threshold = threshold ?? throw new ArgumentNullException(nameof(threshold));
            if (requiresSubject && string.IsNullOrWhiteSpace(expectedSubjectKey))
            {
                throw new ArgumentException(
                    "Subject-bound Candidate 必须提供 ExpectedSubjectKey。",
                    nameof(expectedSubjectKey));
            }

            if (!string.IsNullOrWhiteSpace(expectedSubjectKey))
            {
                SemanticSourceLocation.RequireKey(
                    expectedSubjectKey,
                    nameof(expectedSubjectKey));
            }

            if (similarityScoreRequired && !threshold.MinimumSimilarityScore.HasValue)
            {
                throw new ArgumentException(
                    "要求 SimilarityScore 时必须配置独立 Similarity Threshold。",
                    nameof(threshold));
            }

            RequiresSubject = requiresSubject;
            ExpectedSubjectKey = expectedSubjectKey ?? string.Empty;
            HasNegation = hasNegation;
            HasHardRuleConflict = hasHardRuleConflict;
            EvidenceSufficient = evidenceSufficient;
            SimilarityScoreRequired = similarityScoreRequired;
        }

        public SystemAcceptanceThreshold Threshold { get; }

        public bool RequiresSubject { get; }

        public string ExpectedSubjectKey { get; }

        public bool HasNegation { get; }

        public bool HasHardRuleConflict { get; }

        public bool EvidenceSufficient { get; }

        public bool SimilarityScoreRequired { get; }
    }
}
