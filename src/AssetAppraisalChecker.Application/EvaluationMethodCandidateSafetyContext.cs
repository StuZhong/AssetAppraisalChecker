using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Application
{
    // AI-only context derived from an already extracted CandidateUnknown statement.
    // Does not extract/promote facts or broaden the extractor's supported descriptions.
    internal sealed class EvaluationMethodCandidateSafetyContext
    {
        private static readonly Regex Description = new Regex(
            "现行重建成本|重建成本测算|重置成本途径", RegexOptions.CultureInvariant);
        private static readonly Regex Clause = new Regex(
            @"[^，,；;。！？!?\r\n]+[，,；;。！？!?]?", RegexOptions.CultureInvariant);
        private static readonly Regex NegativeVerb = new Regex(
            "并不适用|不适用|不宜采用|不应采用|不予采用|无法采用|不能采用|不得采用|不再采用|没有采用|未曾采用|未予采用|不采用|未采用|没有选用|不选用|未选用|没有使用|不使用|未使用|不考虑|未考虑|不依据|未依据|不基于|未基于|不通过|未通过|不按|未按|不以|未以|不是采用|并非采用|非采用|不是按|并非按|不是通过|并非通过",
            RegexOptions.CultureInvariant);
        private static readonly Regex Bridge = new Regex(
            @"^(?:\s|该|此|这种|本次|本项|上述|以下|基于|按照|按|以|通过|的)*$", RegexOptions.CultureInvariant);
        private static readonly Regex PositiveVerb = new Regex(
            "采用|选用|使用|按照|依据|基于|通过|按|以", RegexOptions.CultureInvariant);
        private static readonly Regex UncertainExpression = new Regex(
            "是否|尚未确定|尚待|待定|可能|拟(?:采用|按|通过|以|依据|选用|使用)|如果|若(?:采用|按|通过|以)|或者|而非|而不是|并非不|不是不|不能不|不得不|不一定|未必|[“”‘’「」《》]",
            RegexOptions.CultureInvariant);

        private EvaluationMethodCandidateSafetyContext(string text, int offset, bool negated, bool uncertain)
        {
            Text = text;
            StatementOffset = offset;
            HasNegation = negated;
            IsUncertain = uncertain;
        }

        public string Text { get; }
        public int StatementOffset { get; }
        public bool HasNegation { get; }
        public bool IsUncertain { get; }

        public static IReadOnlyList<EvaluationMethodCandidateSafetyContext> Resolve(string originalText)
        {
            var result = new List<EvaluationMethodCandidateSafetyContext>();
            foreach (Match clause in Clause.Matches(originalText))
            {
                Match[] descriptions = Description.Matches(clause.Value).Cast<Match>().ToArray();
                if (descriptions.Length == 0) continue;
                // Without a reliable clause boundary, competing method expressions must
                // not borrow each other's adoption/negation scope.
                if (descriptions.Length != 1 || UncertainExpression.IsMatch(clause.Value))
                {
                    result.Add(new EvaluationMethodCandidateSafetyContext(clause.Value, clause.Index, false, true));
                    continue;
                }

                Match description = descriptions[0];
                string prefix = clause.Value.Substring(0, description.Index);
                string suffix = clause.Value.Substring(description.Index + description.Length);
                Match[] negatives = NegativeVerb.Matches(prefix).Cast<Match>().ToArray();
                if (negatives.Length > 0)
                {
                    Match negative = negatives.Last();
                    bool linked = Bridge.IsMatch(prefix.Substring(negative.Index + negative.Length));
                    result.Add(new EvaluationMethodCandidateSafetyContext(clause.Value, clause.Index, linked, !linked));
                }
                else if (NegativeVerb.IsMatch(suffix))
                {
                    // A trailing predicate can negate the expression too. Do not accept
                    // an earlier positive predicate when the local relationship is unclear.
                    result.Add(new EvaluationMethodCandidateSafetyContext(clause.Value, clause.Index, false, true));
                }
                else
                {
                    bool linked = PositiveVerb.Matches(prefix).Cast<Match>().Any(verb =>
                        Bridge.IsMatch(prefix.Substring(verb.Index + verb.Length)));
                    result.Add(new EvaluationMethodCandidateSafetyContext(clause.Value, clause.Index, false, !linked));
                }
            }

            if (result.Count == 0)
            {
                // Preserve the existing unresolved-Subject path (which may contain a
                // standard method name) and abstain for any other unsupported context.
                result.Add(new EvaluationMethodCandidateSafetyContext(originalText, 0, false, true));
            }
            return result.AsReadOnly();
        }
    }
}
