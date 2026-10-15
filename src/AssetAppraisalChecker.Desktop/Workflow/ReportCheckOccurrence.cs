using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class ReportCheckOccurrence
    {
        public ReportCheckOccurrence(
            string rawText,
            string normalizedValue,
            DocumentLocation location,
            string locationText,
            string area,
            string subject,
            IEnumerable<string> evidence,
            IEnumerable<string> warnings,
            string nearbyContext,
            bool isSuspectedOutlier,
            ReportLevelProjectNameCandidate projectNameCandidate = null,
            string documentLabel = null)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            RawText = rawText ?? string.Empty;
            NormalizedValue = normalizedValue ?? string.Empty;
            LocationText = locationText ?? string.Empty;
            Area = area ?? string.Empty;
            Subject = subject ?? string.Empty;
            Evidence = new ReadOnlyCollection<string>(
                (evidence ?? Enumerable.Empty<string>()).ToList());
            Warnings = new ReadOnlyCollection<string>(
                (warnings ?? Enumerable.Empty<string>()).ToList());
            NearbyContext = nearbyContext ?? string.Empty;
            IsSuspectedOutlier = isSuspectedOutlier;
            ProjectNameCandidate = projectNameCandidate;
            DocumentLabel = documentLabel ?? string.Empty;
        }

        public string RawText { get; }

        public string NormalizedValue { get; }

        public DocumentLocation Location { get; }

        public string LocationText { get; }

        public string Area { get; }

        public string Subject { get; }

        public IReadOnlyList<string> Evidence { get; }

        public IReadOnlyList<string> Warnings { get; }

        public string NearbyContext { get; }

        public bool IsSuspectedOutlier { get; }

        public ReportLevelProjectNameCandidate ProjectNameCandidate { get; }

        public string DocumentLabel { get; }
    }
}
