using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;

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
            IssueTypeDisplay = string.IsNullOrWhiteSpace(issue.IssueTypeDisplayOverride)
                ? GetIssueTypeDisplay(IssueType)
                : issue.IssueTypeDisplayOverride;
            Category = issue.Category;
            Authority = issue.Authority;
            GovernanceKind = issue.GovernanceKind;
            StableId = issue.StableId;
            Occurrences = new ReadOnlyCollection<OccurrenceViewModel>(
                issue.Occurrences
                    .Select((item, index) => new OccurrenceViewModel(item, index + 1))
                    .ToList());
        }

        public string IssueId { get; }

        public IssueType IssueType { get; }

        public IssueSeverity Severity { get; }

        public string SeverityDisplay => GetSeverityDisplay(Severity);

        public string IssueTypeDisplay { get; }

        public IssueCategory Category { get; }

        public IssueAuthority Authority { get; }
        public string GovernanceKind { get; }

        public string CategoryDisplay => GetCategoryDisplay(Category);

        public string AuthorityDisplay => GetAuthorityDisplay(Authority);

        public bool IsReviewSuggested => Authority == IssueAuthority.ReviewSuggested;

        public string StableId { get; }

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

        private static string GetCategoryDisplay(IssueCategory category)
        {
            switch (category)
            {
                case IssueCategory.Business:
                    return "业务";
                case IssueCategory.Mechanical:
                    return "机械检查";
                case IssueCategory.Formatting:
                    return "格式";
                case IssueCategory.Language:
                    return "语言";
                case IssueCategory.Domain:
                    return "专业规则";
                default:
                    return "系统执行";
            }
        }

        private static string GetAuthorityDisplay(IssueAuthority authority)
        {
            switch (authority)
            {
                case IssueAuthority.Confirmed:
                    return "明确问题";
                case IssueAuthority.HighConfidence:
                    return "高置信异常";
                default:
                    return "建议人工复核";
            }
        }
    }
}
