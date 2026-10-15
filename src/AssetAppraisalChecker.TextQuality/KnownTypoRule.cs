using System;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class KnownTypoRule
    {
        public KnownTypoRule(string incorrectText, string suggestedTerm, string ruleId)
            : this(
                incorrectText,
                suggestedTerm,
                ruleId,
                "受控项目回归中确认的明确错字。",
                KnownTypoSourceCategory.ProjectRegression)
        {
        }

        public KnownTypoRule(
            string incorrectText,
            string suggestedTerm,
            string ruleId,
            string reason,
            KnownTypoSourceCategory sourceCategory)
        {
            if (string.IsNullOrWhiteSpace(incorrectText))
            {
                throw new ArgumentException("错误形式不能为空。", nameof(incorrectText));
            }

            if (string.IsNullOrWhiteSpace(suggestedTerm))
            {
                throw new ArgumentException("建议术语不能为空。", nameof(suggestedTerm));
            }

            if (string.IsNullOrWhiteSpace(ruleId))
            {
                throw new ArgumentException("规则标识不能为空。", nameof(ruleId));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("规则原因不能为空。", nameof(reason));
            }

            IncorrectText = incorrectText;
            SuggestedTerm = suggestedTerm;
            RuleId = ruleId;
            Reason = reason;
            SourceCategory = sourceCategory;
        }

        public string IncorrectText { get; }

        public string SuggestedTerm { get; }

        public string RuleId { get; }

        public string Reason { get; }

        public KnownTypoSourceCategory SourceCategory { get; }
    }
}
