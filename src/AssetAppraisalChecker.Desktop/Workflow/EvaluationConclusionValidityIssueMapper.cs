using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.ReportValidation;
using AssetAppraisalChecker.Desktop.Workflow.Governance;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    internal sealed class EvaluationConclusionValidityIssueMapper
    {
        public IEnumerable<ReportCheckIssue> Map(EvaluationConclusionValidityResult result, int order, string label)
        {
            foreach (var finding in result.Findings)
            {
                string description = finding.Anomaly == EvaluationConclusionValidityAnomaly.ReversedRange
                    ? "截止日期早于起始日期"
                    : finding.Anomaly == EvaluationConclusionValidityAnomaly.ClearlyShortPeriod
                        ? "明确日期范围仅约一个月或更短，明显短于一年原则"
                        : "明确日期范围达到两年或更长，明显长于一年原则";
                yield return new ReportCheckIssue("VALIDITY-" + order, IssueType.EvaluationConclusionValidityAnomaly,
                    IssueSeverity.Normal, "评估结论使用有效期", "评估结论使用有效期需复核：" + description,
                    description + "。已同时定位评估基准日及有效期起止日期；本检查不采用精确365/366天或公司精确容差标准。",
                    "请人工核对评估结论使用有效期及适用约定；程序不会修改报告或改变业务一致性裁决。",
                    new[] { finding.Benchmark, finding.Start, finding.End }.Select((date, index) =>
                        new ReportCheckOccurrence(date.RawText, date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            date.Element.Location, DocumentLocationFormatter.Format(date.Element.Location),
                            DocumentLocationFormatter.FormatArea(date.Element.Location), "评估结论使用有效期",
                            new[] { "日期角色=" + new[] { "评估基准日", "有效期起始日", "有效期截止日" }[index],
                                "文档字符偏移=" + date.Offset, "长度=" + date.RawText.Length },
                            new string[0], string.Empty, false, documentLabel: label)), order++,
                    issueTypeDisplayOverride: "评估结论使用有效期异常",
                    authority: finding.Anomaly == EvaluationConclusionValidityAnomaly.ReversedRange
                        ? IssueAuthority.Confirmed : IssueAuthority.HighConfidence,
                    governanceKind: "EvaluationConclusionValidity." + finding.Anomaly);
            }
        }
    }
}
