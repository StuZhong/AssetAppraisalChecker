using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class MechanicalIssue
    {
        public MechanicalIssue(
            string checkerName,
            MechanicalIssueKind kind,
            MechanicalIssueSeverity severity,
            string fieldName,
            string summary,
            string reason,
            string suggestedAction,
            IEnumerable<MechanicalIssueOccurrence> occurrences)
        {
            if (string.IsNullOrWhiteSpace(checkerName))
            {
                throw new ArgumentException("Checker 名称不能为空。", nameof(checkerName));
            }

            CheckerName = checkerName;
            Kind = kind;
            Severity = severity;
            FieldName = fieldName ?? string.Empty;
            Summary = summary ?? string.Empty;
            Reason = reason ?? string.Empty;
            SuggestedAction = suggestedAction ?? string.Empty;
            Occurrences = new ReadOnlyCollection<MechanicalIssueOccurrence>(
                (occurrences ?? throw new ArgumentNullException(nameof(occurrences))).ToList());
            if (Occurrences.Count == 0)
            {
                throw new ArgumentException("机械问题必须至少包含一处来源证据。", nameof(occurrences));
            }
        }

        public string CheckerName { get; }
        public MechanicalIssueKind Kind { get; }
        public MechanicalIssueSeverity Severity { get; }
        public string FieldName { get; }
        public string Summary { get; }
        public string Reason { get; }
        public string SuggestedAction { get; }
        public IReadOnlyList<MechanicalIssueOccurrence> Occurrences { get; }
    }
}
