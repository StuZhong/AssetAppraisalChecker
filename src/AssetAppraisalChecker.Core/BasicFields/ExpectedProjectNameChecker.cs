using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Compares every certified report-level ProjectName candidate with a value
    /// explicitly supplied by the user. Document values never select, vote for,
    /// or replace the expected value.
    /// </summary>
    public sealed class ExpectedProjectNameChecker
    {
        public ProjectNameExpectedCheckResult Check(
            string userExpectedProjectNameRaw,
            IEnumerable<ReportLevelProjectNameCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            List<ReportLevelProjectNameCandidate> source = candidates.ToList();
            if (source.Any(item => item == null))
            {
                throw new ArgumentException(
                    "报告级项目名称候选不能包含空项。",
                    nameof(candidates));
            }

            List<ReportLevelProjectNameCandidate> ordered = OrderCandidates(source);
            if (string.IsNullOrEmpty(userExpectedProjectNameRaw))
            {
                return CreateResult(
                    ProjectNameExpectedCheckStatus.MissingExpectedValue,
                    userExpectedProjectNameRaw,
                    string.Empty,
                    ordered,
                    null,
                    null);
            }

            BasicFieldNormalizationResult expectedNormalization =
                BasicFieldNormalizer.Normalize(
                    BasicFieldType.ProjectName,
                    userExpectedProjectNameRaw);
            if (!expectedNormalization.IsSuccess)
            {
                return CreateResult(
                    ProjectNameExpectedCheckStatus.ExpectedNormalizationFailed,
                    userExpectedProjectNameRaw,
                    string.Empty,
                    ordered,
                    null,
                    null);
            }

            string expectedCanonical =
                expectedNormalization.NormalizedValue.CanonicalValue;
            if (ordered.Count == 0)
            {
                return CreateResult(
                    ProjectNameExpectedCheckStatus.NoAuthoritativeCandidates,
                    userExpectedProjectNameRaw,
                    expectedCanonical,
                    ordered,
                    null,
                    null);
            }

            List<ReportLevelProjectNameCandidate> failed = ordered
                .Where(item =>
                    !item.NormalizationSuccess ||
                    string.IsNullOrWhiteSpace(item.CanonicalText))
                .ToList();
            if (failed.Count > 0)
            {
                return CreateResult(
                    ProjectNameExpectedCheckStatus.CandidateNormalizationFailed,
                    userExpectedProjectNameRaw,
                    expectedCanonical,
                    ordered,
                    null,
                    failed);
            }

            List<ProjectNameExpectedCandidateComparison> comparisons = ordered
                .Select(candidate => new ProjectNameExpectedCandidateComparison(
                    expectedCanonical,
                    candidate,
                    StringComparer.Ordinal.Equals(
                        candidate.CanonicalText,
                        expectedCanonical)
                        ? ProjectNameExpectedComparisonOutcome.Match
                        : ProjectNameExpectedComparisonOutcome.Mismatch))
                .ToList();
            return CreateResult(
                ProjectNameExpectedCheckStatus.ValidExpectedValue,
                userExpectedProjectNameRaw,
                expectedCanonical,
                ordered,
                comparisons,
                failed);
        }

        private static List<ReportLevelProjectNameCandidate> OrderCandidates(
            IEnumerable<ReportLevelProjectNameCandidate> candidates)
        {
            // Ordering is only for deterministic presentation. No authority role,
            // occurrence count or input position influences comparison outcomes.
            return candidates
                .OrderBy(item => item.SourceParagraphs[0].DocumentOrderIndex)
                .ThenBy(item => item.SourceParagraphs[item.SourceParagraphs.Count - 1]
                    .DocumentOrderIndex)
                .ThenBy(item => item.Anchor.DocumentOrderIndex)
                .ThenBy(item => (int)item.AuthorityRole)
                .ThenBy(item => (int)item.ExtractionPattern)
                .ThenBy(item => item.AssembledText, StringComparer.Ordinal)
                .ToList();
        }

        private static ProjectNameExpectedCheckResult CreateResult(
            ProjectNameExpectedCheckStatus status,
            string expectedRawText,
            string expectedCanonicalText,
            IEnumerable<ReportLevelProjectNameCandidate> candidates,
            IEnumerable<ProjectNameExpectedCandidateComparison> comparisons,
            IEnumerable<ReportLevelProjectNameCandidate> failedCandidates)
        {
            return new ProjectNameExpectedCheckResult(
                status,
                expectedRawText,
                expectedCanonicalText,
                candidates,
                comparisons,
                failedCandidates);
        }
    }
}
