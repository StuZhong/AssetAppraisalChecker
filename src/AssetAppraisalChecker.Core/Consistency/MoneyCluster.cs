using System.Collections.Generic;
using AssetAppraisalChecker.Core.BusinessFields;

namespace AssetAppraisalChecker.Core.Consistency
{
    internal sealed class MoneyCluster
    {
        public MoneyCluster(MoneyOccurrence firstOccurrence)
        {
            Occurrences = new List<MoneyOccurrence> { firstOccurrence };
            MinimumAmountYuan = firstOccurrence.NormalizedAmountYuan;
            MaximumAmountYuan = firstOccurrence.NormalizedAmountYuan;
        }

        public IList<MoneyOccurrence> Occurrences { get; }

        public decimal MinimumAmountYuan { get; private set; }

        public decimal MaximumAmountYuan { get; private set; }

        public bool CanInclude(MoneyOccurrence occurrence, decimal toleranceYuan)
        {
            return occurrence.NormalizedAmountYuan - MinimumAmountYuan <= toleranceYuan;
        }

        public void Add(MoneyOccurrence occurrence)
        {
            Occurrences.Add(occurrence);
            if (occurrence.NormalizedAmountYuan > MaximumAmountYuan)
            {
                MaximumAmountYuan = occurrence.NormalizedAmountYuan;
            }
        }
    }
}
