using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow.Governance;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class ReportCheckIssue
    {
        public ReportCheckIssue(
            string issueId,
            IssueType issueType,
            IssueSeverity severity,
            string fieldName,
            string summary,
            string reason,
            string suggestedAction,
            IEnumerable<ReportCheckOccurrence> occurrences,
            int sourceOrder,
            BasicFieldType fieldType = BasicFieldType.Unknown,
            string expectedValue = null,
            string issueTypeDisplayOverride = null,
            IssueCategory category = IssueCategory.Business,
            IssueAuthority? authority = null,
            string governanceKind = null)
        {
            if (string.IsNullOrWhiteSpace(issueId))
            {
                throw new ArgumentException("IssueId 不能为空。", nameof(issueId));
            }

            if (sourceOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrder));
            }

            IssueId = issueId;
            IssueType = issueType;
            Severity = severity;
            FieldName = fieldName ?? string.Empty;
            Summary = summary ?? string.Empty;
            Reason = reason ?? string.Empty;
            SuggestedAction = suggestedAction ?? string.Empty;
            Occurrences = new ReadOnlyCollection<ReportCheckOccurrence>(
                (occurrences ?? throw new ArgumentNullException(nameof(occurrences))).ToList());
            SourceOrder = sourceOrder;
            FieldType = fieldType;
            ExpectedValue = expectedValue ?? string.Empty;
            IssueTypeDisplayOverride = issueTypeDisplayOverride ?? string.Empty;
            Category = category;
            Authority = authority ?? IssueGovernancePolicy.ForBusiness(issueType).Authority;
            GovernanceKind = string.IsNullOrWhiteSpace(governanceKind)
                ? issueType.ToString()
                : governanceKind.Trim();
            StableId = IssueStableIdentity.Create(Category, GovernanceKind, Occurrences);
        }

        public string IssueId { get; }

        public IssueType IssueType { get; }

        public IssueSeverity Severity { get; }

        public string FieldName { get; }

        public string Summary { get; }

        public string Reason { get; }

        public string SuggestedAction { get; }

        public IReadOnlyList<ReportCheckOccurrence> Occurrences { get; }

        public int SourceOrder { get; }

        public BasicFieldType FieldType { get; }

        public string ExpectedValue { get; }

        public string IssueTypeDisplayOverride { get; }

        public IssueCategory Category { get; }

        public IssueAuthority Authority { get; }

        public string GovernanceKind { get; }

        public string StableId { get; }

        public string PrimaryLocation
        {
            get
            {
                if (Occurrences.Count == 0)
                {
                    return "文档正文";
                }

                if (Occurrences.Count == 1)
                {
                    return Occurrences[0].LocationText;
                }

                return string.Format(
                    "共{0}处：{1}",
                    Occurrences.Count,
                    string.Join("、", Occurrences.Select(item => item.LocationText)));
            }
        }
    }
}
