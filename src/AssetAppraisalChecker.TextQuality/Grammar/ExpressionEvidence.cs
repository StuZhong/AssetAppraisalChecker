using System;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class ExpressionEvidence
    {
        public ExpressionEvidence(ExpressionRule rule, string matchedText)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            MatchedText = matchedText ?? string.Empty;
        }

        public ExpressionRule Rule { get; }
        public string RuleId => Rule.RuleId;
        public GrammarExpressionKind Kind => Rule.Kind;
        public string Trigger => Rule.Trigger;
        public string ExpectedStructure => Rule.ExpectedStructure;
        public string Explanation => Rule.Explanation;
        public string MatchedText { get; }
    }
}
