using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldIssue
    {
        public BasicFieldIssue(
            string issueId,
            IssueType issueType,
            IssueSeverity severity,
            BasicFieldType fieldType,
            string fieldName,
            string reason,
            IEnumerable<BasicFieldOccurrence> occurrences,
            ClassificationConfidence confidence,
            string suggestedAction,
            IEnumerable<BasicFieldOccurrence> suspectedOutlierOccurrences)
        {
            if (string.IsNullOrWhiteSpace(issueId))
            {
                throw new ArgumentException("IssueId 不能为空。", nameof(issueId));
            }

            if (issueType != IssueType.BasicFieldConflict &&
                issueType != IssueType.BasicFieldParseFailed)
            {
                throw new ArgumentException("基础字段问题类型无效。", nameof(issueType));
            }

            if (fieldType == BasicFieldType.Unknown)
            {
                throw new ArgumentException("基础字段问题不能使用 Unknown 类型。", nameof(fieldType));
            }

            IssueId = issueId;
            IssueType = issueType;
            Severity = severity;
            FieldType = fieldType;
            FieldName = fieldName ?? string.Empty;
            Reason = reason ?? string.Empty;
            Occurrences = new ReadOnlyCollection<BasicFieldOccurrence>(
                (occurrences ?? throw new ArgumentNullException(nameof(occurrences))).ToList());
            Confidence = confidence;
            SuggestedAction = suggestedAction ?? string.Empty;
            SuspectedOutlierOccurrences = new ReadOnlyCollection<BasicFieldOccurrence>(
                (suspectedOutlierOccurrences ?? Enumerable.Empty<BasicFieldOccurrence>()).ToList());
        }

        public string IssueId { get; }

        public IssueType IssueType { get; }

        public IssueSeverity Severity { get; }

        public BasicFieldType FieldType { get; }

        public string FieldName { get; }

        public string Reason { get; }

        public IReadOnlyList<BasicFieldOccurrence> Occurrences { get; }

        public ClassificationConfidence Confidence { get; }

        public string SuggestedAction { get; }

        public IReadOnlyList<BasicFieldOccurrence> SuspectedOutlierOccurrences { get; }
    }
}
