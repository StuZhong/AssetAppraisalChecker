using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    public sealed class AssessmentObjectFactExtractor
    {
        private static readonly string[] SupportedAssetTypes =
        {
            "供配电资产",
            "房屋建筑物",
            "机器设备"
        };

        private static readonly string[] SupportedEconomicActions =
        {
            "接收",
            "转让",
            "购买",
            "投资"
        };

        private static readonly string[] OwnerPrefixes =
        {
            "本次评估对象为",
            "本次评估对象是",
            "评估对象为",
            "评估对象是",
            "产权持有人为",
            "产权持有人是",
            "所有权人为",
            "所有权人是",
            "权利人为",
            "权利人是",
            "本次由",
            "由"
        };

        private static readonly Regex QuantityPattern = new Regex(
            @"(?<![\d.])(?<value>\d+(?:\.\d+)?)\s*(?<unit>家客户|台设备)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex OwnerLabelPattern = new Regex(
            @"(?:产权持有人|所有权人|权利人)\s*(?:为|是|：|:)\s*(?<segment>[^，。；;]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ActionContextPattern = new Regex(
            @"(?:^|[，。；;])(?<segment>[^，。；;]{0,80}?)(?:拟)?(?:接收|转让|购买|投资)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex CompanyOrUnitPattern = new Regex(
            @"(?<owner>[A-Za-z0-9\u4e00-\u9fff（）()·]{2,40}?(?:公司|单位))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ScopePattern = new Regex(
            @"(?:^|[，,])\s*(?:评估对象范围|对象范围|范围)\s*(?:为|是|：|:)\s*(?<scope>[^，。；;]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public IReadOnlyList<AssessmentObjectFact> Extract(
            DocumentReadResult documentReadResult,
            string sourceDocument)
        {
            if (documentReadResult == null)
            {
                throw new ArgumentNullException(nameof(documentReadResult));
            }

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException(
                    "事实来源文档不能为空。",
                    nameof(sourceDocument));
            }

            var facts = new List<AssessmentObjectFact>();
            var sourceContext = new AssessmentObjectSourceContext(documentReadResult);
            facts.AddRange(EquityAssessmentObjectExtractor.Extract(documentReadResult, sourceDocument, sourceContext));
            foreach (DocumentElement element in documentReadResult.Elements
                .OrderBy(item => item.DocumentOrderIndex))
            {
                AssessmentObjectFact fact = TryExtract(element, sourceDocument, sourceContext);
                if (fact != null)
                {
                    facts.Add(fact);
                }
            }

            return new ReadOnlyCollection<AssessmentObjectFact>(facts);
        }

        private static AssessmentObjectFact TryExtract(
            DocumentElement element,
            string sourceDocument,
            AssessmentObjectSourceContext sourceContext)
        {
            string text = AssessmentObjectSourceContext.SelectObjectStatement(element.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            IReadOnlyList<string> assetTypes = FindDistinctSupportedValues(
                text,
                SupportedAssetTypes);
            IReadOnlyList<string> owners = FindOwnerCandidates(text);
            IReadOnlyList<string> economicActions = FindEconomicActions(text);
            IReadOnlyList<QuantityMatch> quantities = FindQuantities(text);

            if (assetTypes.Count != 1 ||
                owners.Count != 1 ||
                economicActions.Count != 1 ||
                quantities.Count != 1)
            {
                return null;
            }

            IReadOnlyList<string> explicitScopes = FindExplicitScopes(text);
            if (explicitScopes.Count > 1)
            {
                return null;
            }

            QuantityMatch quantity = quantities[0];
            IReadOnlyList<DocumentElement> aliasEvidence;
            string owner = sourceContext.ResolveEntityName(owners[0], out aliasEvidence);
            if (owner == null) return null;
            string scope = explicitScopes.Count == 1
                ? explicitScopes[0]
                : quantity.OriginalText;

            return new AssessmentObjectFact(
                sourceDocument,
                element.Location,
                element.Text,
                new FactConfidence(FactConfidenceBand.High, 0.95m),
                assetTypes[0],
                owner,
                economicActions[0],
                quantity.Value,
                scope,
                aliasEvidence);
        }

        private static IReadOnlyList<string> FindDistinctSupportedValues(
            string text,
            IEnumerable<string> supportedValues)
        {
            return supportedValues
                .Where(value => text.IndexOf(value, StringComparison.Ordinal) >= 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> FindEconomicActions(string text)
        {
            var matches = new List<string>();
            foreach (string action in SupportedEconomicActions)
            {
                int searchStart = 0;
                while (searchStart < text.Length)
                {
                    int matchStart = text.IndexOf(
                        action,
                        searchStart,
                        StringComparison.Ordinal);
                    if (matchStart < 0)
                    {
                        break;
                    }

                    if (!IsImmediatelyNegated(text, matchStart))
                    {
                        matches.Add(action);
                    }

                    searchStart = matchStart + action.Length;
                }
            }

            return matches
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsImmediatelyNegated(string text, int matchStart)
        {
            if (matchStart == 0)
            {
                return false;
            }

            char previous = text[matchStart - 1];
            return previous == '不' || previous == '未' || previous == '无';
        }

        private static IReadOnlyList<QuantityMatch> FindQuantities(string text)
        {
            var quantities = new List<QuantityMatch>();
            foreach (Match match in QuantityPattern.Matches(text))
            {
                decimal value;
                if (!decimal.TryParse(
                    match.Groups["value"].Value,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out value))
                {
                    continue;
                }

                quantities.Add(new QuantityMatch(
                    value,
                    match.Groups["unit"].Value,
                    match.Value));
            }

            return quantities
                .GroupBy(
                    quantity => string.Concat(
                        quantity.Value.ToString(CultureInfo.InvariantCulture),
                        "|",
                        quantity.Unit),
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static IReadOnlyList<string> FindOwnerCandidates(string text)
        {
            var candidates = new List<string>();

            foreach (Match match in OwnerLabelPattern.Matches(text))
            {
                AddOwnerCandidates(match.Groups["segment"].Value, candidates);
            }

            foreach (Match match in ActionContextPattern.Matches(text))
            {
                AddOwnerCandidates(match.Groups["segment"].Value, candidates);
            }

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static void AddOwnerCandidates(
            string segment,
            ICollection<string> candidates)
        {
            foreach (Match match in CompanyOrUnitPattern.Matches(segment))
            {
                string candidate = NormalizeOwner(match.Groups["owner"].Value);
                if (IsConcreteOwner(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        private static string NormalizeOwner(string value)
        {
            string normalized = value.Trim();
            bool prefixRemoved;
            do
            {
                prefixRemoved = false;
                foreach (string prefix in OwnerPrefixes)
                {
                    if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        normalized = normalized.Substring(prefix.Length).Trim();
                        prefixRemoved = true;
                        break;
                    }
                }
            }
            while (prefixRemoved);

            return normalized.TrimStart(' ', '，', '、', '：', ':', '和', '与', '及');
        }

        private static bool IsConcreteOwner(string candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate) &&
                   !string.Equals(candidate, "公司", StringComparison.Ordinal) &&
                   !string.Equals(candidate, "单位", StringComparison.Ordinal) &&
                   !string.Equals(candidate, "评估单位", StringComparison.Ordinal) &&
                   !string.Equals(candidate, "委托单位", StringComparison.Ordinal) &&
                   !string.Equals(candidate, "被评估单位", StringComparison.Ordinal);
        }

        private static IReadOnlyList<string> FindExplicitScopes(string text)
        {
            return ScopePattern
                .Matches(text)
                .Cast<Match>()
                .Select(match => match.Groups["scope"].Value.Trim())
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private sealed class QuantityMatch
        {
            public QuantityMatch(decimal value, string unit, string originalText)
            {
                Value = value;
                Unit = unit;
                OriginalText = originalText;
            }

            public decimal Value { get; }

            public string Unit { get; }

            public string OriginalText { get; }
        }
    }
}
