namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal sealed class MergedHeaderColumnSemantic
    {
        public MergedHeaderColumnSemantic(
            ValueCategory valueCategory,
            TaxBasis taxBasis)
        {
            ValueCategory = valueCategory;
            TaxBasis = taxBasis;
        }

        public ValueCategory ValueCategory { get; }

        public TaxBasis TaxBasis { get; }
    }
}
