using AssetAppraisalChecker.Core.BusinessFields;

namespace AssetAppraisalChecker.Core.Consistency
{
    internal static class ComparisonEligibility
    {
        public static bool IsEligible(MoneyOccurrence occurrence)
        {
            // A field category and aggregation scope do not identify the owning asset or
            // project. Deterministic comparison is therefore fail-closed until the business
            // subject itself is explicit and supported by more than weak evidence.
            return occurrence != null &&
                   occurrence.FieldIdentity.ValueCategory != ValueCategory.Unknown &&
                   occurrence.ValueCategoryConfidence != ClassificationConfidence.Low &&
                   occurrence.SubjectKind != SubjectKind.Unknown &&
                   occurrence.SubjectConfidence != ClassificationConfidence.Low;
        }
    }
}
