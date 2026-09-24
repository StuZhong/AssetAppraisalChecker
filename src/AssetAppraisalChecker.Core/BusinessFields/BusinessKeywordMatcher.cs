using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class BusinessKeywordMatcher
    {
        private static readonly ValueRule[] ValueRules =
        {
            // More specific and overlapping terms must precede their general forms.
            new ValueRule(ValueCategory.AppraisedOriginalValue, "评估值原值", "评估原值"),
            new ValueRule(ValueCategory.AppraisedNetValue, "评估净值"),
            new ValueRule(ValueCategory.BookOriginalValue, "账面原值"),
            new ValueRule(ValueCategory.BookNetValue, "账面净值"),
            new ValueRule(ValueCategory.BookValue, "账面价值", "账面值"),
            new ValueRule(ValueCategory.ReplacementCost, "重置成本", "重置全价"),
            new ValueRule(ValueCategory.Increment, "评估增值额", "增值额"),
            new ValueRule(ValueCategory.Decrement, "评估减值额", "减值额"),
            new ValueRule(ValueCategory.ConstructionCost, "建筑安装工程费", "建安工程费"),
            new ValueRule(ValueCategory.OtherCost, "其他费用"),
            new ValueRule(ValueCategory.CapitalCost, "资金成本"),
            new ValueRule(ValueCategory.Tax, "增值税额", "税费"),
            new ValueRule(ValueCategory.AppraisedValue, "评估价值", "评估值", "评估结果")
        };

        public static bool TryClassifyValue(
            string text,
            out ValueCategory category,
            out string matchedKeyword)
        {
            foreach (ValueRule rule in ValueRules)
            {
                foreach (string keyword in rule.Keywords)
                {
                    if (Contains(text, keyword))
                    {
                        category = rule.Category;
                        matchedKeyword = keyword;
                        return true;
                    }
                }
            }

            category = ValueCategory.Unknown;
            matchedKeyword = string.Empty;
            return false;
        }

        public static bool TryClassifyTaxBasis(
            string text,
            out TaxBasis taxBasis,
            out string matchedKeyword)
        {
            // Exclusions contain the substring “含税”, so negative phrases must win first.
            string[] excludedKeywords = { "不含增值税", "不含税" };
            foreach (string keyword in excludedKeywords)
            {
                if (Contains(text, keyword))
                {
                    taxBasis = TaxBasis.TaxExcluded;
                    matchedKeyword = keyword;
                    return true;
                }
            }

            string[] includedKeywords = { "含增值税", "含税" };
            foreach (string keyword in includedKeywords)
            {
                if (Contains(text, keyword))
                {
                    taxBasis = TaxBasis.TaxIncluded;
                    matchedKeyword = keyword;
                    return true;
                }
            }

            taxBasis = TaxBasis.Unspecified;
            matchedKeyword = string.Empty;
            return false;
        }

        public static bool TryClassifyScope(string text, out Scope scope, out string matchedKeyword)
        {
            string[] calculationKeywords = { "测算案例", "计算案例", "测算过程", "计算过程", "案例", "示例" };
            if (TryMatch(text, calculationKeywords, out matchedKeyword))
            {
                scope = Scope.CalculationCase;
                return true;
            }

            string[] projectTotalKeywords =
            {
                "评估结果合计", "评估结果汇总", "整体评估结果", "全部评估对象",
                "本次评估项目", "项目评估值", "项目总值", "项目合计",
                "评估总值", "本项目", "总计"
            };
            if (TryMatch(text, projectTotalKeywords, out matchedKeyword))
            {
                scope = Scope.ProjectTotal;
                return true;
            }

            string[] subtotalKeywords = { "小计", "合计" };
            if (TryMatch(text, subtotalKeywords, out matchedKeyword))
            {
                scope = Scope.Subtotal;
                return true;
            }

            string[] singleAssetKeywords = { "单项资产", "单台" };
            if (TryMatch(text, singleAssetKeywords, out matchedKeyword))
            {
                scope = Scope.SingleAsset;
                return true;
            }

            scope = Scope.Unspecified;
            matchedKeyword = string.Empty;
            return false;
        }

        private static bool TryMatch(string text, string[] keywords, out string matchedKeyword)
        {
            foreach (string keyword in keywords)
            {
                if (Contains(text, keyword))
                {
                    matchedKeyword = keyword;
                    return true;
                }
            }

            matchedKeyword = string.Empty;
            return false;
        }

        private static bool Contains(string text, string keyword)
        {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf(keyword, StringComparison.Ordinal) >= 0;
        }

        private sealed class ValueRule
        {
            public ValueRule(ValueCategory category, params string[] keywords)
            {
                Category = category;
                Keywords = keywords;
            }

            public ValueCategory Category { get; }

            public string[] Keywords { get; }
        }
    }
}
