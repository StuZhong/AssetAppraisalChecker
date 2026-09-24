using System.Collections.Generic;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal sealed class FieldClassification
    {
        public FieldClassification(
            FieldIdentity identity,
            ClassificationConfidence confidence,
            ClassificationConfidence valueCategoryConfidence,
            ClassificationConfidence taxBasisConfidence,
            ClassificationConfidence scopeConfidence,
            IReadOnlyList<string> evidence)
        {
            Identity = identity;
            Confidence = confidence;
            ValueCategoryConfidence = valueCategoryConfidence;
            TaxBasisConfidence = taxBasisConfidence;
            ScopeConfidence = scopeConfidence;
            Evidence = evidence;
        }

        public FieldIdentity Identity { get; }

        public ClassificationConfidence Confidence { get; }

        public ClassificationConfidence ValueCategoryConfidence { get; }

        public ClassificationConfidence TaxBasisConfidence { get; }

        public ClassificationConfidence ScopeConfidence { get; }

        public IReadOnlyList<string> Evidence { get; }
    }
}
