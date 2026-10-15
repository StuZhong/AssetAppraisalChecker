using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateRequest
    {
        public SemanticCandidateRequest(
            string requestId,
            SemanticCandidateSetIdentity candidateSetIdentity,
            IEnumerable<SemanticCandidateEvidence> evidence,
            IEnumerable<string> allowedNormalizedCandidates,
            string ontologyVersion,
            string analyzerPolicyVersion,
            bool similarityScoreRequested)
        {
            SemanticSourceLocation.RequireKey(requestId, nameof(requestId));
            CandidateSetIdentity = candidateSetIdentity
                ?? throw new ArgumentNullException(nameof(candidateSetIdentity));
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<SemanticCandidateEvidence> evidenceItems = evidence.ToList();
            if (evidenceItems.Count == 0 || evidenceItems.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Semantic 请求必须包含 Evidence。",
                    nameof(evidence));
            }

            if (allowedNormalizedCandidates == null)
            {
                throw new ArgumentNullException(nameof(allowedNormalizedCandidates));
            }

            List<string> allowedItems = allowedNormalizedCandidates.ToList();
            if (allowedItems.Count == 0 || allowedItems.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Semantic 请求必须包含受控候选值。",
                    nameof(allowedNormalizedCandidates));
            }

            if (allowedItems.Distinct(StringComparer.Ordinal).Count() != allowedItems.Count)
            {
                throw new ArgumentException(
                    "受控候选值不能重复。",
                    nameof(allowedNormalizedCandidates));
            }

            SemanticSourceLocation.RequireText(ontologyVersion, nameof(ontologyVersion));
            SemanticSourceLocation.RequireText(
                analyzerPolicyVersion,
                nameof(analyzerPolicyVersion));

            RequestId = requestId;
            Evidence = new ReadOnlyCollection<SemanticCandidateEvidence>(evidenceItems);
            allowedItems.Sort(StringComparer.Ordinal);
            AllowedNormalizedCandidates = new ReadOnlyCollection<string>(allowedItems);
            OntologyVersion = ontologyVersion;
            AnalyzerPolicyVersion = analyzerPolicyVersion;
            SimilarityScoreRequested = similarityScoreRequested;
        }

        public string RequestId { get; }

        public SemanticCandidateSetIdentity CandidateSetIdentity { get; }

        public IReadOnlyList<SemanticCandidateEvidence> Evidence { get; }

        public IReadOnlyList<string> AllowedNormalizedCandidates { get; }

        public string OntologyVersion { get; }

        public string AnalyzerPolicyVersion { get; }

        public bool SimilarityScoreRequested { get; }

        public override string ToString()
        {
            return string.Concat(
                "RequestId=",
                RequestId,
                "; CandidateSetIdentity=",
                CandidateSetIdentity,
                "; EvidenceHash=",
                Evidence[0].EvidenceHash,
                "; OntologyVersion=",
                OntologyVersion,
                "; AnalyzerPolicyVersion=",
                AnalyzerPolicyVersion);
        }
    }
}
