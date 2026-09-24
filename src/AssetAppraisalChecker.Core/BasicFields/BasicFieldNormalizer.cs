using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public static class BasicFieldNormalizer
    {
        private static readonly Regex WhitespacePattern = new Regex(
            @"\s+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex DatePattern = new Regex(
            @"^(?:(?<year>\d{4})年(?<month>\d{1,2})月(?<day>\d{1,2})日|" +
            @"(?<year>\d{4})(?<separator>[-/.])(?<month>\d{1,2})\k<separator>(?<day>\d{1,2}))$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ReportIssueDateArabicPattern = new Regex(
            @"^(?<year>\d{4})年(?<month>\d{1,2})月(?<day>\d{1,2})日$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ReportIssueDateChinesePattern = new Regex(
            @"^(?<year>[〇○零一二三四五六七八九]{4})年" +
            @"(?<month>[〇○零一二三四五六七八九十]{1,3})月" +
            @"(?<day>[〇○零一二三四五六七八九十]{1,3})日$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ReportYearBracketPattern = new Regex(
            @"(?:\(|\[|【|〔)\s*(?<year>\d{4})\s*(?:\)|\]|】|〕)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static BasicFieldNormalizationResult Normalize(
            BasicFieldType fieldType,
            string rawValue)
        {
            switch (fieldType)
            {
                case BasicFieldType.ProjectName:
                    return NormalizeText(rawValue);
                case BasicFieldType.ReportNumber:
                    return NormalizeReportNumber(rawValue);
                case BasicFieldType.ValuationDate:
                    return NormalizeDate(rawValue);
                case BasicFieldType.ReportIssueDate:
                    return NormalizeReportIssueDate(rawValue);
                default:
                    return BasicFieldNormalizationResult.Failed("不支持的基础字段类型。");
            }
        }

        internal static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return WhitespacePattern.Replace(value.Replace('\u3000', ' '), " ").Trim();
        }

        private static BasicFieldNormalizationResult NormalizeText(string rawValue)
        {
            string value = NormalizeWhitespace(rawValue);
            if (value.Length == 0)
            {
                return BasicFieldNormalizationResult.Failed("字段值为空，无法形成标准值。");
            }

            return BasicFieldNormalizationResult.Succeeded(
                BasicFieldValue.FromText(value),
                "仅去除首尾空白并合并连续空白，未进行模糊匹配或语义改写。");
        }

        private static BasicFieldNormalizationResult NormalizeReportNumber(string rawValue)
        {
            string value = NormalizeWhitespace(rawValue);
            if (value.Length == 0)
            {
                return BasicFieldNormalizationResult.Failed("报告编号为空，无法形成标准值。");
            }

            value = value.Replace(" ", string.Empty)
                .Replace('（', '(')
                .Replace('）', ')');
            value = ReportYearBracketPattern.Replace(value, "〔${year}〕");

            return BasicFieldNormalizationResult.Succeeded(
                BasicFieldValue.FromText(value),
                "报告编号作为完整字符串处理，仅规范空白和明确的年份括号形式。");
        }

        private static BasicFieldNormalizationResult NormalizeDate(string rawValue)
        {
            string value = NormalizeWhitespace(rawValue);
            Match match = DatePattern.Match(value);
            if (!match.Success)
            {
                return BasicFieldNormalizationResult.Failed("无法解析为受支持的有效日期。");
            }

            int year;
            int month;
            int day;
            if (!int.TryParse(match.Groups["year"].Value, out year) ||
                !int.TryParse(match.Groups["month"].Value, out month) ||
                !int.TryParse(match.Groups["day"].Value, out day))
            {
                return BasicFieldNormalizationResult.Failed("无法解析为受支持的有效日期。");
            }

            try
            {
                DateTime date = new DateTime(year, month, day);
                return BasicFieldNormalizationResult.Succeeded(
                    BasicFieldValue.FromDate(date),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "日期已严格标准化为 {0:yyyy-MM-dd}。",
                        date));
            }
            catch (ArgumentOutOfRangeException)
            {
                return BasicFieldNormalizationResult.Failed("日期中的年、月或日无效，未生成虚假日期标准值。");
            }
        }

        private static BasicFieldNormalizationResult NormalizeReportIssueDate(string rawValue)
        {
            string value = NormalizeWhitespace(rawValue);
            Match match = ReportIssueDateArabicPattern.Match(value);
            int year;
            int month;
            int day;

            if (match.Success)
            {
                if (!int.TryParse(match.Groups["year"].Value, out year) ||
                    !int.TryParse(match.Groups["month"].Value, out month) ||
                    !int.TryParse(match.Groups["day"].Value, out day))
                {
                    return BasicFieldNormalizationResult.Failed("无法解析为受支持的评估报告日。");
                }
            }
            else
            {
                match = ReportIssueDateChinesePattern.Match(value);
                if (!match.Success ||
                    !TryParseChineseYear(match.Groups["year"].Value, out year) ||
                    !TryParseChineseCardinal(match.Groups["month"].Value, out month) ||
                    !TryParseChineseCardinal(match.Groups["day"].Value, out day))
                {
                    return BasicFieldNormalizationResult.Failed("无法解析为受支持的评估报告日。");
                }
            }

            try
            {
                DateTime date = new DateTime(year, month, day);
                return BasicFieldNormalizationResult.Succeeded(
                    BasicFieldValue.FromDate(date),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "评估报告日已严格标准化为 {0:yyyy-MM-dd}。",
                        date));
            }
            catch (ArgumentOutOfRangeException)
            {
                return BasicFieldNormalizationResult.Failed(
                    "评估报告日中的年、月或日无效，未生成虚假日期标准值。");
            }
        }

        private static bool TryParseChineseYear(string value, out int year)
        {
            year = 0;
            if (string.IsNullOrEmpty(value) || value.Length != 4)
            {
                return false;
            }

            foreach (char character in value)
            {
                int digit;
                if (!TryGetChineseDigit(character, out digit))
                {
                    return false;
                }

                year = year * 10 + digit;
            }

            return true;
        }

        private static bool TryParseChineseCardinal(string value, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int tensIndex = value.IndexOf('十');
            if (tensIndex < 0)
            {
                return value.Length == 1 && TryGetChineseDigit(value[0], out number);
            }

            if (value.LastIndexOf('十') != tensIndex)
            {
                return false;
            }

            int tens;
            int units;
            if (tensIndex == 0)
            {
                if (value.Length == 1)
                {
                    number = 10;
                    return true;
                }

                if (value.Length == 2 &&
                    TryGetChineseDigit(value[1], out units))
                {
                    number = 10 + units;
                    return true;
                }

                return false;
            }

            if (tensIndex != 1 ||
                !TryGetChineseDigit(value[0], out tens) ||
                tens == 0)
            {
                return false;
            }

            if (value.Length == 2)
            {
                number = tens * 10;
                return true;
            }

            if (value.Length == 3 &&
                TryGetChineseDigit(value[2], out units))
            {
                number = tens * 10 + units;
                return true;
            }

            return false;
        }

        private static bool TryGetChineseDigit(char character, out int digit)
        {
            const string Digits = "〇一二三四五六七八九";
            if (character == '○' || character == '零')
            {
                digit = 0;
                return true;
            }

            digit = Digits.IndexOf(character);
            return digit >= 0;
        }
    }
}
