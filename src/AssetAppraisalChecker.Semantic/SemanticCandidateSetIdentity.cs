using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateSetIdentity : IEquatable<SemanticCandidateSetIdentity>
    {
        public SemanticCandidateSetIdentity(
            SemanticCandidateType candidateType,
            string subjectKey,
            string sourceLocationKey,
            SemanticCandidateRole candidateRole)
        {
            if (candidateType == SemanticCandidateType.Unknown
                || !Enum.IsDefined(typeof(SemanticCandidateType), candidateType))
            {
                throw new ArgumentOutOfRangeException(nameof(candidateType));
            }

            if (candidateRole == SemanticCandidateRole.Unknown
                || !Enum.IsDefined(typeof(SemanticCandidateRole), candidateRole))
            {
                throw new ArgumentOutOfRangeException(nameof(candidateRole));
            }

            SemanticSourceLocation.RequireKey(subjectKey, nameof(subjectKey));
            SemanticSourceLocation.RequireKey(sourceLocationKey, nameof(sourceLocationKey));

            CandidateType = candidateType;
            SubjectKey = subjectKey;
            SourceLocationKey = sourceLocationKey;
            CandidateRole = candidateRole;
            CanonicalValue = string.Concat(
                candidateType,
                "|",
                subjectKey,
                "|",
                sourceLocationKey,
                "|",
                candidateRole);
        }

        public SemanticCandidateType CandidateType { get; }

        public string SubjectKey { get; }

        public string SourceLocationKey { get; }

        public SemanticCandidateRole CandidateRole { get; }

        public string CanonicalValue { get; }

        public bool Equals(SemanticCandidateSetIdentity other)
        {
            return other != null
                && string.Equals(CanonicalValue, other.CanonicalValue, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticCandidateSetIdentity);
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
