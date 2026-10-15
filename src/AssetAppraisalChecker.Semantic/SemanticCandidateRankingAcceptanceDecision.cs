using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateRankingAcceptanceDecision
    {
        public SemanticCandidateRankingAcceptanceDecision(
            SemanticCandidateRankingAcceptanceDecisionKind decision,
            SemanticCandidateRankingAcceptanceReasonCode reasonCode,
            SemanticCandidateSetIdentity candidateSetIdentity,
            SemanticCandidateIdentity priorityCandidateIdentity,
            IEnumerable<string> evidence)
        {
            if (!Enum.IsDefined(
                typeof(SemanticCandidateRankingAcceptanceDecisionKind),
                decision))
            {
                throw new ArgumentOutOfRangeException(nameof(decision));
            }

            if (!Enum.IsDefined(
                typeof(SemanticCandidateRankingAcceptanceReasonCode),
                reasonCode))
            {
                throw new ArgumentOutOfRangeException(nameof(reasonCode));
            }

            CandidateSetIdentity = candidateSetIdentity
                ?? throw new ArgumentNullException(nameof(candidateSetIdentity));
            if (priorityCandidateIdentity != null
                && !priorityCandidateIdentity.SetIdentity.Equals(candidateSetIdentity))
            {
                throw new ArgumentException(
                    "Priority Candidate 必须属于当前 CandidateSet。",
                    nameof(priorityCandidateIdentity));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<string> evidenceItems = evidence.ToList();
            if (evidenceItems.Count == 0 || evidenceItems.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Ranking Acceptance Decision 必须包含安全证据。",
                    nameof(evidence));
            }

            Decision = decision;
            ReasonCode = reasonCode;
            PriorityCandidateIdentity = priorityCandidateIdentity;
            Evidence = new ReadOnlyCollection<string>(evidenceItems);
        }

        public SemanticCandidateRankingAcceptanceDecisionKind Decision { get; }

        public SemanticCandidateRankingAcceptanceReasonCode ReasonCode { get; }

        public SemanticCandidateSetIdentity CandidateSetIdentity { get; }

        public SemanticCandidateIdentity PriorityCandidateIdentity { get; }

        public IReadOnlyList<string> Evidence { get; }

        public bool FactPromotionAllowed => false;
    }
}
