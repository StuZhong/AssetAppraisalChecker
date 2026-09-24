using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.Consistency
{
    public sealed class ConsistencyCheckResult
    {
        public ConsistencyCheckResult(
            IEnumerable<ComparisonGroup> comparisonGroups,
            IEnumerable<Issue> issues,
            int skippedOccurrenceCount)
        {
            if (skippedOccurrenceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(skippedOccurrenceCount));
            }

            ComparisonGroups = new ReadOnlyCollection<ComparisonGroup>(
                (comparisonGroups ?? throw new ArgumentNullException(nameof(comparisonGroups))).ToList());
            Issues = new ReadOnlyCollection<Issue>(
                (issues ?? throw new ArgumentNullException(nameof(issues))).ToList());
            SkippedOccurrenceCount = skippedOccurrenceCount;
        }

        public IReadOnlyList<ComparisonGroup> ComparisonGroups { get; }

        public IReadOnlyList<Issue> Issues { get; }

        public int SkippedOccurrenceCount { get; }
    }
}
