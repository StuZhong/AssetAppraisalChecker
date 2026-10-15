using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Mechanical.Units
{
    public sealed class UnitParser
    {
        private const string NumberPattern = @"(?<![A-Za-z0-9_])[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?\s*";
        private readonly UnitNormalizer _normalizer = new UnitNormalizer();
        private readonly Regex _numericUnitRegex;

        public UnitParser()
        {
            string aliases = string.Join(
                "|",
                UnitNormalizer.KnownAliases
                    .OrderByDescending(item => item.Length)
                    .Select(Regex.Escape));
            string currency = string.Join(
                "|",
                new[] { "人民币万元", "人民币元", "万元", "元" }.Select(Regex.Escape));
            string unknownDenominator = @"[A-Za-z\u4e00-\u9fff㎡²³0-9]+";
            string compound = @"(?:" + currency + @")\s*(?:/|／|每)\s*(?:" + aliases + "|" + unknownDenominator + ")";

            _numericUnitRegex = new Regex(
                NumberPattern + @"(?<unit>" + compound + "|" + aliases + @")(?![A-Za-z])",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }

        public bool TryParse(string text, out UnitExpression expression)
        {
            expression = null;
            string surface = UnitNormalizer.NormalizeSurface(text);
            if (surface.Length == 0)
            {
                return false;
            }

            int separator = surface.IndexOf('/');
            if (separator < 0)
            {
                separator = surface.IndexOf('每');
            }

            if (separator < 0)
            {
                if (!_normalizer.TryNormalize(text, out UnitToken atomic))
                {
                    return false;
                }

                expression = UnitExpression.Atomic(atomic);
                return true;
            }

            string numeratorText = surface.Substring(0, separator);
            string denominatorText = surface.Substring(separator + 1);
            if (!_normalizer.TryNormalize(numeratorText, out UnitToken numerator) ||
                numerator.Family != UnitFamily.Currency)
            {
                return false;
            }

            UnitToken denominator;
            if (!_normalizer.TryNormalize(denominatorText, out denominator))
            {
                denominator = new UnitToken(
                    denominatorText,
                    "Unknown(" + denominatorText + ")",
                    UnitFamily.Unknown,
                    1m);
            }

            expression = UnitExpression.Compound(text.Trim(), numerator, denominator);
            return true;
        }

        public IReadOnlyList<UnitExpression> FindExpressions(string text)
        {
            var expressions = new List<UnitExpression>();
            MatchCollection dates = Regex.Matches(text ?? string.Empty,
                @"(?:\d{4}\s*年(?:\s*\d{1,2}\s*月(?:\s*\d{1,2}\s*日)?)?|\d{1,2}\s*月\s*\d{1,2}\s*日)");
            foreach (Match match in _numericUnitRegex.Matches(text ?? string.Empty))
            {
                // Dates and regulation editions are not quantity observations.
                if (dates.Cast<Match>().Any(date => match.Index >= date.Index &&
                    match.Index + match.Length <= date.Index + date.Length)) continue;
                string unit = match.Groups["unit"].Value;
                string suffix = (text ?? string.Empty).Substring(match.Index + match.Length);
                if ((unit == "年" || unit == "月" || unit == "日") &&
                    (Regex.IsMatch(match.Value, @"^\d{4}\s*年$") ||
                     Regex.IsMatch(suffix, @"^(版|修订|颁布|发布|\d{1,2}[月日])"))) continue;
                if (TryParse(match.Groups["unit"].Value, out UnitExpression expression))
                {
                    expressions.Add(expression);
                }
            }

            return new ReadOnlyCollection<UnitExpression>(expressions);
        }
    }
}
