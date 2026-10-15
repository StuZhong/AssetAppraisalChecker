using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    // Exclusion only: method-specific calculations do not establish the adopted conclusion.
    // This does not infer a method, subject, tax basis or confidence for any amount.
    public static class MethodCalculationContext
    {
        private static readonly Regex MethodPattern = new Regex(
            "资产基础法|收益法|市场法|重置成本法|成本法",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex CalculationPattern = new Regex(
            "测算|计算|评估结果", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AdoptedConclusionPattern = new Regex(
            @"最终(?:采用|选用|选取)|(?:作为|确定为)(?:本项目|本次评估)?(?:最终)?评估结论",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NegatedAdoptionPattern = new Regex(
            "不|未|非|没有", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsMethodSpecificResult(MoneyOccurrence occurrence)
        {
            if (occurrence == null) throw new ArgumentNullException(nameof(occurrence));
            ValueCategory category = occurrence.FieldIdentity.ValueCategory;
            if (category != ValueCategory.AppraisedValue &&
                category != ValueCategory.AppraisedOriginalValue &&
                category != ValueCategory.AppraisedNetValue) return false;
            string text = occurrence.NearbyContext.CurrentText;
            // Existing synthetic/manual occurrences may not carry a complete source span.
            // Do not invent context or alter their existing comparison contract.
            if (string.IsNullOrEmpty(text) || occurrence.TextStartIndex > text.Length - occurrence.TextLength)
                return false;
            if (!string.Equals(text.Substring(occurrence.TextStartIndex, occurrence.TextLength),
                occurrence.RawText, StringComparison.Ordinal)) return false;
            string clause = LocalContextBuilder.Build(text, new MoneyCandidate(
                occurrence.RawText, occurrence.TextStartIndex, occurrence.TextLength));
            int methodCount = MethodPattern.Matches(clause).Cast<Match>()
                .Select(match => match.Value).Distinct(StringComparer.Ordinal).Count();
            if (methodCount == 0 || !CalculationPattern.IsMatch(clause)) return false;

            // A unique method explicitly adopted as the final conclusion can still be
            // compared under the existing identity rules. Multiple methods remain ambiguous.
            return methodCount != 1 || !AdoptedConclusionPattern.IsMatch(clause) ||
                   NegatedAdoptionPattern.IsMatch(clause.Replace("不含税", "").Replace("不含增值税", ""));
        }
    }
}
