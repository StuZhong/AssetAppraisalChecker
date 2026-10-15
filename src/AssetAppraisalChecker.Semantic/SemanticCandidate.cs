using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidate
    {
        public SemanticCandidate(
            SemanticCandidateIdentity identity,
            string candidateValue,
            string normalizedCandidate,
            decimal? similarityScore,
            decimal modelConfidence,
            SemanticSourceLocation sourceLocation,
            IEnumerable<SemanticCandidateEvidence> originalEvidence,
            string modelId,
            string modelVersion,
            string analyzerVersion,
            string modelHash,
            string candidateReason,
            SemanticCandidateCreatedBy createdBy)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            SemanticSourceLocation.RequireText(candidateValue, nameof(candidateValue));
            SemanticSourceLocation.RequireKey(normalizedCandidate, nameof(normalizedCandidate));
            ValidateScore(modelConfidence, nameof(modelConfidence));
            if (similarityScore.HasValue)
            {
                ValidateScore(similarityScore.Value, nameof(similarityScore));
            }

            SourceLocation = sourceLocation
                ?? throw new ArgumentNullException(nameof(sourceLocation));
            if (!string.Equals(
                identity.SetIdentity.SourceLocationKey,
                sourceLocation.SourceLocationKey,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Candidate Identity 与 SourceLocation 不一致。",
                    nameof(sourceLocation));
            }

            if (!string.Equals(
                identity.NormalizedCandidateKey,
                normalizedCandidate,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Identity 与 NormalizedCandidate 不一致。",
                    nameof(normalizedCandidate));
            }

            if (originalEvidence == null)
            {
                throw new ArgumentNullException(nameof(originalEvidence));
            }

            List<SemanticCandidateEvidence> evidenceItems = originalEvidence.ToList();
            if (evidenceItems.Count == 0 || evidenceItems.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Candidate 必须包含非空 Evidence。",
                    nameof(originalEvidence));
            }

            if (evidenceItems.Any(item => !string.Equals(
                item.SourceLocation.SourceDocument,
                sourceLocation.SourceDocument,
                StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Candidate Evidence 必须属于同一来源文档。",
                    nameof(originalEvidence));
            }

            SemanticSourceLocation.RequireText(modelId, nameof(modelId));
            SemanticSourceLocation.RequireText(modelVersion, nameof(modelVersion));
            SemanticSourceLocation.RequireText(analyzerVersion, nameof(analyzerVersion));
            if (!SemanticHash.IsSha256(modelHash))
            {
                throw new ArgumentException(
                    "ModelHash 必须是 64 位 SHA256 十六进制值。",
                    nameof(modelHash));
            }

            SemanticSourceLocation.RequireText(candidateReason, nameof(candidateReason));
            if (!Enum.IsDefined(typeof(SemanticCandidateCreatedBy), createdBy))
            {
                throw new ArgumentOutOfRangeException(nameof(createdBy));
            }

            CandidateValue = candidateValue;
            NormalizedCandidate = normalizedCandidate;
            SimilarityScore = similarityScore;
            ModelConfidence = modelConfidence;
            OriginalEvidence = new ReadOnlyCollection<SemanticCandidateEvidence>(
                evidenceItems);
            ModelId = modelId;
            ModelVersion = modelVersion;
            AnalyzerVersion = analyzerVersion;
            ModelHash = modelHash.ToLowerInvariant();
            CandidateReason = candidateReason;
            CreatedBy = createdBy;
        }

        public SemanticCandidateIdentity Identity { get; }

        public SemanticCandidateType CandidateType => Identity.SetIdentity.CandidateType;

        public string CandidateValue { get; }

        public string NormalizedCandidate { get; }

        public string SubjectKey => Identity.SetIdentity.SubjectKey;

        public SemanticCandidateRole CandidateRole => Identity.SetIdentity.CandidateRole;

        public decimal? SimilarityScore { get; }

        public decimal ModelConfidence { get; }

        public SemanticSourceLocation SourceLocation { get; }

        public IReadOnlyList<SemanticCandidateEvidence> OriginalEvidence { get; }

        public string ModelId { get; }

        public string ModelVersion { get; }

        public string AnalyzerVersion { get; }

        public string ModelHash { get; }

        public string CandidateReason { get; }

        public SemanticCandidateCreatedBy CreatedBy { get; }

        public string ToSafeDiagnosticString()
        {
            return string.Concat(
                "CandidateType=",
                CandidateType,
                "; SubjectKey=",
                SubjectKey,
                "; ModelConfidence=",
                ModelConfidence,
                "; SimilarityScore=",
                SimilarityScore.HasValue ? SimilarityScore.Value.ToString() : "n/a",
                "; ModelId=",
                ModelId,
                "; ModelVersion=",
                ModelVersion,
                "; AnalyzerVersion=",
                AnalyzerVersion,
                "; SourceLocation=",
                SourceLocation.LocationSummary,
                "; EvidenceHash=",
                OriginalEvidence[0].EvidenceHash);
        }

        public override string ToString()
        {
            return ToSafeDiagnosticString();
        }

        private static void ValidateScore(decimal value, string parameterName)
        {
            if (value < 0m || value > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "分数必须在 0 到 1 之间。");
            }
        }
    }
}
