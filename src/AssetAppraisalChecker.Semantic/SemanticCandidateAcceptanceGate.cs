using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateAcceptanceGate
    {
        public const string UnresolvedSubjectKey = "subject/v1:unresolved";

        private readonly SemanticCandidateValidator _validator;

        public SemanticCandidateAcceptanceGate()
            : this(new SemanticCandidateValidator())
        {
        }

        public SemanticCandidateAcceptanceGate(SemanticCandidateValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public SemanticCandidateAcceptanceDecision Evaluate(
            SemanticCandidateAnalysis analysis,
            SemanticCandidateAcceptanceContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            SemanticCandidateContractValidationResult validation =
                _validator.Validate(analysis);
            if (!validation.IsValid)
            {
                SemanticCandidateSetIdentity invalidIdentity = analysis == null
                    ? new SemanticCandidateSetIdentity(
                        SemanticCandidateType.EvaluationMethod,
                        UnresolvedSubjectKey,
                        "loc/v1:unknown",
                        SemanticCandidateRole.AdoptedMethod)
                    : analysis.CandidateSet.Identity;
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Rejected,
                    SemanticCandidateAcceptanceReasonCode.ContractInvalid,
                    invalidIdentity,
                    null,
                    null,
                    "ContractValidation=" + string.Join(
                        ",",
                        validation.ReasonCodes));
            }

            SemanticCandidateSet set = analysis.CandidateSet;
            if (analysis.Status == SemanticCandidateAnalyzerStatus.Failed)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.AnalyzerFailure,
                    set.Identity,
                    null,
                    null,
                    analysis.Failure.ToString());
            }

            if (IsHardRuleProtectedType(set.Identity.CandidateType))
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Rejected,
                    SemanticCandidateAcceptanceReasonCode.HardRuleProtectedType,
                    set.Identity,
                    null,
                    null,
                    "CandidateType=" + set.Identity.CandidateType);
            }

            if (context.RequiresSubject
                && string.Equals(
                    set.Identity.SubjectKey,
                    UnresolvedSubjectKey,
                    StringComparison.Ordinal))
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.SubjectUnresolved,
                    set.Identity,
                    null,
                    null,
                    "SubjectKey=" + set.Identity.SubjectKey);
            }

            if (!string.IsNullOrWhiteSpace(context.ExpectedSubjectKey)
                && !string.Equals(
                    set.Identity.SubjectKey,
                    context.ExpectedSubjectKey,
                    StringComparison.Ordinal))
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Rejected,
                    SemanticCandidateAcceptanceReasonCode.SubjectConflict,
                    set.Identity,
                    null,
                    null,
                    "ExpectedSubjectKey=" + context.ExpectedSubjectKey);
            }

            if (context.HasNegation)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Rejected,
                    SemanticCandidateAcceptanceReasonCode.NegationDetected,
                    set.Identity,
                    null,
                    null,
                    "RuleContext=NegationDetected");
            }

            if (context.HasHardRuleConflict)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Rejected,
                    SemanticCandidateAcceptanceReasonCode.HardRuleConflict,
                    set.Identity,
                    null,
                    null,
                    "RuleContext=HardRuleConflict");
            }

            if (set.Candidates.Count == 0)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.NoCandidate,
                    set.Identity,
                    null,
                    null,
                    analysis.Failure == null
                        ? "CandidateCount=0"
                        : analysis.Failure.ToString());
            }

            List<SemanticCandidate> ranked = set.Candidates
                .OrderByDescending(item => item.ModelConfidence)
                .ThenBy(item => item.Identity.CanonicalValue, StringComparer.Ordinal)
                .ToList();
            SemanticCandidate top = ranked[0];

            if (top.ModelConfidence < context.Threshold.MinimumModelConfidence)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.LowModelConfidence,
                    set.Identity,
                    top.Identity,
                    CalculateMargin(ranked),
                    SafeScoreSummary(top, context.Threshold));
            }

            if (context.SimilarityScoreRequired && !top.SimilarityScore.HasValue)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.SimilarityScoreMissing,
                    set.Identity,
                    top.Identity,
                    CalculateMargin(ranked),
                    SafeScoreSummary(top, context.Threshold));
            }

            if (context.SimilarityScoreRequired
                && top.SimilarityScore.Value
                    < context.Threshold.MinimumSimilarityScore.Value)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.SimilarityThresholdNotMet,
                    set.Identity,
                    top.Identity,
                    CalculateMargin(ranked),
                    SafeScoreSummary(top, context.Threshold));
            }

            decimal? margin = CalculateMargin(ranked);
            if (margin.HasValue
                && margin.Value < context.Threshold.AmbiguityThreshold)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Warning,
                    SemanticCandidateAcceptanceReasonCode.MultipleCandidates,
                    set.Identity,
                    top.Identity,
                    margin,
                    SafeScoreSummary(top, context.Threshold));
            }

            if (!context.EvidenceSufficient)
            {
                return Decision(
                    SemanticCandidateAcceptanceDecisionKind.Rejected,
                    SemanticCandidateAcceptanceReasonCode.EvidenceInsufficient,
                    set.Identity,
                    top.Identity,
                    margin,
                    "EvidenceHash=" + top.OriginalEvidence[0].EvidenceHash);
            }

            return Decision(
                SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate,
                SemanticCandidateAcceptanceReasonCode.Accepted,
                set.Identity,
                top.Identity,
                margin,
                SafeScoreSummary(top, context.Threshold));
        }

        public SemanticCandidateRankingAcceptanceDecision EvaluateRanked(
            SemanticCandidateRankingResult ranking,
            SemanticCandidateRankingAcceptanceContext context)
        {
            if (ranking == null)
            {
                throw new ArgumentNullException(nameof(ranking));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            SemanticCandidateSetIdentity identity = ranking.CandidateSetIdentity;
            if (IsHardRuleProtectedType(identity.CandidateType))
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.HardRuleProtectedType,
                    identity,
                    null);
            }

            if (context.RequiresSubject
                && string.Equals(
                    identity.SubjectKey,
                    UnresolvedSubjectKey,
                    StringComparison.Ordinal))
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Abstained,
                    SemanticCandidateRankingAcceptanceReasonCode.SubjectUnresolved,
                    identity,
                    null);
            }

            if (!string.IsNullOrWhiteSpace(context.ExpectedSubjectKey)
                && !string.Equals(
                    identity.SubjectKey,
                    context.ExpectedSubjectKey,
                    StringComparison.Ordinal))
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.SubjectConflict,
                    identity,
                    null);
            }

            if (context.HasNegation)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.NegationDetected,
                    identity,
                    null);
            }

            if (context.HasHardRuleConflict)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.HardRuleConflict,
                    identity,
                    null);
            }

            if (!context.EvidenceSufficient)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.EvidenceInsufficient,
                    identity,
                    null);
            }

            if (ranking.SelectionContext.ShadowOnly)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.ShadowEvidenceOnly,
                    SemanticCandidateRankingAcceptanceReasonCode.ShadowOnly,
                    identity,
                    null);
            }

            if (ranking.SelectionContext.Resolution
                == SemanticCandidateResolution.NoValidCandidate)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Abstained,
                    SemanticCandidateRankingAcceptanceReasonCode.NoValidCandidate,
                    identity,
                    null);
            }

            if (ranking.SelectionContext.Resolution
                == SemanticCandidateResolution.Ambiguous)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Abstained,
                    SemanticCandidateRankingAcceptanceReasonCode.Ambiguous,
                    identity,
                    null);
            }

            if (ranking.Status == SemanticCandidateRankingStatus.Failed)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.RankingFailed,
                    identity,
                    null);
            }

            if (ranking.Status == SemanticCandidateRankingStatus.Abstained)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Abstained,
                    SemanticCandidateRankingAcceptanceReasonCode.RankingAbstained,
                    identity,
                    null);
            }

            SemanticCandidateRankingItem priority = ranking.Rankings
                .Where(item => item.RerankerRank == 1)
                .SingleOrDefault()
                ?? ranking.Rankings
                    .Where(item => item.RetrievalRank == 1)
                    .SingleOrDefault();
            if (priority == null)
            {
                return RankingDecision(
                    SemanticCandidateRankingAcceptanceDecisionKind.Rejected,
                    SemanticCandidateRankingAcceptanceReasonCode.ContractInvalid,
                    identity,
                    null);
            }

            return RankingDecision(
                SemanticCandidateRankingAcceptanceDecisionKind.AcceptedEvidence,
                SemanticCandidateRankingAcceptanceReasonCode.AcceptedForCandidatePriority,
                identity,
                priority.CandidateIdentity);
        }

        private static bool IsHardRuleProtectedType(SemanticCandidateType type)
        {
            return type == SemanticCandidateType.AssessmentValue
                || type == SemanticCandidateType.BenchmarkDate
                || type == SemanticCandidateType.ReportNumber
                || type == SemanticCandidateType.Quantity
                || type == SemanticCandidateType.Owner;
        }

        private static decimal? CalculateMargin(IReadOnlyList<SemanticCandidate> ranked)
        {
            return ranked.Count < 2
                ? (decimal?)null
                : ranked[0].ModelConfidence - ranked[1].ModelConfidence;
        }

        private static string SafeScoreSummary(
            SemanticCandidate candidate,
            SystemAcceptanceThreshold threshold)
        {
            return string.Concat(
                "CandidateIdentity=",
                candidate.Identity,
                "; ModelConfidence=",
                candidate.ModelConfidence.ToString(CultureInfo.InvariantCulture),
                "; SimilarityScore=",
                candidate.SimilarityScore.HasValue
                    ? candidate.SimilarityScore.Value.ToString(CultureInfo.InvariantCulture)
                    : "n/a",
                "; PolicyVersion=",
                threshold.PolicyVersion,
                "; EvidenceHash=",
                candidate.OriginalEvidence[0].EvidenceHash);
        }

        private static SemanticCandidateAcceptanceDecision Decision(
            SemanticCandidateAcceptanceDecisionKind kind,
            SemanticCandidateAcceptanceReasonCode reason,
            SemanticCandidateSetIdentity setIdentity,
            SemanticCandidateIdentity candidateIdentity,
            decimal? margin,
            string evidenceSummary)
        {
            return new SemanticCandidateAcceptanceDecision(
                kind,
                reason,
                setIdentity,
                candidateIdentity,
                margin,
                new[] { evidenceSummary });
        }

        private static SemanticCandidateRankingAcceptanceDecision RankingDecision(
            SemanticCandidateRankingAcceptanceDecisionKind decision,
            SemanticCandidateRankingAcceptanceReasonCode reason,
            SemanticCandidateSetIdentity setIdentity,
            SemanticCandidateIdentity priorityIdentity)
        {
            return new SemanticCandidateRankingAcceptanceDecision(
                decision,
                reason,
                setIdentity,
                priorityIdentity,
                new[]
                {
                    string.Concat(
                        "SemanticRankingGate | Decision=",
                        decision,
                        " | Reason=",
                        reason,
                        " | CandidateSetIdentity=",
                        setIdentity,
                        " | PriorityCandidateIdentity=",
                        priorityIdentity == null ? "n/a" : priorityIdentity.ToString(),
                        " | FactPromotionAllowed=false")
                });
        }
    }
}
