using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Derives an expected project name only when every authoritative full-title
    /// candidate normalizes successfully and exactly one ordinal canonical value
    /// remains. Authority roles and candidate counts never influence the outcome.
    /// </summary>
    public sealed class ExpectedProjectNameResolver
    {
        public ExpectedProjectNameResolution Resolve(
            IEnumerable<ReportLevelProjectNameCandidate> candidates)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            List<ReportLevelProjectNameCandidate> source = candidates.ToList();
            if (source.Any(item => item == null))
            {
                throw new ArgumentException("权威项目名称候选不能包含空项。", nameof(candidates));
            }

            if (source.Count == 0)
            {
                return CreateResolution(
                    ExpectedProjectNameResolutionStatus.NoCandidates,
                    null,
                    source,
                    new ExpectedProjectNameValueGroup[0],
                    new ReportLevelProjectNameCandidate[0]);
            }

            List<ReportLevelProjectNameCandidate> failed = source
                .Where(item =>
                    !item.NormalizationSuccess ||
                    string.IsNullOrWhiteSpace(item.CanonicalText))
                .ToList();
            List<ExpectedProjectNameValueGroup> groups = source
                .Where(item =>
                    item.NormalizationSuccess &&
                    !string.IsNullOrWhiteSpace(item.CanonicalText))
                .GroupBy(item => item.CanonicalText, StringComparer.Ordinal)
                .Select(group => new ExpectedProjectNameValueGroup(group.Key, group.ToList()))
                .ToList();

            if (failed.Count > 0)
            {
                return CreateResolution(
                    ExpectedProjectNameResolutionStatus.NormalizationFailed,
                    null,
                    source,
                    groups,
                    failed);
            }

            if (groups.Count == 1)
            {
                return CreateResolution(
                    ExpectedProjectNameResolutionStatus.Resolved,
                    groups[0].CanonicalText,
                    source,
                    groups,
                    failed);
            }

            return CreateResolution(
                ExpectedProjectNameResolutionStatus.Ambiguous,
                null,
                source,
                groups,
                failed);
        }

        private static ExpectedProjectNameResolution CreateResolution(
            ExpectedProjectNameResolutionStatus status,
            string expectedProjectName,
            IEnumerable<ReportLevelProjectNameCandidate> candidates,
            IEnumerable<ExpectedProjectNameValueGroup> valueGroups,
            IEnumerable<ReportLevelProjectNameCandidate> failedCandidates)
        {
            return new ExpectedProjectNameResolution(
                status,
                expectedProjectName,
                candidates,
                valueGroups,
                failedCandidates);
        }
    }
}
