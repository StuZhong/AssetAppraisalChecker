using AssetAppraisalChecker.Core.BusinessFields;

namespace AssetAppraisalChecker.Core.Consistency
{
    internal static class FieldDisplayNameProvider
    {
        public static string GetDisplayName(ComparisonKey key)
        {
            string taxPrefix = GetTaxPrefix(key.TaxBasis);
            string category = GetCategoryName(key.ValueCategory);
            string scope = GetScopeName(key.Scope);
            return string.IsNullOrEmpty(scope)
                ? taxPrefix + category
                : taxPrefix + category + " / " + scope;
        }

        private static string GetTaxPrefix(TaxBasis taxBasis)
        {
            switch (taxBasis)
            {
                case TaxBasis.TaxIncluded:
                    return "含税";
                case TaxBasis.TaxExcluded:
                    return "不含税";
                default:
                    return string.Empty;
            }
        }

        private static string GetCategoryName(ValueCategory category)
        {
            switch (category)
            {
                case ValueCategory.AppraisedValue:
                    return "评估值";
                case ValueCategory.AppraisedOriginalValue:
                    return "评估原值";
                case ValueCategory.AppraisedNetValue:
                    return "评估净值";
                case ValueCategory.BookValue:
                    return "账面价值";
                case ValueCategory.BookOriginalValue:
                    return "账面原值";
                case ValueCategory.BookNetValue:
                    return "账面净值";
                case ValueCategory.ReplacementCost:
                    return "重置成本";
                case ValueCategory.Increment:
                    return "评估增值额";
                case ValueCategory.Decrement:
                    return "评估减值额";
                case ValueCategory.ConstructionCost:
                    return "建安工程费";
                case ValueCategory.OtherCost:
                    return "其他费用";
                case ValueCategory.CapitalCost:
                    return "资金成本";
                case ValueCategory.Tax:
                    return "税额";
                default:
                    return "未明确金额字段";
            }
        }

        private static string GetScopeName(Scope scope)
        {
            switch (scope)
            {
                case Scope.SingleAsset:
                    return "单项资产";
                case Scope.Subtotal:
                    return "小计";
                case Scope.ProjectTotal:
                    return "项目总值";
                case Scope.CalculationCase:
                    return "测算案例";
                default:
                    return string.Empty;
            }
        }
    }
}
