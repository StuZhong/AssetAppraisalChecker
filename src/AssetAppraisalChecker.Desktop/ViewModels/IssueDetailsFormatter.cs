using System;
using System.Collections.Generic;
using System.Text;
using AssetAppraisalChecker.Desktop.Services.Navigation;

namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public static class IssueDetailsFormatter
    {
        public static string Format(IssueViewModel issue, IReadOnlyList<CheckedDocumentSource> sources)
        {
            if (issue == null) return string.Empty;
            var text = new StringBuilder();
            text.AppendLine("问题类型：" + issue.IssueTypeDisplay);
            text.AppendLine("问题类别：" + issue.CategoryDisplay);
            text.AppendLine("判定级别：" + issue.AuthorityDisplay);
            text.AppendLine("严重程度：" + issue.SeverityDisplay);
            text.AppendLine("字段：" + issue.FieldName);
            text.AppendLine("摘要：" + issue.Summary);
            text.AppendLine();
            text.AppendLine("问题说明：" + issue.Reason);
            text.AppendLine("建议操作：" + issue.SuggestedAction);
            foreach (OccurrenceViewModel occurrence in issue.Occurrences)
            {
                CheckedDocumentSource source = SourceNavigationService.FindSource(sources, occurrence.SourceOccurrence);
                text.AppendLine();
                text.AppendLine((source?.Label ?? (occurrence.HasDocumentLabel ? occurrence.DocumentLabel : "来源未确认")) +
                    " · 位置 " + occurrence.DisplayIndex);
                text.AppendLine("结构化位置：" + occurrence.LocationText);
                text.AppendLine("所在区域：" + occurrence.Area);
                if (occurrence.IsSuspectedOutlier) text.AppendLine("提醒：疑似异常值，请人工复核。");
                text.AppendLine("原文片段：" + occurrence.RawText);
                text.AppendLine("标准化值：" + occurrence.NormalizedValue);
                if (occurrence.HasSubject) text.AppendLine("主体：" + occurrence.Subject);
                foreach (string evidence in occurrence.SourceOccurrence.Evidence)
                {
                    if (evidence.StartsWith("稳定身份：", StringComparison.Ordinal)) continue;
                    text.AppendLine("证据：" + evidence);
                }
                if (occurrence.HasWarnings) text.AppendLine("提醒：" + occurrence.WarningsText);
                if (occurrence.HasNearbyContext) text.AppendLine("附近文本：" + occurrence.NearbyContext);
            }
            return text.ToString().TrimEnd('\r', '\n');
        }
    }
}
