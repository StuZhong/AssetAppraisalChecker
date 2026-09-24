using System;

namespace AssetAppraisalChecker.Core.Consistency
{
    public sealed class MoneyTolerance
    {
        public static readonly MoneyTolerance Exact = new MoneyTolerance(0m);
        public static readonly MoneyTolerance OneYuan = new MoneyTolerance(1m);
        public static readonly MoneyTolerance TenYuan = new MoneyTolerance(10m);
        public static readonly MoneyTolerance HundredYuan = new MoneyTolerance(100m);
        public static readonly MoneyTolerance OneHundredthTenThousandYuan = new MoneyTolerance(100m);

        public MoneyTolerance(decimal amountYuan)
        {
            if (amountYuan < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(amountYuan), "金额容差不能为负数。");
            }

            AmountYuan = amountYuan;
        }

        public decimal AmountYuan { get; }
    }
}
