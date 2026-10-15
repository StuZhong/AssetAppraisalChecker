using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateEvidence
    {
        public SemanticCandidateEvidence(
            SemanticSourceLocation sourceLocation,
            string originalText,
            string evidenceRole,
            string evidenceHash)
        {
            SourceLocation = sourceLocation
                ?? throw new ArgumentNullException(nameof(sourceLocation));
            SemanticSourceLocation.RequireText(originalText, nameof(originalText));
            SemanticSourceLocation.RequireText(evidenceRole, nameof(evidenceRole));
            if (!SemanticHash.IsSha256(evidenceHash))
            {
                throw new ArgumentException(
                    "EvidenceHash 必须是 64 位 SHA256 十六进制值。",
                    nameof(evidenceHash));
            }

            string actualHash = SemanticHash.ComputeSha256(originalText);
            if (!string.Equals(actualHash, evidenceHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "EvidenceHash 与 OriginalText 不匹配。",
                    nameof(evidenceHash));
            }

            OriginalText = originalText;
            EvidenceRole = evidenceRole;
            EvidenceHash = evidenceHash.ToLowerInvariant();
        }

        public SemanticSourceLocation SourceLocation { get; }

        public string OriginalText { get; }

        public string EvidenceRole { get; }

        public string EvidenceHash { get; }

        public override string ToString()
        {
            return string.Concat(
                "EvidenceRole=",
                EvidenceRole,
                "; SourceLocation=",
                SourceLocation,
                "; EvidenceHash=",
                EvidenceHash);
        }
    }
}
