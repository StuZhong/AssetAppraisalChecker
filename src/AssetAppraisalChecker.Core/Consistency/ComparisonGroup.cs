using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;

namespace AssetAppraisalChecker.Core.Consistency
{
    public sealed class ComparisonGroup
    {
        public ComparisonGroup(ComparisonKey comparisonKey, IEnumerable<MoneyOccurrence> occurrences)
        {
            if (comparisonKey == null)
            {
                throw new ArgumentNullException(nameof(comparisonKey));
            }

            ComparisonKey = comparisonKey;
            Occurrences = new ReadOnlyCollection<MoneyOccurrence>(
                (occurrences ?? throw new ArgumentNullException(nameof(occurrences))).ToList());
        }

        public ComparisonKey ComparisonKey { get; }

        public IReadOnlyList<MoneyOccurrence> Occurrences { get; }
    }
}
