using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldConsistencyCheckResult
    {
        public BasicFieldConsistencyCheckResult(
            IEnumerable<BasicFieldComparisonGroup> comparisonGroups,
            IEnumerable<BasicFieldIssue> issues,
            int unparsedOccurrenceCount)
        {
            if (unparsedOccurrenceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unparsedOccurrenceCount));
            }

            ComparisonGroups = new ReadOnlyCollection<BasicFieldComparisonGroup>(
                (comparisonGroups ?? throw new ArgumentNullException(nameof(comparisonGroups))).ToList());
            Issues = new ReadOnlyCollection<BasicFieldIssue>(
                (issues ?? throw new ArgumentNullException(nameof(issues))).ToList());
            UnparsedOccurrenceCount = unparsedOccurrenceCount;
        }

        public IReadOnlyList<BasicFieldComparisonGroup> ComparisonGroups { get; }

        public IReadOnlyList<BasicFieldIssue> Issues { get; }

        public int UnparsedOccurrenceCount { get; }
    }
}
