using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateIdentity : IEquatable<SemanticCandidateIdentity>
    {
        public SemanticCandidateIdentity(
            SemanticCandidateSetIdentity setIdentity,
            string normalizedCandidateKey)
        {
            SetIdentity = setIdentity ?? throw new ArgumentNullException(nameof(setIdentity));
            SemanticSourceLocation.RequireKey(
                normalizedCandidateKey,
                nameof(normalizedCandidateKey));

            NormalizedCandidateKey = normalizedCandidateKey;
            CanonicalValue = string.Concat(
                setIdentity.CanonicalValue,
                "|",
                normalizedCandidateKey);
        }

        public SemanticCandidateSetIdentity SetIdentity { get; }

        public string NormalizedCandidateKey { get; }

        public string CanonicalValue { get; }

        public bool Equals(SemanticCandidateIdentity other)
        {
            return other != null
                && string.Equals(CanonicalValue, other.CanonicalValue, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticCandidateIdentity);
        }

        public override int GetHashCode()
        {
            return SemanticSourceLocation.StableOrdinalHash(CanonicalValue);
        }

        public override string ToString()
        {
            return CanonicalValue;
        }
    }
}
