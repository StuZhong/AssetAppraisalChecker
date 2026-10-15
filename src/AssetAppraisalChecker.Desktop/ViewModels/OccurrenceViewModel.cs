using System;
using System.Linq;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public sealed class OccurrenceViewModel
    {
        public OccurrenceViewModel(ReportCheckOccurrence occurrence, int displayIndex)
        {
            if (occurrence == null)
            {
                throw new ArgumentNullException(nameof(occurrence));
            }

            DisplayIndex = displayIndex;
            SourceOccurrence = occurrence;
            RawText = occurrence.RawText;
            NormalizedValue = occurrence.NormalizedValue;
            LocationText = occurrence.LocationText;
            Area = occurrence.Area;
            Subject = occurrence.Subject;
            EvidenceText = string.Join(
                Environment.NewLine,
                occurrence.Evidence.Select(item => "• " + item));
            WarningsText = string.Join(
                Environment.NewLine,
                occurrence.Warnings.Select(item => "• " + item));
            NearbyContext = occurrence.NearbyContext;
            IsSuspectedOutlier = occurrence.IsSuspectedOutlier;
            DocumentLabel = occurrence.DocumentLabel;
        }

        public int DisplayIndex { get; }

        public ReportCheckOccurrence SourceOccurrence { get; }

        public string NavigationDisplay =>
            (HasDocumentLabel ? DocumentLabel : "当前文档") + " · 位置 " + DisplayIndex + " · " + LocationText;

        public string RawText { get; }

        public string NormalizedValue { get; }

        public string LocationText { get; }

        public string Area { get; }

        public string Subject { get; }

        public bool HasSubject => !string.IsNullOrWhiteSpace(Subject);

        public string EvidenceText { get; }

        public bool HasEvidence => !string.IsNullOrWhiteSpace(EvidenceText);

        public string WarningsText { get; }

        public bool HasWarnings => !string.IsNullOrWhiteSpace(WarningsText);

        public string NearbyContext { get; }

        public bool HasNearbyContext => !string.IsNullOrWhiteSpace(NearbyContext);

        public bool IsSuspectedOutlier { get; }

        public string DocumentLabel { get; }

        public bool HasDocumentLabel => !string.IsNullOrWhiteSpace(DocumentLabel);

        public string OutlierDisplay => IsSuspectedOutlier ? "疑似异常" : string.Empty;
    }
}
