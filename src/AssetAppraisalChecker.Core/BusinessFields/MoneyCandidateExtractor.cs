using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    public static class MoneyCandidateExtractor
    {
        private const string ChineseNumeralCharacters =
            "零〇一二两三四五六七八九壹贰叁肆伍陆柒捌玖十百千万亿拾佰仟萬億";

        private static readonly Regex ArabicCandidatePattern = new Regex(
            @"(?<![\dA-Za-z,.])(?:(?<prefix>人民币|RMB|￥|¥)\s*)?(?<number>(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)(?:\s*(?<unit>亿元|万元|元|圆))?(?:\s*(?<whole>整|正))?(?![\dA-Za-z,.])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex ChineseCandidatePattern = new Regex(
            @"(?<![" + ChineseNumeralCharacters + @"])(?<amount>(?:人民币\s*)?[" +
            ChineseNumeralCharacters + @"]+(?:亿元|万元|元|圆)(?:[" +
            ChineseNumeralCharacters + @"]+角)?(?:[" +
            ChineseNumeralCharacters + @"]+分)?(?:整|正)?)(?![" +
            ChineseNumeralCharacters + @"角分整正])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<MoneyCandidate> NoCandidates =
            new ReadOnlyCollection<MoneyCandidate>(new MoneyCandidate[0]);

        public static IReadOnlyList<MoneyCandidate> Extract(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return NoCandidates;
            }

            var candidates = new List<MoneyCandidate>();
            foreach (Match match in ArabicCandidatePattern.Matches(text))
            {
                if (!match.Groups["prefix"].Success && !match.Groups["unit"].Success)
                {
                    continue;
                }

                candidates.Add(new MoneyCandidate(match.Value, match.Index, match.Length));
            }

            foreach (Match match in ChineseCandidatePattern.Matches(text))
            {
                Group amount = match.Groups["amount"];
                candidates.Add(new MoneyCandidate(amount.Value, amount.Index, amount.Length));
            }

            var ordered = candidates
                .OrderBy(candidate => candidate.StartIndex)
                .ThenByDescending(candidate => candidate.Length)
                .ToList();

            var nonOverlapping = new List<MoneyCandidate>();
            foreach (MoneyCandidate candidate in ordered)
            {
                if (nonOverlapping.Any(existing => Overlaps(existing, candidate)))
                {
                    continue;
                }

                nonOverlapping.Add(candidate);
            }

            return new ReadOnlyCollection<MoneyCandidate>(nonOverlapping);
        }

        private static bool Overlaps(MoneyCandidate left, MoneyCandidate right)
        {
            int leftEnd = left.StartIndex + left.Length;
            int rightEnd = right.StartIndex + right.Length;
            return left.StartIndex < rightEnd && right.StartIndex < leftEnd;
        }
    }
}
