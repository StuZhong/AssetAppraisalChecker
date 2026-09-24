using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;

namespace AssetAppraisalChecker.Core.Consistency
{
    public sealed class Issue
    {
        public Issue(
            string issueId,
            IssueType issueType,
            IssueSeverity severity,
            ComparisonKey comparisonKey,
            string fieldName,
            string reason,
            IEnumerable<MoneyOccurrence> occurrences,
            decimal difference,
            ClassificationConfidence confidence,
            string suggestedAction,
            IEnumerable<MoneyOccurrence> suspectedOutlierOccurrences)
        {
            if (string.IsNullOrWhiteSpace(issueId))
            {
                throw new ArgumentException("IssueId 不能为空。", nameof(issueId));
            }

            if (comparisonKey == null)
            {
                throw new ArgumentNullException(nameof(comparisonKey));
            }

            if (difference < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(difference));
            }

            IssueId = issueId;
            IssueType = issueType;
            Severity = severity;
            ComparisonKey = comparisonKey;
            FieldName = fieldName ?? string.Empty;
            Reason = reason ?? string.Empty;
            Occurrences = new ReadOnlyCollection<MoneyOccurrence>(
                (occurrences ?? throw new ArgumentNullException(nameof(occurrences))).ToList());
            Difference = difference;
            Confidence = confidence;
            SuggestedAction = suggestedAction ?? string.Empty;
            SuspectedOutlierOccurrences = new ReadOnlyCollection<MoneyOccurrence>(
                (suspectedOutlierOccurrences ?? Enumerable.Empty<MoneyOccurrence>()).ToList());
        }

        public string IssueId { get; }

        public IssueType IssueType { get; }

        public IssueSeverity Severity { get; }

        public ComparisonKey ComparisonKey { get; }

        public string FieldName { get; }

        public string Reason { get; }

        public IReadOnlyList<MoneyOccurrence> Occurrences { get; }

        public decimal Difference { get; }

        public ClassificationConfidence Confidence { get; }

        public string SuggestedAction { get; }

        /// <summary>
        /// Occurrences outside a unique strict-majority cluster. These are only
        /// suspected outliers and are never declared to be incorrect.
        /// </summary>
        public IReadOnlyList<MoneyOccurrence> SuspectedOutlierOccurrences { get; }
    }
}
