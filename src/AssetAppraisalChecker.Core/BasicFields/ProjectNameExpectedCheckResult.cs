using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public enum ProjectNameExpectedCheckStatus
    {
        ValidExpectedValue,
        MissingExpectedValue,
        ExpectedNormalizationFailed,
        NoAuthoritativeCandidates,
        CandidateNormalizationFailed
    }

    public enum ProjectNameExpectedComparisonOutcome
    {
        Match,
        Mismatch
    }

    /// <summary>
    /// One deterministic comparison between a user-confirmed expected value and
    /// one certified report-level candidate. The candidate reference is retained
    /// so its complete multi-paragraph and anchor provenance remains available.
    /// </summary>
    public sealed class ProjectNameExpectedCandidateComparison
    {
        internal ProjectNameExpectedCandidateComparison(
            string expectedCanonicalText,
            ReportLevelProjectNameCandidate candidate,
            ProjectNameExpectedComparisonOutcome outcome)
        {
            if (string.IsNullOrWhiteSpace(expectedCanonicalText))
            {
                throw new ArgumentException("人工项目名称标准值不能为空。", nameof(expectedCanonicalText));
            }

            ExpectedCanonicalText = expectedCanonicalText;
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Outcome = outcome;
        }

        public string ExpectedCanonicalText { get; }

        public ReportLevelProjectNameCandidate Candidate { get; }

        public string CandidateAssembledText => Candidate.AssembledText;

        public string CandidateCanonicalText => Candidate.CanonicalText;

        public ReportLevelProjectNameAuthorityRole AuthorityRole => Candidate.AuthorityRole;

        public IReadOnlyList<DocumentElement> SourceParagraphs => Candidate.SourceParagraphs;

        public DocumentElement Anchor => Candidate.Anchor;

        public ReportLevelProjectNameExtractionPattern ExtractionPattern =>
            Candidate.ExtractionPattern;

        public ProjectNameExpectedComparisonOutcome Outcome { get; }

        public bool IsMatch => Outcome == ProjectNameExpectedComparisonOutcome.Match;

        public bool IsMismatch => Outcome == ProjectNameExpectedComparisonOutcome.Mismatch;
    }

    public sealed class ProjectNameExpectedCheckResult
    {
        internal ProjectNameExpectedCheckResult(
            ProjectNameExpectedCheckStatus status,
            string expectedRawText,
            string expectedCanonicalText,
            IEnumerable<ReportLevelProjectNameCandidate> candidates,
            IEnumerable<ProjectNameExpectedCandidateComparison> comparisons,
            IEnumerable<ReportLevelProjectNameCandidate> failedCandidates)
        {
            Status = status;
            ExpectedRawText = expectedRawText ?? string.Empty;
            ExpectedCanonicalText = expectedCanonicalText ?? string.Empty;
            Candidates = new ReadOnlyCollection<ReportLevelProjectNameCandidate>(
                (candidates ?? Enumerable.Empty<ReportLevelProjectNameCandidate>()).ToList());
            Comparisons = new ReadOnlyCollection<ProjectNameExpectedCandidateComparison>(
                (comparisons ?? Enumerable.Empty<ProjectNameExpectedCandidateComparison>()).ToList());
            FailedCandidates = new ReadOnlyCollection<ReportLevelProjectNameCandidate>(
                (failedCandidates ?? Enumerable.Empty<ReportLevelProjectNameCandidate>()).ToList());
        }

        public ProjectNameExpectedCheckStatus Status { get; }

        public string ExpectedRawText { get; }

        public string ExpectedCanonicalText { get; }

        public IReadOnlyList<ReportLevelProjectNameCandidate> Candidates { get; }

        public IReadOnlyList<ProjectNameExpectedCandidateComparison> Comparisons { get; }

        public IReadOnlyList<ReportLevelProjectNameCandidate> FailedCandidates { get; }

        public bool HasValidExpectedValue =>
            Status == ProjectNameExpectedCheckStatus.ValidExpectedValue &&
            !string.IsNullOrEmpty(ExpectedCanonicalText);

        public int MatchCount => Comparisons.Count(item => item.IsMatch);

        public int MismatchCount => Comparisons.Count(item => item.IsMismatch);

        public bool AllMatched =>
            HasValidExpectedValue &&
            Comparisons.Count > 0 &&
            MismatchCount == 0;
    }
}
