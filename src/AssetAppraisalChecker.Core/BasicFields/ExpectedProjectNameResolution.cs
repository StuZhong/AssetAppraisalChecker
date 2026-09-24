using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public enum ExpectedProjectNameResolutionStatus
    {
        Resolved,
        NoCandidates,
        Ambiguous,
        NormalizationFailed
    }

    /// <summary>
    /// One exact canonical value and every authoritative candidate that produced it.
    /// Candidate references are retained so paragraph, anchor and authority provenance
    /// remain available to later issue generation.
    /// </summary>
    public sealed class ExpectedProjectNameValueGroup
    {
        public ExpectedProjectNameValueGroup(
            string canonicalText,
            IEnumerable<ReportLevelProjectNameCandidate> candidates)
        {
            List<ReportLevelProjectNameCandidate> source =
                (candidates ?? Enumerable.Empty<ReportLevelProjectNameCandidate>()).ToList();
            if (string.IsNullOrWhiteSpace(canonicalText) ||
                source.Count == 0 ||
                source.Any(item => item == null))
            {
                throw new ArgumentException("标准项目名称分组必须包含有效值和来源候选。");
            }

            CanonicalText = canonicalText;
            Candidates = new ReadOnlyCollection<ReportLevelProjectNameCandidate>(source);
            AuthorityRoles = new ReadOnlyCollection<ReportLevelProjectNameAuthorityRole>(
                source
                    .Select(item => item.AuthorityRole)
                    .Distinct()
                    .OrderBy(item => (int)item)
                    .ToList());
        }

        public string CanonicalText { get; }

        public IReadOnlyList<ReportLevelProjectNameCandidate> Candidates { get; }

        public IReadOnlyList<ReportLevelProjectNameAuthorityRole> AuthorityRoles { get; }
    }

    public sealed class ExpectedProjectNameResolution
    {
        internal ExpectedProjectNameResolution(
            ExpectedProjectNameResolutionStatus status,
            string expectedProjectName,
            IEnumerable<ReportLevelProjectNameCandidate> candidates,
            IEnumerable<ExpectedProjectNameValueGroup> valueGroups,
            IEnumerable<ReportLevelProjectNameCandidate> failedCandidates)
        {
            Status = status;
            ExpectedProjectName = expectedProjectName;
            Candidates = new ReadOnlyCollection<ReportLevelProjectNameCandidate>(
                (candidates ?? Enumerable.Empty<ReportLevelProjectNameCandidate>()).ToList());
            ValueGroups = new ReadOnlyCollection<ExpectedProjectNameValueGroup>(
                (valueGroups ?? Enumerable.Empty<ExpectedProjectNameValueGroup>()).ToList());
            FailedCandidates = new ReadOnlyCollection<ReportLevelProjectNameCandidate>(
                (failedCandidates ?? Enumerable.Empty<ReportLevelProjectNameCandidate>()).ToList());
        }

        public ExpectedProjectNameResolutionStatus Status { get; }

        public string ExpectedProjectName { get; }

        public IReadOnlyList<ReportLevelProjectNameCandidate> Candidates { get; }

        public IReadOnlyList<ExpectedProjectNameValueGroup> ValueGroups { get; }

        public IReadOnlyList<ReportLevelProjectNameCandidate> FailedCandidates { get; }

        public int DistinctCanonicalCount => ValueGroups.Count;

        public bool HasExpectedProjectName =>
            Status == ExpectedProjectNameResolutionStatus.Resolved &&
            !string.IsNullOrEmpty(ExpectedProjectName);

        public bool IsAmbiguous =>
            Status == ExpectedProjectNameResolutionStatus.Ambiguous;
    }
}
