using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Production-callable ProjectName-specific path. It only discovers certified
    /// report-level candidates and compares them with a user-provided expected
    /// value; legacy BasicField occurrences and the generic consistency engine are
    /// deliberately outside this coordinator.
    /// </summary>
    public sealed class ReportLevelProjectNameCheckCoordinator
    {
        private readonly ReportLevelProjectNameBlockMatcher matcher;
        private readonly ExpectedProjectNameChecker checker;

        public ReportLevelProjectNameCheckCoordinator()
            : this(
                new ReportLevelProjectNameBlockMatcher(),
                new ExpectedProjectNameChecker())
        {
        }

        public ReportLevelProjectNameCheckCoordinator(
            ReportLevelProjectNameBlockMatcher matcher,
            ExpectedProjectNameChecker checker)
        {
            this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            this.checker = checker ?? throw new ArgumentNullException(nameof(checker));
        }

        public ProjectNameExpectedCheckResult Check(
            string userExpectedProjectNameRaw,
            IEnumerable<DocumentElement> documentElements)
        {
            if (documentElements == null)
            {
                throw new ArgumentNullException(nameof(documentElements));
            }

            IReadOnlyList<ReportLevelProjectNameCandidate> candidates =
                matcher.FindMatches(documentElements);
            return checker.Check(userExpectedProjectNameRaw, candidates);
        }
    }
}
