using System;

namespace AssetAppraisalChecker.Core.Money
{
    internal static class MoneyUnitConverter
    {
        public static decimal ConvertToYuan(decimal amount, MoneyUnit unit)
        {
            decimal multiplier;

            switch (unit)
            {
                case MoneyUnit.Yuan:
                    multiplier = 1m;
                    break;
                case MoneyUnit.TenThousandYuan:
                    multiplier = 10000m;
                    break;
                case MoneyUnit.HundredMillionYuan:
                    multiplier = 100000000m;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(unit), unit, "无法换算未知金额单位。");
            }

            return checked(amount * multiplier);
        }
    }
}
