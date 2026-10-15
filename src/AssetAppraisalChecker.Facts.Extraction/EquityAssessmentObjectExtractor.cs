using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    // Deliberately narrow, affirmative object assertions only. Neither a role
    // label in another cell nor an equity keyword in a project title is enough.
    internal static class EquityAssessmentObjectExtractor
    {
        private static readonly Regex Statement = new Regex(
            @"^(?:本次资产评估的|本次评估的|本次)?评估对象\s*(?:为|是|：|:)\s*(?<value>.+)$",
            RegexOptions.CultureInvariant);
        private const string Subject = @"(?<subject>[A-Za-z0-9\u4e00-\u9fff·]{2,100}?)\s*";
        private static readonly Regex Entire = new Regex(
            "^" + Subject + @"(?:股东全部权益|全部股东权益)(?:的)?价值$", RegexOptions.CultureInvariant);
        private static readonly Regex Percentage = new Regex(
            "^" + Subject + @"(?<share>[0-9]+(?:\.[0-9]+)?)\s*[%％]股权(?:的)?价值$",
            RegexOptions.CultureInvariant);
        private static readonly Regex Partial = new Regex(
            "^" + Subject + @"部分股权(?:的)?价值\s*[（(]持股比例\s*(?:为|：|:)\s*(?<share>[0-9]+(?:\.[0-9]+)?)\s*[%％][）)]$",
            RegexOptions.CultureInvariant);
        private static readonly Regex Company = new Regex(
            @"^(?!(?:非|不|未|拟|例如|假设))(?!.*(?:公司.+公司|或者|以及|可能|待定|不确定|并非|不是|例如|假设|拟为|委托人|被评估单位|评估范围|项目名称|产权持有人))[A-Za-z0-9\u4e00-\u9fff·]{1,80}公司$",
            RegexOptions.CultureInvariant);

        public static IReadOnlyList<AssessmentObjectFact> Extract(
            DocumentReadResult document, string source, AssessmentObjectSourceContext context)
        {
            var facts = new List<AssessmentObjectFact>();
            bool unresolvedObject = false;
            foreach (DocumentElement element in document.Elements.OrderBy(item => item.DocumentOrderIndex))
            foreach (string clause in Regex.Split(element.Text, @"[。；;\r\n]"))
            {
                Match statement = Statement.Match(clause.Trim());
                if (!statement.Success) continue;
                string value = statement.Groups["value"].Value.Trim();
                Match match = Entire.Match(value);
                AssessmentEquityInterest interest = AssessmentEquityInterest.EntireShareholdersEquity;
                decimal share = 100m;
                if (!match.Success)
                {
                    interest = AssessmentEquityInterest.PartialShareholding;
                    match = Percentage.Match(value);
                    if (!match.Success) match = Partial.Match(value);
                    if (!match.Success || !decimal.TryParse(match.Groups["share"].Value,
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out share)
                        || share <= 0m || share >= 100m)
                    {
                        unresolvedObject = true;
                        continue;
                    }
                }

                string name = match.Groups["subject"].Value;
                IReadOnlyList<DocumentElement> aliases;
                string canonical = ResolveSubject(name, context, out aliases);
                if (canonical == null)
                {
                    unresolvedObject = true;
                    continue;
                }
                facts.Add(new AssessmentObjectFact(source, element.Location, element.Text,
                    new FactConfidence(FactConfidenceBand.High, 0.95m), canonical, interest, share, aliases));
            }
            // Do not silently discard an unresolved competing object and claim
            // the remaining equity assertion is the document's unique object.
            return (unresolvedObject ? new List<AssessmentObjectFact>() : facts).AsReadOnly();
        }

        private static string ResolveSubject(string text, AssessmentObjectSourceContext context,
            out IReadOnlyList<DocumentElement> evidence)
        {
            var names = new List<string>();
            var definitions = new List<DocumentElement>();
            evidence = definitions.AsReadOnly();
            // “的” may belong to a defined short name, not a possessive suffix.
            // Resolve both grammatical readings using explicit identity evidence;
            // never let regex greediness choose the entity.
            string[] readings = text.EndsWith("的", StringComparison.Ordinal)
                ? new[] { text, text.Substring(0, text.Length - 1) } : new[] { text };
            foreach (string reading in readings)
            {
                IReadOnlyList<DocumentElement> aliases;
                string canonical = context.ResolveEntityName(reading, out aliases);
                if (canonical == null) return null;
                if ((!Company.IsMatch(reading) && aliases.Count == 0) || !Company.IsMatch(canonical)) continue;
                names.Add(canonical);
                definitions.AddRange(aliases);
            }
            string[] unique = names.Distinct(StringComparer.Ordinal).ToArray();
            evidence = definitions.Distinct().ToList().AsReadOnly();
            return unique.Length == 1 ? unique[0] : null;
        }
    }
}
