namespace AssetAppraisalChecker.Core.Money
{
    public static class MoneyNormalizer
    {
        public static MoneyParseResult Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return MoneyParseResult.Failed(text, MoneyNumberFormat.Unknown, "金额文本为空。");
            }

            foreach (char character in text)
            {
                if (character >= '0' && character <= '9')
                {
                    return ArabicMoneyParser.Parse(text);
                }
            }

            return ChineseMoneyParser.Parse(text);
        }

        public static bool TryParse(string text, out MoneyParseResult result)
        {
            result = Parse(text);
            return result.IsSuccess;
        }
    }
}
