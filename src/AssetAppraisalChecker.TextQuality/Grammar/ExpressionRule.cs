using System;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class ExpressionRule
    {
        public ExpressionRule(
            string ruleId,
            GrammarExpressionKind kind,
            string trigger,
            string expectedStructure,
            string explanation,
            string pattern)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("规则标识不能为空。", nameof(ruleId));
            if (string.IsNullOrWhiteSpace(trigger)) throw new ArgumentException("触发表达不能为空。", nameof(trigger));
            if (string.IsNullOrWhiteSpace(expectedStructure)) throw new ArgumentException("预期结构不能为空。", nameof(expectedStructure));
            if (string.IsNullOrWhiteSpace(explanation)) throw new ArgumentException("规则说明不能为空。", nameof(explanation));
            if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("规则模式不能为空。", nameof(pattern));

            RuleId = ruleId.Trim();
            Kind = kind;
            Trigger = trigger.Trim();
            ExpectedStructure = expectedStructure.Trim();
            Explanation = explanation.Trim();
            Pattern = new Regex(
                pattern,
                RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        public string RuleId { get; }
        public GrammarExpressionKind Kind { get; }
        public string Trigger { get; }
        public string ExpectedStructure { get; }
        public string Explanation { get; }
        internal Regex Pattern { get; }
    }
}
