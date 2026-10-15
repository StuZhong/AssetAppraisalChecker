using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.ReportValidation;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.TextQuality;
using AssetAppraisalChecker.TextQuality.Grammar;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class ReportCheckService : IReportCheckService
    {
        private static readonly CultureInfo AmountCulture = CultureInfo.GetCultureInfo("zh-CN");
        private static readonly ProjectNameExpectedIssueMapper ProjectNameIssueMapper =
            new ProjectNameExpectedIssueMapper();
        private static readonly SpellChecker TextSpellChecker = new SpellChecker();
        private static readonly GrammarExpressionChecker GrammarChecker =
            new GrammarExpressionChecker();

        private readonly IV2DocumentAnalysisPipeline _crossDocumentPipeline;
        private readonly CrossDocumentIssueMapper _crossDocumentIssueMapper;

        public ReportCheckService()
            : this(new V2DocumentAnalysisPipeline(new FailClosedSemanticAnalyzer()))
        {
        }

        public ReportCheckService(IV2DocumentAnalysisPipeline crossDocumentPipeline)
        {
            _crossDocumentPipeline = crossDocumentPipeline ??
                throw new ArgumentNullException(nameof(crossDocumentPipeline));
            _crossDocumentIssueMapper = new CrossDocumentIssueMapper();
        }

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

        private ReportCheckResult Check(
            ReportCheckRequest request,
            IProgress<string> progress)
        {
            DateTimeOffset startedAt = DateTimeOffset.Now;
            var stopwatch = Stopwatch.StartNew();
            string documentAPath = request.DocumentAPath;
            string documentAName = GetFileName(documentAPath);
            string documentBPath = request.DocumentBPath;
            string documentBName = GetFileName(documentBPath);

            try
            {
                string hashABefore = TryComputeSha256(documentAPath);
                string hashBBefore = request.HasDocumentB
                    ? TryComputeSha256(documentBPath)
                    : string.Empty;

                ReportProgress(progress, request.HasDocumentB
                    ? "正在读取文档 A……"
                    : "正在读取报告……");
                DocumentReadResult documentA = new DocxDocumentReader().Read(documentAPath);
                if (!documentA.IsSuccess)
                {
                    stopwatch.Stop();
                    return FailedResult(
                        documentAPath,
                        documentAName,
                        startedAt,
                        stopwatch.Elapsed,
                        documentA.Status,
                        null,
                        documentBPath,
                        documentBName,
                        null);
                }

                EnsureHashBaselineAvailable(hashABefore, "文档 A");

                DocumentReadResult documentB = null;
                if (request.HasDocumentB)
                {
                    ReportProgress(progress, "正在读取文档 B……");
                    documentB = new DocxDocumentReader().Read(documentBPath);
                    if (!documentB.IsSuccess)
                    {
                        stopwatch.Stop();
                        return FailedResult(
                            documentAPath,
                            documentAName,
                            startedAt,
                            stopwatch.Elapsed,
                            DocumentReadStatus.Success,
                            "文档 B 解析失败：" +
                                DocumentReadStatusMessageProvider.GetMessage(documentB.Status),
                            documentBPath,
                            documentBName,
                            documentB.Status);
                    }

                    EnsureHashBaselineAvailable(hashBBefore, "文档 B");
                }

                SingleDocumentAnalysis analysisA = AnalyzeSingleDocument(
                    documentAPath,
                    documentA,
                    request.ExpectedProjectName,
                    progress,
                    request.HasDocumentB ? "文档 A" : null);

                if (!request.HasDocumentB)
                {
                    EnsureUnchanged(documentAPath, hashABefore, "文档 A");
                    stopwatch.Stop();
                    return CreateSingleDocumentResult(
                        documentAPath,
                        documentAName,
                        startedAt,
                        stopwatch.Elapsed,
                        analysisA,
                        new CheckedDocumentSource("文档 A", documentAPath, hashABefore, documentA));
                }

                SingleDocumentAnalysis analysisB = AnalyzeSingleDocument(
                    documentBPath,
                    documentB,
                    request.ExpectedProjectName,
                    progress,
                    "文档 B");

                ReportProgress(progress, "正在执行跨文档业务事实比较……");
                V2AnalysisResult crossDocument = _crossDocumentPipeline.Analyze(
                    new V2DocumentAnalysisInput(
                        documentA,
                        CrossDocumentIssueMapper.DocumentASource),
                    new V2DocumentAnalysisInput(
                        documentB,
                        CrossDocumentIssueMapper.DocumentBSource));

                var issues = new List<ReportCheckIssue>();
                issues.AddRange(LabelIssues(
                    analysisA.Issues,
                    CrossDocumentIssueMapper.DocumentALabel,
                    issues.Count));
                issues.AddRange(LabelIssues(
                    analysisB.Issues,
                    CrossDocumentIssueMapper.DocumentBLabel,
                    issues.Count));
                issues.AddRange(_crossDocumentIssueMapper.Map(
                    crossDocument,
                    documentA,
                    documentB,
                    issues.Count));
                issues.AddRange(_crossDocumentIssueMapper.MapMoney(
                    analysisA.MoneyOccurrences,
                    analysisB.MoneyOccurrences,
                    crossDocument.Results.Any(item =>
                        item.FactType == AssetAppraisalChecker.Facts.Models.FactType.AssessmentValue),
                    issues.Count));
                issues.AddRange(_crossDocumentIssueMapper.MapProjectName(
                    analysisA.BasicOccurrences,
                    analysisB.BasicOccurrences,
                    issues.Count));

                EnsureUnchanged(documentAPath, hashABefore, "文档 A");
                EnsureUnchanged(documentBPath, hashBBefore, "文档 B");

                issues = new IssueGovernanceProcessor()
                    .Deduplicate(issues)
                    .OrderByDescending(item => (int)item.Severity)
                    .ThenBy(item => item.SourceOrder)
                    .ToList();
                stopwatch.Stop();
                string message = issues.Count == 0
                    ? "双文档校检完成，当前规则未发现能够确认的跨文档一致性问题。"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "双文档校检完成，共发现 {0} 个需要关注的问题。",
                        issues.Count);
                return new ReportCheckResult(
                    documentAPath,
                    documentAName,
                    startedAt,
                    DateTimeOffset.Now,
                    stopwatch.Elapsed,
                    DocumentReadStatus.Success,
                    analysisA.MoneyOccurrenceCount + analysisB.MoneyOccurrenceCount,
                    analysisA.BasicOccurrences.Count + analysisB.BasicOccurrences.Count,
                    issues,
                    true,
                    message,
                    analysisA.ProjectNameExpectedResult,
                    documentBPath,
                    documentBName,
                    DocumentReadStatus.Success,
                    new[]
                    {
                        new CheckedDocumentSource("文档 A", documentAPath, hashABefore, documentA),
                        new CheckedDocumentSource("文档 B", documentBPath, hashBBefore, documentB)
                    });
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                Trace.TraceError("ReportCheckService unexpected error: {0}", exception);
                return FailedResult(
                    documentAPath,
                    documentAName,
                    startedAt,
                    stopwatch.Elapsed,
                    DocumentReadStatus.UnexpectedError,
                    null,
                    documentBPath,
                    documentBName,
                    request.HasDocumentB
                        ? (DocumentReadStatus?)DocumentReadStatus.UnexpectedError
                        : null);
            }
        }

        private static SingleDocumentAnalysis AnalyzeSingleDocument(
            string filePath,
            DocumentReadResult document,
            string expectedProjectName,
            IProgress<string> progress,
            string documentLabel)
        {
            string prefix = string.IsNullOrWhiteSpace(documentLabel)
                ? string.Empty
                : documentLabel + "：";
            ReportProgress(progress, prefix + "正在识别金额……");
            IReadOnlyList<MoneyOccurrence> moneyOccurrences =
                new MoneyOccurrenceExtractor().Extract(document);
            ConsistencyCheckResult moneyResult =
                new ConsistencyEngine().Check(moneyOccurrences);

            ReportProgress(progress, prefix + "正在校检基础信息……");
            IReadOnlyList<BasicFieldOccurrence> basicOccurrences =
                new BasicFieldExtractor().Extract(document);
            BasicFieldConsistencyCheckResult basicResult =
                new BasicFieldConsistencyEngine().Check(basicOccurrences);
            ProjectNameExpectedCheckResult projectNameExpectedResult =
                new ReportLevelProjectNameCheckCoordinator().Check(
                    expectedProjectName,
                    document.Elements);

            IReadOnlyList<MechanicalIssue> mechanicalIssues =
                new MechanicalDocumentCheckPipeline().Check(
                    new MechanicalCheckContext(
                        filePath,
                        document,
                        string.IsNullOrWhiteSpace(documentLabel) ? "文档 A" : documentLabel));
            List<ReportCheckIssue> mappedIssues = MapIssues(
                moneyResult.Issues,
                basicResult.Issues,
                projectNameExpectedResult);
            mappedIssues.AddRange(new TableCalculationIssueMapper().Map(
                new ConsistencyEngine().CheckTableCalculations(document), mappedIssues.Count,
                string.IsNullOrWhiteSpace(documentLabel) ? "文档 A" : documentLabel));
            mappedIssues.AddRange(new MechanicalIssueMapper().Map(
                mechanicalIssues,
                mappedIssues.Count));

            mappedIssues.AddRange(new EvaluationConclusionValidityIssueMapper().Map(
                new EvaluationConclusionValidityChecker().Check(document.Elements), mappedIssues.Count,
                string.IsNullOrWhiteSpace(documentLabel) ? "文档 A" : documentLabel));

            ReportProgress(progress, prefix + "正在检查文字质量……");
            SpellCheckResult spellResult = TextSpellChecker.Check(
                document,
                string.IsNullOrWhiteSpace(documentLabel) ? "文档 A" : documentLabel);
            mappedIssues.AddRange(new TextQualityIssueMapper().Map(
                spellResult,
                mappedIssues.Count));
            GrammarCheckResult grammarResult = GrammarChecker.Check(
                document,
                string.IsNullOrWhiteSpace(documentLabel) ? "文档 A" : documentLabel);
            mappedIssues.AddRange(new GrammarIssueMapper().Map(
                grammarResult,
                mappedIssues.Count));

            ReportProgress(progress, prefix + "正在生成结果……");
            return new SingleDocumentAnalysis(
                moneyOccurrences,
                basicOccurrences,
                mappedIssues,
                projectNameExpectedResult);
        }

        private static ReportCheckResult CreateSingleDocumentResult(
            string filePath,
            string fileName,
            DateTimeOffset startedAt,
            TimeSpan duration,
            SingleDocumentAnalysis analysis,
            CheckedDocumentSource source)
        {
            List<ReportCheckIssue> issues = new IssueGovernanceProcessor()
                .Deduplicate(analysis.Issues)
                .OrderByDescending(item => (int)item.Severity)
                .ThenBy(item => item.SourceOrder)
                .ToList();
            return new ReportCheckResult(
                filePath,
                fileName,
                startedAt,
                DateTimeOffset.Now,
                duration,
                DocumentReadStatus.Success,
                analysis.MoneyOccurrenceCount,
                analysis.BasicOccurrences.Count,
                issues,
                true,
                CreateCompletionMessage(
                    issues.Count,
                    analysis.ProjectNameExpectedResult.Status),
                analysis.ProjectNameExpectedResult,
                sourceDocuments: new[] { source });
        }

        private static ReportCheckResult FailedResult(
            string filePath,
            string fileName,
            DateTimeOffset startedAt,
            TimeSpan duration,
            DocumentReadStatus status,
            string message,
            string documentBPath,
            string documentBName,
            DocumentReadStatus? documentBReadStatus)
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
                message ?? DocumentReadStatusMessageProvider.GetMessage(status),
                null,
                documentBPath,
                documentBName,
                documentBReadStatus);
        }

        private static IEnumerable<ReportCheckIssue> LabelIssues(
            IEnumerable<ReportCheckIssue> source,
            string documentLabel,
            int sourceOrderStart)
        {
            return source.Select((issue, issueIndex) => new ReportCheckIssue(
                documentLabel.Replace(" ", string.Empty) + "-" + issue.IssueId,
                issue.IssueType,
                issue.Severity,
                issue.FieldName,
                issue.Summary,
                issue.Reason,
                issue.SuggestedAction,
                issue.Occurrences.Select(item => new ReportCheckOccurrence(
                    item.RawText,
                    item.NormalizedValue,
                    item.Location,
                    item.LocationText,
                    item.Area,
                    item.Subject,
                    item.Evidence,
                    item.Warnings,
                    item.NearbyContext,
                    item.IsSuspectedOutlier,
                    item.ProjectNameCandidate,
                    documentLabel)),
                sourceOrderStart + issueIndex,
                issue.FieldType,
                issue.ExpectedValue,
                issue.IssueTypeDisplayOverride,
                issue.Category,
                issue.Authority,
                issue.GovernanceKind));
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
                mapped.Add(MapMoneyIssue(issue, sourceOrder++));
            }

            foreach (BasicFieldIssue issue in basicIssues)
            {
                mapped.Add(MapBasicIssue(issue, sourceOrder++));
            }

            mapped.AddRange(ProjectNameIssueMapper.Map(projectNameExpectedResult, sourceOrder));
            return mapped;
        }

        private static ReportCheckIssue MapMoneyIssue(Issue issue, int sourceOrder)
        {
            IssueGovernanceClassification governance =
                IssueGovernancePolicy.ForBusiness(issue.IssueType);
            List<ReportCheckOccurrence> occurrences = issue.Occurrences
                .Select(occurrence => new ReportCheckOccurrence(
                    occurrence.RawText,
                    string.Format(AmountCulture, "{0:N2} 元", occurrence.NormalizedAmountYuan),
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
                string.Format(CultureInfo.InvariantCulture, "{0}处金额标准值不一致", occurrences.Count),
                issue.Reason,
                issue.SuggestedAction,
                occurrences,
                sourceOrder,
                BasicFieldType.Unknown,
                null,
                null,
                governance.Category,
                governance.Authority,
                governance.GovernanceKind);
        }

        private static ReportCheckIssue MapBasicIssue(BasicFieldIssue issue, int sourceOrder)
        {
            IssueGovernanceClassification governance =
                IssueGovernancePolicy.ForBusiness(issue.IssueType);
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
                : string.Format(CultureInfo.InvariantCulture, "{0}处基础字段标准值不一致", occurrences.Count);
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
                issue.FieldType,
                null,
                null,
                governance.Category,
                governance.Authority,
                governance.GovernanceKind);
        }

        private static string CreateCompletionMessage(
            int issueCount,
            ProjectNameExpectedCheckStatus projectNameStatus)
        {
            string resultMessage = issueCount == 0
                ? "其他当前规则未发现能够确认的一致性问题。"
                : string.Format(CultureInfo.InvariantCulture, "校检完成，共发现 {0} 个需要关注的问题。", issueCount);
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
                ? (issueCount == 0 ? "未发现当前规则能够确认的一致性问题。" : resultMessage)
                : resultMessage + " " + projectNameMessage;
        }

        private static void EnsureUnchanged(string filePath, string hashBefore, string documentLabel)
        {
            string hashAfter = ComputeSha256(filePath);
            if (!string.Equals(hashBefore, hashAfter, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(documentLabel + " 在只读校检期间发生变化，结果已作废。");
            }
        }

        private static void EnsureHashBaselineAvailable(string hashBefore, string documentLabel)
        {
            if (string.IsNullOrWhiteSpace(hashBefore))
            {
                throw new IOException(documentLabel + " 无法建立只读完整性基线，校检已停止。");
            }
        }

        private static string TryComputeSha256(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            try
            {
                return ComputeSha256(filePath);
            }
            catch (IOException exception)
            {
                Trace.TraceWarning("Unable to establish SHA-256 baseline: {0}", exception.GetType().Name);
                return string.Empty;
            }
            catch (UnauthorizedAccessException exception)
            {
                Trace.TraceWarning("Unable to establish SHA-256 baseline: {0}", exception.GetType().Name);
                return string.Empty;
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
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

        private sealed class SingleDocumentAnalysis
        {
            public SingleDocumentAnalysis(
                IReadOnlyList<MoneyOccurrence> moneyOccurrences,
                IReadOnlyList<BasicFieldOccurrence> basicOccurrences,
                IReadOnlyList<ReportCheckIssue> issues,
                ProjectNameExpectedCheckResult projectNameExpectedResult)
            {
                MoneyOccurrences = moneyOccurrences;
                BasicOccurrences = basicOccurrences;
                Issues = issues;
                ProjectNameExpectedResult = projectNameExpectedResult;
            }

            public IReadOnlyList<MoneyOccurrence> MoneyOccurrences { get; }
            public int MoneyOccurrenceCount => MoneyOccurrences.Count;
            public IReadOnlyList<BasicFieldOccurrence> BasicOccurrences { get; }
            public IReadOnlyList<ReportCheckIssue> Issues { get; }
            public ProjectNameExpectedCheckResult ProjectNameExpectedResult { get; }
        }
    }
}
