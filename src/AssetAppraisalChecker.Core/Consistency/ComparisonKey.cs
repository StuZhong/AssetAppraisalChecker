using System;
using AssetAppraisalChecker.Core.BusinessFields;

namespace AssetAppraisalChecker.Core.Consistency
{
    /// <summary>
    /// Identifies occurrences that are eligible for the same business comparison.
    /// SectionCategory is deliberately excluded because the same value is expected
    /// to remain consistent across summaries, conclusions and result tables.
    /// </summary>
    public sealed class ComparisonKey : IEquatable<ComparisonKey>
    {
        public ComparisonKey(
            ValueCategory valueCategory,
            TaxBasis taxBasis,
            Scope scope,
            ComparisonSubjectKey comparisonSubjectKey)
        {
            if (comparisonSubjectKey == null)
            {
                throw new ArgumentNullException(nameof(comparisonSubjectKey));
            }

            ValueCategory = valueCategory;
            TaxBasis = taxBasis;
            Scope = scope;
            ComparisonSubjectKey = comparisonSubjectKey;
        }

        public ValueCategory ValueCategory { get; }

        public TaxBasis TaxBasis { get; }

        public Scope Scope { get; }

        public ComparisonSubjectKey ComparisonSubjectKey { get; }

        public static ComparisonKey From(
            FieldIdentity identity,
            ComparisonSubjectKey comparisonSubjectKey)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return new ComparisonKey(
                identity.ValueCategory,
                identity.TaxBasis,
                identity.Scope,
                comparisonSubjectKey);
        }

        public bool Equals(ComparisonKey other)
        {
            return other != null &&
                   ValueCategory == other.ValueCategory &&
                   TaxBasis == other.TaxBasis &&
                   Scope == other.Scope &&
                   ComparisonSubjectKey.Equals(other.ComparisonSubjectKey);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ComparisonKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ValueCategory;
                hash = (hash * 397) ^ (int)TaxBasis;
                hash = (hash * 397) ^ (int)Scope;
                hash = (hash * 397) ^ ComparisonSubjectKey.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                "{0}/{1}/{2}/{3}",
                ValueCategory,
                TaxBasis,
                Scope,
                ComparisonSubjectKey);
        }
    }
}
