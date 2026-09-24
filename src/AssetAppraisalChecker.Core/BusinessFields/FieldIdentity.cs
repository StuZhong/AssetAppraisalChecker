using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    public sealed class FieldIdentity : IEquatable<FieldIdentity>
    {
        public FieldIdentity(
            ValueCategory valueCategory,
            TaxBasis taxBasis,
            Scope scope,
            SectionCategory sectionCategory)
        {
            ValueCategory = valueCategory;
            TaxBasis = taxBasis;
            Scope = scope;
            SectionCategory = sectionCategory;
        }

        public ValueCategory ValueCategory { get; }

        public TaxBasis TaxBasis { get; }

        public Scope Scope { get; }

        public SectionCategory SectionCategory { get; }

        public bool Equals(FieldIdentity other)
        {
            return other != null &&
                   ValueCategory == other.ValueCategory &&
                   TaxBasis == other.TaxBasis &&
                   Scope == other.Scope &&
                   SectionCategory == other.SectionCategory;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FieldIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ValueCategory;
                hash = (hash * 397) ^ (int)TaxBasis;
                hash = (hash * 397) ^ (int)Scope;
                hash = (hash * 397) ^ (int)SectionCategory;
                return hash;
            }
        }
    }
}
