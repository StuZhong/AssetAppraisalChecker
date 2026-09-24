using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    /// <summary>
    /// Identifies the business object to which an amount belongs. Scope describes
    /// aggregation level; this key separately answers "whose amount is this?".
    /// </summary>
    public sealed class ComparisonSubjectKey : IEquatable<ComparisonSubjectKey>
    {
        public static readonly ComparisonSubjectKey Unknown =
            new ComparisonSubjectKey(SubjectKind.Unknown, string.Empty);

        public static readonly ComparisonSubjectKey Project =
            new ComparisonSubjectKey(SubjectKind.Project, "PROJECT");

        public ComparisonSubjectKey(SubjectKind subjectKind, string normalizedSubjectId)
        {
            string subjectId = SubjectTextNormalizer.Normalize(normalizedSubjectId);
            if (subjectKind == SubjectKind.Unknown)
            {
                subjectId = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException("明确的比较主体必须具有主体标识。", nameof(normalizedSubjectId));
            }

            SubjectKind = subjectKind;
            NormalizedSubjectId = subjectId;
        }

        public SubjectKind SubjectKind { get; }

        public string NormalizedSubjectId { get; }

        public bool Equals(ComparisonSubjectKey other)
        {
            return other != null &&
                   SubjectKind == other.SubjectKind &&
                   string.Equals(
                       NormalizedSubjectId,
                       other.NormalizedSubjectId,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ComparisonSubjectKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SubjectKind;
                foreach (char character in NormalizedSubjectId)
                {
                    hash = (hash * 397) ^ character;
                }

                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}", SubjectKind, NormalizedSubjectId);
        }
    }
}
