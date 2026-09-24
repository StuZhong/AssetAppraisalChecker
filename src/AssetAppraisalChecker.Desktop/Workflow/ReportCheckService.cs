using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class ReportCheckService : IReportCheckService
    {
        private static readonly CultureInfo AmountCulture = CultureInfo.GetCultureInfo("zh-CN");
        private static readonly ProjectNameExpectedIssueMapper ProjectNameIssueMapper =
            new ProjectNameExpectedIssueMapper();

        public Task<ReportCheckResult> CheckAsync(
            string filePath,
            IProgress<string> progress = null)
        {
            return CheckAsync(new ReportCheckRequest(filePath, null), progress);
        }

        public Task<ReportCheckResult> CheckAsync(
            ReportCheckRequest request,
            IProgress<string> progress = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return Task.Run(() => Check(request, progress));
        }

        private static ReportCheckResult Check(
            ReportCheckRequest request,
            IProgress<string> progress)
        {
            DateTimeOffset startedAt = DateTimeOffset.Now;
            var stopwatch = Stopwatch.StartNew();
            string filePath = request.FilePath;
            string fileName = GetFileName(filePath);

            try
            {
                ReportProgress(progress, "正在读取报告……");
                DocumentReadResult document = new DocxDocumentReader().Read(filePath);
                if (!document.IsSuccess)
                {
                    stopwatch.Stop();
                    return FailedResult(
                        filePath,
                        fileName,
                        startedAt,
                        stopwatch.Elapsed,
                        document.Status);
                }

                ReportProgress(progress, "正在识别金额……");
                IReadOnlyList<MoneyOccurrence> moneyOccurrences =
                    new MoneyOccurrenceExtractor().Extract(document);
                ConsistencyCheckResult moneyResult =
                    new ConsistencyEngine().Check(moneyOccurrences);

                ReportProgress(progress, "正在校检基础信息……");
                IReadOnlyList<BasicFieldOccurrence> basicOccurrences =
                    new BasicFieldExtractor().Extract(document);
                BasicFieldConsistencyCheckResult basicResult =
                    new BasicFieldConsistencyEngine().Check(basicOccurrences);
                ProjectNameExpectedCheckResult projectNameExpectedResult =
                    new ReportLevelProjectNameCheckCoordinator().Check(
                        request.ExpectedProjectName,
                        document.Elements);

                ReportProgress(progress, "正在生成结果……");
                List<ReportCheckIssue> issues = MapIssues(
                    moneyResult.Issues,
                    basicResult.Issues,
                    projectNameExpectedResult);
                issues = issues
                    .OrderByDescending(item => (int)item.Severity)
                    .ThenBy(item => item.SourceOrder)
                    .ToList();

                stopwatch.Stop();
                string message = CreateCompletionMessage(
                    issues.Count,
                    projectNameExpectedResult.Status);
                return new ReportCheckResult(
                    filePath,
                    fileName,
                    startedAt,
                    DateTimeOffset.Now,
                    stopwatch.Elapsed,
                    DocumentReadStatus.Success,
                    moneyOccurrences.Count,
                    basicOccurrences.Count,
                    issues,
                    true,
                    message,
                    projectNameExpectedResult);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                Trace.TraceError("ReportCheckService unexpected error: {0}", exception);
                return FailedResult(
                    filePath,
                    fileName,
                    startedAt,
                    stopwatch.Elapsed,
                    DocumentReadStatus.UnexpectedError);
            }
        }

        private static ReportCheckResult FailedResult(
            string filePath,
            string fileName,
            DateTimeOffset startedAt,
            TimeSpan duration,
            DocumentReadStatus status)
        {
            return new ReportCheckResult(
                filePath,
                fileName,
                startedAt,
                DateTimeOffset.Now,
                duration,
                status,
                0,
                0,
                new ReportCheckIssue[0],
                false,
                DocumentReadStatusMessageProvider.GetMessage(status));
        }

        private static List<ReportCheckIssue> MapIssues(
            IEnumerable<Issue> moneyIssues,
            IEnumerable<BasicFieldIssue> basicIssues,
            ProjectNameExpectedCheckResult projectNameExpectedResult)
        {
            var mapped = new List<ReportCheckIssue>();
            int sourceOrder = 0;

            foreach (Issue issue in moneyIssues)
            {
                mapped.Add(MapMoneyIssue(issue, sourceOrder));
                sourceOrder++;
            }

            foreach (BasicFieldIssue issue in basicIssues)
            {
                mapped.Add(MapBasicIssue(issue, sourceOrder));
                sourceOrder++;
            }

            mapped.AddRange(ProjectNameIssueMapper.Map(
                projectNameExpectedResult,
                sourceOrder));

            return mapped;
        }

        private static ReportCheckIssue MapMoneyIssue(Issue issue, int sourceOrder)
        {
            List<ReportCheckOccurrence> occurrences = issue.Occurrences
                .Select(occurrence => new ReportCheckOccurrence(
                    occurrence.RawText,
                    string.Format(
                        AmountCulture,
                        "{0:N2} 元",
                        occurrence.NormalizedAmountYuan),
                    occurrence.Location,
                    DocumentLocationFormatter.Format(occurrence.Location),
                    GetSectionDisplayName(occurrence.FieldIdentity.SectionCategory),
                    GetSubjectDisplayName(occurrence),
                    occurrence.ClassificationEvidence
                        .Concat(occurrence.SubjectEvidence)
                        .Distinct(StringComparer.Ordinal),
                    occurrence.ParseWarnings,
                    FormatNearbyContext(occurrence.NearbyContext),
                    issue.SuspectedOutlierOccurrences.Any(
                        outlier => ReferenceEquals(outlier, occurrence))))
                .ToList();

            return new ReportCheckIssue(
                issue.IssueId,
                issue.IssueType,
                issue.Severity,
                issue.FieldName,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}处金额标准值不一致",
                    occurrences.Count),
                issue.Reason,
                issue.SuggestedAction,
                occurrences,
                sourceOrder);
        }

        private static string CreateCompletionMessage(
            int issueCount,
            ProjectNameExpectedCheckStatus projectNameStatus)
        {
            string resultMessage = issueCount == 0
                ? "其他当前规则未发现能够确认的一致性问题。"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "校检完成，共发现 {0} 个需要关注的问题。",
                    issueCount);
            string projectNameMessage;
            switch (projectNameStatus)
            {
                case ProjectNameExpectedCheckStatus.ValidExpectedValue:
                    projectNameMessage = string.Empty;
                    break;
                case ProjectNameExpectedCheckStatus.MissingExpectedValue:
                    projectNameMessage = "未提供正确项目名称，本次未执行项目名称人工基准校检。";
                    break;
                case ProjectNameExpectedCheckStatus.ExpectedNormalizationFailed:
                    projectNameMessage = "输入的正确项目名称无法形成有效标准值，项目名称人工基准校检未完成。";
                    break;
                case ProjectNameExpectedCheckStatus.NoAuthoritativeCandidates:
                    projectNameMessage = "未识别到经认证的报告级项目名称位置，项目名称人工基准校检未完成。";
                    break;
                case ProjectNameExpectedCheckStatus.CandidateNormalizationFailed:
                    projectNameMessage = "报告级项目名称候选无法标准化，项目名称人工基准校检未完成。";
                    break;
                default:
                    projectNameMessage = "项目名称人工基准校检未完成。";
                    break;
            }

            return string.IsNullOrEmpty(projectNameMessage)
                ? (issueCount == 0
                    ? "未发现当前规则能够确认的一致性问题。"
                    : resultMessage)
                : resultMessage + " " + projectNameMessage;
        }

        private static ReportCheckIssue MapBasicIssue(BasicFieldIssue issue, int sourceOrder)
        {
            List<ReportCheckOccurrence> occurrences = issue.Occurrences
                .Select(occurrence => new ReportCheckOccurrence(
                    occurrence.RawValue,
                    occurrence.HasNormalizedValue
                        ? occurrence.NormalizedValue.CanonicalValue
                        : "无法解析",
                    occurrence.Location,
                    DocumentLocationFormatter.Format(occurrence.Location),
                    DocumentLocationFormatter.FormatArea(occurrence.Location),
                    string.Empty,
                    occurrence.Evidence,
                    string.IsNullOrWhiteSpace(occurrence.FailureReason)
                        ? new string[0]
                        : new[] { occurrence.FailureReason },
                    FormatNearbyContext(occurrence.NearbyContext),
                    issue.SuspectedOutlierOccurrences.Any(
                        outlier => ReferenceEquals(outlier, occurrence))))
                .ToList();

            string summary = issue.IssueType == IssueType.BasicFieldParseFailed
                ? "字段值无法标准化，请人工复核"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}处基础字段标准值不一致",
                    occurrences.Count);
            return new ReportCheckIssue(
                issue.IssueId,
                issue.IssueType,
                issue.Severity,
                issue.FieldName,
                summary,
                issue.Reason,
                issue.SuggestedAction,
                occurrences,
                sourceOrder,
                issue.FieldType);
        }

        private static string GetSubjectDisplayName(MoneyOccurrence occurrence)
        {
            switch (occurrence.SubjectKind)
            {
                case SubjectKind.Project:
                    return "当前项目";
                case SubjectKind.Asset:
                    return string.Format("资产“{0}”", occurrence.SubjectId);
                default:
                    return string.Empty;
            }
        }

        private static string GetSectionDisplayName(SectionCategory section)
        {
            switch (section)
            {
                case SectionCategory.ReportSummary:
                    return "报告摘要";
                case SectionCategory.ValuationConclusion:
                    return "评估结论";
                case SectionCategory.ResultTable:
                    return "评估结果表";
                case SectionCategory.CalculationProcess:
                    return "计算过程";
                case SectionCategory.Explanation:
                    return "说明";
                case SectionCategory.Appendix:
                    return "附件";
                default:
                    return "其他区域";
            }
        }

        private static string FormatNearbyContext(DocumentNearbyContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(context.PreviousText))
            {
                lines.Add("前文：" + context.PreviousText);
            }

            if (!string.IsNullOrWhiteSpace(context.CurrentText))
            {
                lines.Add("当前：" + context.CurrentText);
            }

            if (!string.IsNullOrWhiteSpace(context.NextText))
            {
                lines.Add("后文：" + context.NextText);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string GetFileName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(filePath);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        private static void ReportProgress(IProgress<string> progress, string message)
        {
            progress?.Report(message);
        }
    }
}
