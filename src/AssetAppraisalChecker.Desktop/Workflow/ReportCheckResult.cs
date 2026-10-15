using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services.Navigation;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class ReportCheckResult
    {
        public ReportCheckResult(
            string filePath,
            string fileName,
            DateTimeOffset startedAt,
            DateTimeOffset completedAt,
            TimeSpan duration,
            DocumentReadStatus documentReadStatus,
            int moneyOccurrenceCount,
            int basicFieldOccurrenceCount,
            IEnumerable<ReportCheckIssue> issues,
            bool isSuccess,
            string message,
            ProjectNameExpectedCheckResult projectNameExpectedCheck = null,
            string documentBFilePath = null,
            string documentBFileName = null,
            DocumentReadStatus? documentBReadStatus = null,
            IEnumerable<CheckedDocumentSource> sourceDocuments = null)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            if (moneyOccurrenceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moneyOccurrenceCount));
            }

            if (basicFieldOccurrenceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basicFieldOccurrenceCount));
            }

            FilePath = filePath ?? string.Empty;
            FileName = fileName ?? string.Empty;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            Duration = duration;
            DocumentReadStatus = documentReadStatus;
            MoneyOccurrenceCount = moneyOccurrenceCount;
            BasicFieldOccurrenceCount = basicFieldOccurrenceCount;
            Issues = new ReadOnlyCollection<ReportCheckIssue>(
                (issues ?? throw new ArgumentNullException(nameof(issues))).ToList());
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
            ProjectNameExpectedCheck = projectNameExpectedCheck;
            DocumentBFilePath = documentBFilePath ?? string.Empty;
            DocumentBFileName = documentBFileName ?? string.Empty;
            DocumentBReadStatus = documentBReadStatus;
            SourceDocuments = new ReadOnlyCollection<CheckedDocumentSource>(
                (sourceDocuments ?? Enumerable.Empty<CheckedDocumentSource>()).ToList());
        }

        public string FilePath { get; }

        public string FileName { get; }

        public DateTimeOffset StartedAt { get; }

        public DateTimeOffset CompletedAt { get; }

        public TimeSpan Duration { get; }

        public DocumentReadStatus DocumentReadStatus { get; }

        public int MoneyOccurrenceCount { get; }

        public int BasicFieldOccurrenceCount { get; }

        public IReadOnlyList<ReportCheckIssue> Issues { get; }

        public int SeriousCount => Issues.Count(item => item.Severity == IssueSeverity.Serious);

        public int NormalCount => Issues.Count(item => item.Severity == IssueSeverity.Normal);

        public int ReminderCount => Issues.Count(item => item.Severity == IssueSeverity.Reminder);

        public int TotalCount => Issues.Count;

        public bool IsSuccess { get; }

        public string Message { get; }

        public ProjectNameExpectedCheckResult ProjectNameExpectedCheck { get; }

        public string DocumentBFilePath { get; }

        public string DocumentBFileName { get; }

        public DocumentReadStatus? DocumentBReadStatus { get; }

        public bool IsCrossDocumentCheck => !string.IsNullOrWhiteSpace(DocumentBFilePath);

        public IReadOnlyList<CheckedDocumentSource> SourceDocuments { get; }
    }
}
