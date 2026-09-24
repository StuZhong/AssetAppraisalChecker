using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public sealed class IssueViewModel
    {
        public IssueViewModel(ReportCheckIssue issue)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }

            IssueId = issue.IssueId;
            IssueType = issue.IssueType;
            Severity = issue.Severity;
            FieldName = issue.FieldName;
            Summary = issue.Summary;
            Reason = issue.Reason;
            SuggestedAction = issue.SuggestedAction;
            PrimaryLocation = issue.PrimaryLocation;
            SourceOrder = issue.SourceOrder;
            Occurrences = new ReadOnlyCollection<OccurrenceViewModel>(
                issue.Occurrences
                    .Select((item, index) => new OccurrenceViewModel(item, index + 1))
                    .ToList());
        }

        public string IssueId { get; }

        public IssueType IssueType { get; }

        public IssueSeverity Severity { get; }

        public string SeverityDisplay => GetSeverityDisplay(Severity);

        public string IssueTypeDisplay => GetIssueTypeDisplay(IssueType);

        public string FieldName { get; }

        public string Summary { get; }

        public string Reason { get; }

        public string SuggestedAction { get; }

        public string PrimaryLocation { get; }

        public int SourceOrder { get; }

        public IReadOnlyList<OccurrenceViewModel> Occurrences { get; }

        private static string GetSeverityDisplay(IssueSeverity severity)
        {
            switch (severity)
            {
                case IssueSeverity.Serious:
                    return "严重";
                case IssueSeverity.Normal:
                    return "一般";
                default:
                    return "提醒";
            }
        }

        private static string GetIssueTypeDisplay(IssueType issueType)
        {
            switch (issueType)
            {
                case IssueType.MoneyConflict:
                    return "金额冲突";
                case IssueType.BasicFieldConflict:
                    return "基础信息冲突";
                case IssueType.BasicFieldParseFailed:
                    return "基础信息解析提醒";
                case IssueType.ProjectNameExpectedMismatch:
                    return "项目名称不一致";
                default:
                    return "校检问题";
            }
        }
    }
}
