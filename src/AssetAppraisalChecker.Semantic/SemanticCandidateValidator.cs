using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateValidator
    {
        public SemanticCandidateContractValidationResult Validate(
            SemanticCandidateAnalysis analysis)
        {
            if (analysis == null)
            {
                return Invalid(SemanticCandidateContractValidationReasonCode.NullAnalysis);
            }

            var reasons = new HashSet<SemanticCandidateContractValidationReasonCode>();
            SemanticCandidateSet set = analysis.CandidateSet;

            if (analysis.Failure != null
                && analysis.Failure.FailureCode == SemanticCandidateFailureCode.InvalidOutput)
            {
                reasons.Add(SemanticCandidateContractValidationReasonCode.InvalidOutput);
            }

            if (set.Identity.CandidateType == SemanticCandidateType.Unknown
                || !Enum.IsDefined(typeof(SemanticCandidateType), set.Identity.CandidateType))
            {
                reasons.Add(
                    SemanticCandidateContractValidationReasonCode.UnsupportedCandidateType);
            }

            if (set.Candidates.GroupBy(item => item.Identity).Any(group => group.Count() > 1))
            {
                reasons.Add(
                    SemanticCandidateContractValidationReasonCode.DuplicateCandidateIdentity);
            }

            foreach (SemanticCandidate candidate in set.Candidates)
            {
                if (!candidate.Identity.SetIdentity.Equals(set.Identity)
                    || !string.Equals(
                        candidate.Identity.NormalizedCandidateKey,
                        candidate.NormalizedCandidate,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        candidate.SourceLocation.SourceLocationKey,
                        set.Identity.SourceLocationKey,
                        StringComparison.Ordinal))
                {
                    reasons.Add(
                        SemanticCandidateContractValidationReasonCode.IdentityMismatch);
                }

                if (candidate.ModelConfidence < 0m || candidate.ModelConfidence > 1m
                    || (candidate.SimilarityScore.HasValue
                        && (candidate.SimilarityScore.Value < 0m
                            || candidate.SimilarityScore.Value > 1m)))
                {
                    reasons.Add(SemanticCandidateContractValidationReasonCode.InvalidScore);
                }

                if (string.IsNullOrWhiteSpace(candidate.ModelId)
                    || string.IsNullOrWhiteSpace(candidate.ModelVersion)
                    || string.IsNullOrWhiteSpace(candidate.AnalyzerVersion))
                {
                    reasons.Add(
                        SemanticCandidateContractValidationReasonCode.MissingModelMetadata);
                }

                if (!SemanticHash.IsSha256(candidate.ModelHash))
                {
                    reasons.Add(SemanticCandidateContractValidationReasonCode.InvalidModelHash);
                }

                if (candidate.OriginalEvidence.Count == 0
                    || candidate.OriginalEvidence.Any(item =>
                        item == null
                        || !SemanticHash.IsSha256(item.EvidenceHash)
                        || !string.Equals(
                            SemanticHash.ComputeSha256(item.OriginalText),
                            item.EvidenceHash,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    reasons.Add(SemanticCandidateContractValidationReasonCode.InvalidEvidence);
                }
            }

            if ((analysis.Status == SemanticCandidateAnalyzerStatus.Succeeded
                    && analysis.Failure != null)
                || (analysis.Status != SemanticCandidateAnalyzerStatus.Succeeded
                    && analysis.Failure == null))
            {
                reasons.Add(SemanticCandidateContractValidationReasonCode.InvalidFailureState);
            }

            return reasons.Count == 0
                ? new SemanticCandidateContractValidationResult(
                    true,
                    new[] { SemanticCandidateContractValidationReasonCode.Valid })
                : new SemanticCandidateContractValidationResult(false, reasons);
        }

        private static SemanticCandidateContractValidationResult Invalid(
            SemanticCandidateContractValidationReasonCode reasonCode)
        {
            return new SemanticCandidateContractValidationResult(
                false,
                new[] { reasonCode });
        }
    }
}
