using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateAcceptanceDecision
    {
        public SemanticCandidateAcceptanceDecision(
            SemanticCandidateAcceptanceDecisionKind decision,
            SemanticCandidateAcceptanceReasonCode reasonCode,
            SemanticCandidateSetIdentity candidateSetIdentity,
            SemanticCandidateIdentity candidateIdentity,
            decimal? candidateMargin,
            IEnumerable<string> evidenceSummary)
        {
            if (!Enum.IsDefined(typeof(SemanticCandidateAcceptanceDecisionKind), decision))
            {
                throw new ArgumentOutOfRangeException(nameof(decision));
            }

            if (!Enum.IsDefined(typeof(SemanticCandidateAcceptanceReasonCode), reasonCode))
            {
                throw new ArgumentOutOfRangeException(nameof(reasonCode));
            }

            CandidateSetIdentity = candidateSetIdentity
                ?? throw new ArgumentNullException(nameof(candidateSetIdentity));
            if (candidateIdentity != null
                && !candidateIdentity.SetIdentity.Equals(candidateSetIdentity))
            {
                throw new ArgumentException(
                    "Decision CandidateIdentity 与 CandidateSetIdentity 不一致。",
                    nameof(candidateIdentity));
            }

            if (candidateMargin.HasValue
                && (candidateMargin.Value < 0m || candidateMargin.Value > 1m))
            {
                throw new ArgumentOutOfRangeException(nameof(candidateMargin));
            }

            if (evidenceSummary == null)
            {
                throw new ArgumentNullException(nameof(evidenceSummary));
            }

            List<string> summaries = evidenceSummary.ToList();
            if (summaries.Count == 0 || summaries.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Acceptance Decision 必须包含脱敏 Evidence 摘要。",
                    nameof(evidenceSummary));
            }

            Decision = decision;
            ReasonCode = reasonCode;
            CandidateIdentity = candidateIdentity;
            CandidateMargin = candidateMargin;
            EvidenceSummary = new ReadOnlyCollection<string>(summaries);
        }

        public SemanticCandidateAcceptanceDecisionKind Decision { get; }

        public SemanticCandidateAcceptanceReasonCode ReasonCode { get; }

        public SemanticCandidateSetIdentity CandidateSetIdentity { get; }

        public SemanticCandidateIdentity CandidateIdentity { get; }

        public decimal? CandidateMargin { get; }

        public IReadOnlyList<string> EvidenceSummary { get; }

        public bool FactPromotionAllowed => false;
    }
}
