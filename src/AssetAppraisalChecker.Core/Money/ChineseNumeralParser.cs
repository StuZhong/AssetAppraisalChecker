using System;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.Money
{
    internal static class ChineseNumeralParser
    {
        private const string DigitCharacters = "零〇一二两三四五六七八九壹贰叁肆伍陆柒捌玖";

        private static readonly Regex FractionPattern = new Regex(
            "^(?:[零〇])*(?:(?<jiao>[" + DigitCharacters + "])角)?(?:[零〇])*(?:(?<fen>[" + DigitCharacters + "])分)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryParseInteger(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            decimal total = 0m;
            decimal section = 0m;
            decimal pendingNumber = 0m;
            decimal lastSmallUnit = 10000m;
            decimal lastLargeUnit = decimal.MaxValue;
            bool hasPendingNumber = false;
            bool previousWasZero = false;
            bool sawToken = false;

            try
            {
                foreach (char character in text)
                {
                    int digit;
                    if (TryGetDigit(character, out digit))
                    {
                        sawToken = true;
                        if (digit == 0)
                        {
                            // 零是占位符，不是新的数位；连续的非零数字则无法可靠解释为金额。
                            if (hasPendingNumber)
                            {
                                return false;
                            }

                            previousWasZero = true;
                            continue;
                        }

                        if (hasPendingNumber)
                        {
                            return false;
                        }

                        pendingNumber = digit;
                        hasPendingNumber = true;
                        previousWasZero = false;
                        continue;
                    }

                    decimal smallUnit;
                    if (TryGetSmallUnit(character, out smallUnit))
                    {
                        sawToken = true;
                        if (smallUnit >= lastSmallUnit || previousWasZero)
                        {
                            return false;
                        }

                        // 只有“十/拾”允许省略前导“一”；把“百元”直接猜成 100 元会产生误报。
                        if (!hasPendingNumber && smallUnit != 10m)
                        {
                            return false;
                        }

                        decimal factor = hasPendingNumber ? pendingNumber : 1m;
                        section = checked(section + checked(factor * smallUnit));
                        pendingNumber = 0m;
                        hasPendingNumber = false;
                        previousWasZero = false;
                        lastSmallUnit = smallUnit;
                        continue;
                    }

                    decimal largeUnit;
                    if (TryGetLargeUnit(character, out largeUnit))
                    {
                        sawToken = true;
                        if (largeUnit >= lastLargeUnit)
                        {
                            return false;
                        }

                        if (hasPendingNumber)
                        {
                            section = checked(section + pendingNumber);
                        }

                        if (section == 0m)
                        {
                            return false;
                        }

                        total = checked(total + checked(section * largeUnit));
                        section = 0m;
                        pendingNumber = 0m;
                        hasPendingNumber = false;
                        previousWasZero = false;
                        lastSmallUnit = 10000m;
                        lastLargeUnit = largeUnit;
                        continue;
                    }

                    return false;
                }

                if (!sawToken)
                {
                    return false;
                }

                if (hasPendingNumber)
                {
                    section = checked(section + pendingNumber);
                }

                value = checked(total + section);
                return true;
            }
            catch (OverflowException)
            {
                value = 0m;
                return false;
            }
        }

        public static bool TryParseFraction(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            Match match = FractionPattern.Match(text);
            if (!match.Success || (!match.Groups["jiao"].Success && !match.Groups["fen"].Success))
            {
                return false;
            }

            int jiao = 0;
            int fen = 0;
            if (match.Groups["jiao"].Success && !TryGetDigit(match.Groups["jiao"].Value[0], out jiao))
            {
                return false;
            }

            if (match.Groups["fen"].Success && !TryGetDigit(match.Groups["fen"].Value[0], out fen))
            {
                return false;
            }

            value = (jiao / 10m) + (fen / 100m);
            return true;
        }

        public static bool IsDigit(char character)
        {
            int ignored;
            return TryGetDigit(character, out ignored);
        }

        private static bool TryGetDigit(char character, out int digit)
        {
            switch (character)
            {
                case '零':
                case '〇':
                    digit = 0;
                    return true;
                case '一':
                case '壹':
                    digit = 1;
                    return true;
                case '二':
                case '两':
                case '贰':
                    digit = 2;
                    return true;
                case '三':
                case '叁':
                    digit = 3;
                    return true;
                case '四':
                case '肆':
                    digit = 4;
                    return true;
                case '五':
                case '伍':
                    digit = 5;
                    return true;
                case '六':
                case '陆':
                    digit = 6;
                    return true;
                case '七':
                case '柒':
                    digit = 7;
                    return true;
                case '八':
                case '捌':
                    digit = 8;
                    return true;
                case '九':
                case '玖':
                    digit = 9;
                    return true;
                default:
                    digit = 0;
                    return false;
            }
        }

        private static bool TryGetSmallUnit(char character, out decimal unit)
        {
            switch (character)
            {
                case '十':
                case '拾':
                    unit = 10m;
                    return true;
                case '百':
                case '佰':
                    unit = 100m;
                    return true;
                case '千':
                case '仟':
                    unit = 1000m;
                    return true;
                default:
                    unit = 0m;
                    return false;
            }
        }

        private static bool TryGetLargeUnit(char character, out decimal unit)
        {
            switch (character)
            {
                case '万':
                case '萬':
                    unit = 10000m;
                    return true;
                case '亿':
                case '億':
                    unit = 100000000m;
                    return true;
                default:
                    unit = 0m;
                    return false;
            }
        }
    }
}
