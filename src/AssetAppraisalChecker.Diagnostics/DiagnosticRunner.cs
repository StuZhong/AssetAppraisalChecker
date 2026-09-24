using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Diagnostics
{
    /// <summary>
    /// Runs the production read/check pipeline and emits only aggregate or structural
    /// diagnostics. Report text, normalized values, subjects, headings and paths are
    /// deliberately excluded from every output branch.
    /// </summary>
    public sealed class DiagnosticRunner
    {
        private const int SuccessExitCode = 0;
        private const int ReadFailureExitCode = 2;
        private const int InvalidArgumentsExitCode = 64;
        private const int InvalidInputExitCode = 65;
        private const int UnexpectedFailureExitCode = 70;

        private static readonly BasicFieldType[] DiagnosticFieldTypes =
        {
            BasicFieldType.ProjectName,
            BasicFieldType.ReportNumber,
            BasicFieldType.ValuationDate
        };

        private readonly IReportCheckService reportCheckService;

        public DiagnosticRunner()
            : this(new ReportCheckService())
        {
        }

        public DiagnosticRunner(IReportCheckService reportCheckService)
        {
            this.reportCheckService = reportCheckService ??
                throw new ArgumentNullException(nameof(reportCheckService));
        }

        public int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (args == null || args.Length != 1)
            {
                error.WriteLine("用法：AssetAppraisalChecker.Diagnostics.exe \"<path-to-docx>\"");
                return InvalidArgumentsExitCode;
            }

            string filePath;
            try
            {
                filePath = Path.GetFullPath(args[0] ?? string.Empty);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                error.WriteLine("输入路径无效，未读取任何文件。");
                return InvalidInputExitCode;
            }

            if (!string.Equals(
                    Path.GetExtension(filePath),
                    ".docx",
                    StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("DocumentReadStatus: UnsupportedFileType");
                error.WriteLine("当前诊断入口仅支持显式指定的DOCX文件。");
                return InvalidInputExitCode;
            }

            if (!File.Exists(filePath))
            {
                output.WriteLine("DocumentReadStatus: FileNotFound");
                error.WriteLine("指定的DOCX文件不存在。");
                return InvalidInputExitCode;
            }

            FileSnapshot before;
            try
            {
                before = FileSnapshot.Capture(filePath);
            }
            catch (Exception)
            {
                output.WriteLine("DocumentReadStatus: AccessDenied");
                error.WriteLine("无法以只读方式访问指定DOCX文件。");
                return ReadFailureExitCode;
            }

            int exitCode;
            try
            {
                exitCode = RunProductionDiagnostics(filePath, output, error);
            }
            catch (Exception)
            {
                output.WriteLine("DocumentReadStatus: UnexpectedError");
                error.WriteLine("诊断执行失败，未输出报告业务内容。");
                exitCode = UnexpectedFailureExitCode;
            }

            WriteFileSafety(filePath, before, output);
            return exitCode;
        }

        private int RunProductionDiagnostics(
            string filePath,
            TextWriter output,
            TextWriter error)
        {
            ReportCheckResult reportResult = reportCheckService
                .CheckAsync(filePath)
                .GetAwaiter()
                .GetResult();
            output.WriteLine("DocumentReadStatus: " + reportResult.DocumentReadStatus);
            if (!reportResult.IsSuccess)
            {
                error.WriteLine("正式生产流程未能完成读取；未输出任何报告业务值。");
                return ReadFailureExitCode;
            }

            DocumentReadResult document = new DocxDocumentReader().Read(filePath);
            if (!document.IsSuccess)
            {
                error.WriteLine("详细诊断读取未成功；未输出任何报告业务值。");
                return ReadFailureExitCode;
            }

            IReadOnlyList<MoneyOccurrence> moneyOccurrences =
                new MoneyOccurrenceExtractor().Extract(document);
            ConsistencyCheckResult moneyResult =
                new ConsistencyEngine().Check(moneyOccurrences);
            IReadOnlyList<BasicFieldOccurrence> basicOccurrences =
                new BasicFieldExtractor().Extract(document);
            BasicFieldConsistencyCheckResult basicResult =
                new BasicFieldConsistencyEngine().Check(basicOccurrences);
            IReadOnlyList<ReportLevelProjectNameCandidate> projectNameCandidates =
                new ReportLevelProjectNameBlockMatcher().FindMatches(document.Elements);
            ExpectedProjectNameResolution expectedProjectName =
                new ExpectedProjectNameResolver().Resolve(projectNameCandidates);

            WriteMoneySummary(moneyOccurrences, moneyResult, output);
            WriteReportLevelProjectNameSummary(projectNameCandidates, output);
            WriteExpectedProjectNameSummary(expectedProjectName, output);
            WriteBasicFieldSummary(
                basicOccurrences,
                basicResult,
                output);
            WriteUnifiedIssueSummary(reportResult, output);
            WriteIssueDetails(moneyResult.Issues, basicResult.Issues, output);
            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "ElapsedMilliseconds: {0:0.###}",
                reportResult.Duration.TotalMilliseconds));
            return SuccessExitCode;
        }

        private static void WriteExpectedProjectNameSummary(
            ExpectedProjectNameResolution resolution,
            TextWriter output)
        {
            output.WriteLine("ExpectedProjectName:");
            output.WriteLine("  Status: " + resolution.Status);
            output.WriteLine("  HasValue: " + resolution.HasExpectedProjectName);
            output.WriteLine("  CandidateCount: " + resolution.Candidates.Count);
            output.WriteLine("  DistinctCanonicalCount: " +
                resolution.DistinctCanonicalCount);
            output.WriteLine("  FailedCandidateCount: " +
                resolution.FailedCandidates.Count);
        }

        private static void WriteReportLevelProjectNameSummary(
            IReadOnlyCollection<ReportLevelProjectNameCandidate> candidates,
            TextWriter output)
        {
            output.WriteLine("ReportLevelProjectNames:");
            output.WriteLine("  CandidateCount: " + candidates.Count);
            foreach (ReportLevelProjectNameAuthorityRole role in
                Enum.GetValues(typeof(ReportLevelProjectNameAuthorityRole)))
            {
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0}CandidateCount: {1}",
                    role,
                    candidates.Count(item => item.AuthorityRole == role)));
            }

            output.WriteLine("  NormalizationFailureCount: " +
                candidates.Count(item => !item.NormalizationSuccess));
            output.WriteLine("  CanonicalDistinctCount: " +
                candidates
                    .Where(item => item.NormalizationSuccess)
                    .Select(item => item.CanonicalText)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            WriteAuthorityEquality(
                candidates,
                ReportLevelProjectNameAuthorityRole.Cover,
                ReportLevelProjectNameAuthorityRole.Summary,
                "CoverVsSummary",
                output);
            WriteAuthorityEquality(
                candidates,
                ReportLevelProjectNameAuthorityRole.Cover,
                ReportLevelProjectNameAuthorityRole.Body,
                "CoverVsBody",
                output);
            WriteAuthorityEquality(
                candidates,
                ReportLevelProjectNameAuthorityRole.Summary,
                ReportLevelProjectNameAuthorityRole.Body,
                "SummaryVsBody",
                output);
        }

        private static void WriteAuthorityEquality(
            IEnumerable<ReportLevelProjectNameCandidate> candidates,
            ReportLevelProjectNameAuthorityRole firstRole,
            ReportLevelProjectNameAuthorityRole secondRole,
            string label,
            TextWriter output)
        {
            List<ReportLevelProjectNameCandidate> source = candidates.ToList();
            List<ReportLevelProjectNameCandidate> firstCandidates = source
                .Where(item => item.AuthorityRole == firstRole && item.NormalizationSuccess)
                .ToList();
            List<ReportLevelProjectNameCandidate> secondCandidates = source
                .Where(item => item.AuthorityRole == secondRole && item.NormalizationSuccess)
                .ToList();
            string result = firstCandidates.Count != 1 || secondCandidates.Count != 1
                ? "Unavailable"
                : string.Equals(
                    firstCandidates[0].CanonicalText,
                    secondCandidates[0].CanonicalText,
                    StringComparison.Ordinal).ToString();
            output.WriteLine("  " + label + "ExactSame: " + result);
        }

        private static void WriteMoneySummary(
            IReadOnlyCollection<MoneyOccurrence> occurrences,
            ConsistencyCheckResult result,
            TextWriter output)
        {
            output.WriteLine("Money:");
            output.WriteLine("  OccurrenceCount: " + occurrences.Count);
            output.WriteLine("  ComparisonGroupCount: " + result.ComparisonGroups.Count);
            output.WriteLine("  SkippedOccurrenceCount: " + result.SkippedOccurrenceCount);
            output.WriteLine("  IssueCount: " + result.Issues.Count);
            output.WriteLine("  SeriousIssueCount: " +
                result.Issues.Count(item => item.Severity == IssueSeverity.Serious));
            output.WriteLine("  NormalIssueCount: " +
                result.Issues.Count(item => item.Severity == IssueSeverity.Normal));
            output.WriteLine("  ReminderIssueCount: " +
                result.Issues.Count(item => item.Severity == IssueSeverity.Reminder));
        }

        private static void WriteBasicFieldSummary(
            IReadOnlyList<BasicFieldOccurrence> occurrences,
            BasicFieldConsistencyCheckResult result,
            TextWriter output)
        {
            output.WriteLine("BasicFields:");
            foreach (BasicFieldType fieldType in DiagnosticFieldTypes)
            {
                List<BasicFieldOccurrence> fieldOccurrences = occurrences
                    .Where(item => item.FieldType == fieldType)
                    .ToList();
                int distinctCount = fieldOccurrences
                    .Where(item => item.HasNormalizedValue)
                    .Select(item => item.NormalizedValue.CanonicalValue)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                int conflictCount = result.Issues.Count(item =>
                    item.FieldType == fieldType &&
                    item.IssueType == IssueType.BasicFieldConflict);
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0}: OccurrenceCount={1}; CanonicalDistinctCount={2}; ConflictCount={3}",
                    fieldType,
                    fieldOccurrences.Count,
                    distinctCount,
                    conflictCount));

                if (fieldType == BasicFieldType.ProjectName ||
                    fieldType == BasicFieldType.ReportNumber)
                {
                    WriteOccurrenceLocations(fieldType.ToString(), fieldOccurrences, output);
                }
            }
        }

        private static void WriteUnifiedIssueSummary(
            ReportCheckResult result,
            TextWriter output)
        {
            output.WriteLine("Issues:");
            output.WriteLine("  Serious: " + result.SeriousCount);
            output.WriteLine("  Normal: " + result.NormalCount);
            output.WriteLine("  Reminder: " + result.ReminderCount);
            output.WriteLine("  Total: " + result.TotalCount);
        }

        private static void WriteIssueDetails(
            IEnumerable<Issue> moneyIssues,
            IEnumerable<BasicFieldIssue> basicIssues,
            TextWriter output)
        {
            int issueIndex = 0;
            foreach (Issue issue in moneyIssues)
            {
                issueIndex++;
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "Issue[{0}]: Severity={1}; IssueType={2}; Field={3}/{4}/{5}",
                    issueIndex,
                    issue.Severity,
                    issue.IssueType,
                    issue.ComparisonKey.ValueCategory,
                    issue.ComparisonKey.TaxBasis,
                    issue.ComparisonKey.Scope));
                WriteMoneyIssueLocations(issueIndex, issue.Occurrences, output);
            }

            foreach (BasicFieldIssue issue in basicIssues)
            {
                issueIndex++;
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "Issue[{0}]: Severity={1}; IssueType={2}; Field={3}",
                    issueIndex,
                    issue.Severity,
                    issue.IssueType,
                    issue.FieldType));
                WriteBasicIssueLocations(issueIndex, issue.Occurrences, output);
            }

        }

        private static void WriteOccurrenceLocations(
            string prefix,
            IEnumerable<BasicFieldOccurrence> occurrences,
            TextWriter output)
        {
            int index = 0;
            foreach (BasicFieldOccurrence occurrence in occurrences)
            {
                index++;
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0}Location[{1}]: {2}; TextStartIndex={3}; TextLength={4}",
                    prefix,
                    index,
                    FormatLocation(occurrence.Location),
                    occurrence.TextStartIndex,
                    occurrence.TextLength));
            }
        }

        private static void WriteMoneyIssueLocations(
            int issueIndex,
            IEnumerable<MoneyOccurrence> occurrences,
            TextWriter output)
        {
            int occurrenceIndex = 0;
            foreach (MoneyOccurrence occurrence in occurrences)
            {
                occurrenceIndex++;
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  IssueLocation[{0}.{1}]: {2}; TextStartIndex={3}; TextLength={4}",
                    issueIndex,
                    occurrenceIndex,
                    FormatLocation(occurrence.Location),
                    occurrence.TextStartIndex,
                    occurrence.TextLength));
            }
        }

        private static void WriteBasicIssueLocations(
            int issueIndex,
            IEnumerable<BasicFieldOccurrence> occurrences,
            TextWriter output)
        {
            int occurrenceIndex = 0;
            foreach (BasicFieldOccurrence occurrence in occurrences)
            {
                occurrenceIndex++;
                output.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  IssueLocation[{0}.{1}]: {2}; TextStartIndex={3}; TextLength={4}",
                    issueIndex,
                    occurrenceIndex,
                    FormatLocation(occurrence.Location),
                    occurrence.TextStartIndex,
                    occurrence.TextLength));
            }
        }

        private static string FormatLocation(DocumentLocation location)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Part={0}; ParagraphIndex={1}; TableIndex={2}; RowIndex={3}; CellIndex={4}; HeadingDepth={5}",
                location.DocumentPart,
                FormatNullableIndex(location.ParagraphIndex),
                FormatNullableIndex(location.TableIndex),
                FormatNullableIndex(location.RowIndex),
                FormatNullableIndex(location.CellIndex),
                location.HeadingPath.Count);
        }

        private static string FormatNullableIndex(int? index)
        {
            return index.HasValue
                ? index.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
        }

        private static void WriteFileSafety(
            string filePath,
            FileSnapshot before,
            TextWriter output)
        {
            try
            {
                FileSnapshot after = FileSnapshot.Capture(filePath);
                output.WriteLine("SourceSha256Unchanged: " +
                    string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal));
                output.WriteLine("SourceLengthUnchanged: " +
                    (before.Length == after.Length));
                output.WriteLine("SourceLastWriteTimeUnchanged: " +
                    (before.LastWriteTimeUtc == after.LastWriteTimeUtc));
            }
            catch (Exception)
            {
                output.WriteLine("SourceSha256Unchanged: Unavailable");
                output.WriteLine("SourceLengthUnchanged: Unavailable");
                output.WriteLine("SourceLastWriteTimeUnchanged: Unavailable");
            }
        }

        private sealed class FileSnapshot
        {
            private FileSnapshot(string sha256, long length, DateTime lastWriteTimeUtc)
            {
                Sha256 = sha256;
                Length = length;
                LastWriteTimeUtc = lastWriteTimeUtc;
            }

            public string Sha256 { get; }

            public long Length { get; }

            public DateTime LastWriteTimeUtc { get; }

            public static FileSnapshot Capture(string filePath)
            {
                FileInfo info = new FileInfo(filePath);
                string sha256;
                using (SHA256 algorithm = SHA256.Create())
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    sha256 = BitConverter.ToString(algorithm.ComputeHash(stream))
                        .Replace("-", string.Empty);
                }

                info.Refresh();
                return new FileSnapshot(sha256, info.Length, info.LastWriteTimeUtc);
            }
        }
    }
}
