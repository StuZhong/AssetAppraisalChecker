using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Application.V3
{
    public sealed class V3MoneyComparisonResult
    {
        internal V3MoneyComparisonResult(IEnumerable<V3MoneyComparisonItem> items)
        {
            Items = new ReadOnlyCollection<V3MoneyComparisonItem>(items.ToList());
        }

        public IReadOnlyList<V3MoneyComparisonItem> Items { get; }
        public int TotalCount => Items.Count;
        public int ConsistentCount => Items.Count(item => item.Status == V3MoneyComparisonStatus.Consistent);
        public int InconsistentCount => Items.Count(item => item.Status == V3MoneyComparisonStatus.Inconsistent);
        public int IndeterminateCount => Items.Count(item => item.Status == V3MoneyComparisonStatus.Indeterminate);
        public bool IsComplete => TotalCount == 36 && IndeterminateCount == 0;
        public V3MoneyComparisonStatus OverallStatus => InconsistentCount > 0 ? V3MoneyComparisonStatus.Inconsistent
            : IsComplete ? V3MoneyComparisonStatus.Consistent : V3MoneyComparisonStatus.Indeterminate;
    }
}
