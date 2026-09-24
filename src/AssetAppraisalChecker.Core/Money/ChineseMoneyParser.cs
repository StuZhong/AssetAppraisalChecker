using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.Money
{
    public static class ChineseMoneyParser
    {
        private const string FinancialSpecificCharacters = "壹贰叁肆伍陆柒捌玖拾佰仟萬億";
        private const string CommonDigitCharacters = "一二两三四五六七八九";

        private static readonly Regex WhitespacePattern = new Regex(
            @"\s+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex AllowedAmountCharactersPattern = new Regex(
            @"^[零〇一二两三四五六七八九壹贰叁肆伍陆柒捌玖十百千万亿拾佰仟萬億元圆角分整正]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static MoneyParseResult Parse(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Unknown, "金额文本为空。");
            }

            string text = WhitespacePattern.Replace(rawText.Trim(), string.Empty);
            if (text.StartsWith("人民币", StringComparison.Ordinal))
            {
                text = text.Substring("人民币".Length);
            }

            MoneyNumberFormat numberFormat = DetectNumberFormat(text);
            if (text.Length == 0 || !AllowedAmountCharactersPattern.IsMatch(text))
            {
                return MoneyParseResult.Failed(rawText, numberFormat, "文本包含不受支持的中文金额字符。");
            }

            bool hasNumeral = false;
            foreach (char character in text)
            {
                if (ChineseNumeralParser.IsDigit(character))
                {
                    hasNumeral = true;
                    break;
                }
            }

            if (!hasNumeral)
            {
                return MoneyParseResult.Failed(rawText, numberFormat, "中文金额中没有数字字符。");
            }

            bool hasWholeSuffix = text.EndsWith("整", StringComparison.Ordinal) ||
                                  text.EndsWith("正", StringComparison.Ordinal);
            if (hasWholeSuffix)
            {
                text = text.Substring(0, text.Length - 1);
            }

            if (text.IndexOf('整') >= 0 || text.IndexOf('正') >= 0)
            {
                return MoneyParseResult.Failed(rawText, numberFormat, "“整”或“正”只能出现在金额末尾。");
            }

            int yuanIndex = FindYuanIndex(text);
            string integerText;
            string fractionText;
            if (yuanIndex >= 0)
            {
                if (FindYuanIndex(text.Substring(yuanIndex + 1)) >= 0)
                {
                    return MoneyParseResult.Failed(rawText, numberFormat, "金额中出现了多个元单位。");
                }

                integerText = text.Substring(0, yuanIndex);
                fractionText = text.Substring(yuanIndex + 1);
            }
            else
            {
                if (text.IndexOf('角') < 0 && text.IndexOf('分') < 0)
                {
                    return MoneyParseResult.Failed(rawText, numberFormat, "中文数字后缺少元、圆、角或分单位。");
                }

                integerText = string.Empty;
                fractionText = text;
            }

            if (hasWholeSuffix && fractionText.Length > 0)
            {
                return MoneyParseResult.Failed(rawText, numberFormat, "带角分的金额不能同时使用“整”或“正”。");
            }

            decimal integerAmount = 0m;
            if (integerText.Length > 0 && !ChineseNumeralParser.TryParseInteger(integerText, out integerAmount))
            {
                return MoneyParseResult.Failed(rawText, numberFormat, "中文整数部分的数位或单位顺序无法可靠解析。");
            }

            decimal fractionAmount;
            if (!ChineseNumeralParser.TryParseFraction(fractionText, out fractionAmount))
            {
                return MoneyParseResult.Failed(rawText, numberFormat, "角分部分不是受支持的中文金额格式。");
            }

            List<string> warnings = CreateFormatWarnings(text, numberFormat);
            MoneyUnit detectedUnit = DetectUnit(integerText);
            return MoneyParseResult.Succeeded(
                rawText,
                checked(integerAmount + fractionAmount),
                detectedUnit,
                numberFormat,
                warnings);
        }

        private static int FindYuanIndex(string text)
        {
            int yuanIndex = text.IndexOf('元');
            int roundIndex = text.IndexOf('圆');
            if (yuanIndex < 0)
            {
                return roundIndex;
            }

            if (roundIndex < 0)
            {
                return yuanIndex;
            }

            return Math.Min(yuanIndex, roundIndex);
        }

        private static MoneyUnit DetectUnit(string integerText)
        {
            if (integerText.EndsWith("万", StringComparison.Ordinal) ||
                integerText.EndsWith("萬", StringComparison.Ordinal))
            {
                return MoneyUnit.TenThousandYuan;
            }

            if (integerText.EndsWith("亿", StringComparison.Ordinal) ||
                integerText.EndsWith("億", StringComparison.Ordinal))
            {
                return MoneyUnit.HundredMillionYuan;
            }

            return MoneyUnit.Yuan;
        }

        private static MoneyNumberFormat DetectNumberFormat(string text)
        {
            if (ContainsAny(text, FinancialSpecificCharacters))
            {
                return MoneyNumberFormat.ChineseFinancial;
            }

            return ContainsAny(text, "零〇一二两三四五六七八九十百千万亿")
                ? MoneyNumberFormat.ChineseCommon
                : MoneyNumberFormat.Unknown;
        }

        private static List<string> CreateFormatWarnings(string text, MoneyNumberFormat numberFormat)
        {
            List<string> warnings = new List<string>();
            if (numberFormat != MoneyNumberFormat.ChineseFinancial)
            {
                return warnings;
            }

            AddCommonUnitWarning(text, '十', '拾', warnings);
            AddCommonUnitWarning(text, '百', '佰', warnings);
            AddCommonUnitWarning(text, '千', '仟', warnings);

            if (ContainsAny(text, CommonDigitCharacters) && ContainsAny(text, "壹贰叁肆伍陆柒捌玖"))
            {
                warnings.Add("中文金额混用了普通数字和财务大写数字。");
            }

            return warnings;
        }

        private static void AddCommonUnitWarning(
            string text,
            char commonUnit,
            char financialUnit,
            ICollection<string> warnings)
        {
            if (text.IndexOf(commonUnit) >= 0)
            {
                warnings.Add(
                    string.Format(
                        "中文财务大写格式可能不规范：使用‘{0}’而非‘{1}’。",
                        commonUnit,
                        financialUnit));
            }
        }

        private static bool ContainsAny(string text, string characters)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.IndexOfAny(characters.ToCharArray()) >= 0;
        }
    }
}
