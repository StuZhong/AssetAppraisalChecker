using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.Money
{
    public static class ArabicMoneyParser
    {
        private static readonly Regex ArabicAmountPattern = new Regex(
            @"^(?<prefix>人民币|RMB|￥|¥)?\s*(?<number>(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*(?<unit>亿元|万元|元|圆)?\s*(?<whole>整|正)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static MoneyParseResult Parse(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Arabic, "金额文本为空。");
            }

            Match match = ArabicAmountPattern.Match(rawText.Trim());
            if (!match.Success)
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Arabic, "文本不是受支持的阿拉伯数字金额格式。");
            }

            string prefix = match.Groups["prefix"].Value;
            string unitText = match.Groups["unit"].Value;
            if (prefix.Length == 0 && unitText.Length == 0)
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Arabic, "缺少货币符号或金额单位，无法确认该数字是金额。");
            }

            if (match.Groups["whole"].Success && unitText.Length == 0)
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Arabic, "“整”或“正”之前缺少人民币金额单位。");
            }

            string normalizedNumber = match.Groups["number"].Value.Replace(",", string.Empty);
            decimal amount;
            if (!decimal.TryParse(
                normalizedNumber,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount))
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Arabic, "数字超出 decimal 范围或格式无效。");
            }

            MoneyUnit unit = DetectUnit(unitText);
            try
            {
                decimal normalizedAmount = MoneyUnitConverter.ConvertToYuan(amount, unit);
                return MoneyParseResult.Succeeded(
                    rawText,
                    normalizedAmount,
                    unit,
                    MoneyNumberFormat.Arabic,
                    new List<string>());
            }
            catch (OverflowException)
            {
                return MoneyParseResult.Failed(rawText, MoneyNumberFormat.Arabic, "金额换算后超出 decimal 可表示范围。");
            }
        }

        private static MoneyUnit DetectUnit(string unitText)
        {
            switch (unitText)
            {
                case "万元":
                    return MoneyUnit.TenThousandYuan;
                case "亿元":
                    return MoneyUnit.HundredMillionYuan;
                default:
                    return MoneyUnit.Yuan;
            }
        }
    }
}
