using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.BasicFields
{
    internal static class StandaloneReportNumberMatcher
    {
        private const string HorizontalWhitespace = @"[ \t\u3000]*";
        private const string RestrictedPrefix = @"[\p{L}\p{N}]{0,20}?";
        private const string PairedYearBoundary =
            @"(?:〔[ \t\u3000]*\d{4}[ \t\u3000]*〕|" +
            @"（[ \t\u3000]*\d{4}[ \t\u3000]*）|" +
            @"\([ \t\u3000]*\d{4}[ \t\u3000]*\)|" +
            @"\[[ \t\u3000]*\d{4}[ \t\u3000]*\]|" +
            @"【[ \t\u3000]*\d{4}[ \t\u3000]*】)";

        private static readonly string[] BusinessMarkers =
        {
            "资评报字",
            "评报字"
        };

        private static readonly Regex WholeParagraphPattern = new Regex(
            @"^(?<leading>" + HorizontalWhitespace + @")(?<number>" +
            RestrictedPrefix +
            @"(?<marker>" + string.Join(
                "|",
                BusinessMarkers.Select(Regex.Escape)) + @")" +
            HorizontalWhitespace + PairedYearBoundary + HorizontalWhitespace +
            @"第" + HorizontalWhitespace + @"\d{1,8}" + HorizontalWhitespace +
            @"号)(?<trailing>" + HorizontalWhitespace + @")$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex AuthoritativeAlphanumericWholeParagraphPattern = new Regex(
            @"^(?<leading>" + HorizontalWhitespace + @")(?<number>" +
            RestrictedPrefix +
            @"(?<marker>" + string.Join(
                "|",
                BusinessMarkers.Select(Regex.Escape)) + @")" +
            HorizontalWhitespace + PairedYearBoundary + HorizontalWhitespace +
            @"第(?<sequence>[A-Za-z][0-9]{3})号)(?<trailing>" +
            HorizontalWhitespace + @")$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryMatch(string text, out int startIndex, out int length)
        {
            Match match = WholeParagraphPattern.Match(text ?? string.Empty);
            if (!match.Success)
            {
                startIndex = 0;
                length = 0;
                return false;
            }

            Group number = match.Groups["number"];
            startIndex = number.Index;
            length = number.Length;
            return true;
        }

        internal static bool TryMatchAuthoritativeAlphanumeric(
            string text,
            out int startIndex,
            out int length)
        {
            Match match = AuthoritativeAlphanumericWholeParagraphPattern.Match(
                text ?? string.Empty);
            if (!match.Success)
            {
                startIndex = 0;
                length = 0;
                return false;
            }

            Group number = match.Groups["number"];
            startIndex = number.Index;
            length = number.Length;
            return true;
        }
    }
}
