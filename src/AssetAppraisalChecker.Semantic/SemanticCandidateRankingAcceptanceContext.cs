using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateRankingAcceptanceContext
    {
        public SemanticCandidateRankingAcceptanceContext(
            bool requiresSubject,
            string expectedSubjectKey,
            bool hasNegation,
            bool hasHardRuleConflict,
            bool evidenceSufficient)
        {
            if (requiresSubject && string.IsNullOrWhiteSpace(expectedSubjectKey))
            {
                throw new ArgumentException(
                    "Subject-bound ranking 必须提供 ExpectedSubjectKey。",
                    nameof(expectedSubjectKey));
            }

            if (!string.IsNullOrWhiteSpace(expectedSubjectKey))
            {
                SemanticSourceLocation.RequireKey(
                    expectedSubjectKey,
                    nameof(expectedSubjectKey));
            }

            RequiresSubject = requiresSubject;
            ExpectedSubjectKey = expectedSubjectKey ?? string.Empty;
            HasNegation = hasNegation;
            HasHardRuleConflict = hasHardRuleConflict;
            EvidenceSufficient = evidenceSufficient;
        }

        public bool RequiresSubject { get; }

        public string ExpectedSubjectKey { get; }

        public bool HasNegation { get; }

        public bool HasHardRuleConflict { get; }

        public bool EvidenceSufficient { get; }
    }
}
