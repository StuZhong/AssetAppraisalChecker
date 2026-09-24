using System.Collections.Generic;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Tests.Consistency
{
    internal static class ConsistencyTestOccurrenceFactory
    {
        public static MoneyOccurrence Create(
            decimal amount,
            ValueCategory category = ValueCategory.AppraisedNetValue,
            TaxBasis taxBasis = TaxBasis.TaxExcluded,
            Scope scope = Scope.ProjectTotal,
            SectionCategory section = SectionCategory.ValuationConclusion,
            ClassificationConfidence confidence = ClassificationConfidence.High,
            string rawText = null,
            int order = 0,
            bool isTableCell = false,
            MoneyNumberFormat numberFormat = MoneyNumberFormat.Arabic,
            ClassificationConfidence? valueCategoryConfidence = null,
            ClassificationConfidence? taxBasisConfidence = null,
            ClassificationConfidence? scopeConfidence = null,
            SubjectKind subjectKind = SubjectKind.Project,
            string subjectId = "PROJECT",
            ClassificationConfidence subjectConfidence = ClassificationConfidence.High)
        {
            string text = rawText ?? amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "元";
            var location = isTableCell
                ? new DocumentLocation(
                    DocumentPart.MainBody,
                    null,
                    order,
                    1,
                    1,
                    new[] { "评估结果表" })
                : new DocumentLocation(
                    DocumentPart.MainBody,
                    order,
                    null,
                    null,
                    null,
                    new[] { "评估结论" });

            return new MoneyOccurrence(
                text,
                amount,
                MoneyParseStatus.Success,
                numberFormat,
                MoneyUnit.Yuan,
                new string[0],
                new FieldIdentity(category, taxBasis, scope, section),
                location,
                new DocumentNearbyContext(string.Empty, "测试字段" + text, string.Empty),
                confidence,
                valueCategoryConfidence ?? confidence,
                taxBasisConfidence ?? confidence,
                scopeConfidence ?? confidence,
                new List<string> { "虚构测试中的明确字段标签。" },
                subjectKind == SubjectKind.Unknown
                    ? ComparisonSubjectKey.Unknown
                    : new ComparisonSubjectKey(subjectKind, subjectId),
                subjectConfidence,
                new List<string> { "虚构测试中的明确比较主体。" },
                0,
                text.Length);
        }
    }
}
