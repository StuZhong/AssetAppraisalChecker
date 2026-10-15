using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssetAppraisalChecker.Desktop.Workflow.Governance
{
    public sealed class IssueGovernanceProcessor
    {
        public IReadOnlyList<ReportCheckIssue> Deduplicate(
            IEnumerable<ReportCheckIssue> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<ReportCheckIssue>();
            foreach (ReportCheckIssue issue in issues)
            {
                if (issue == null)
                {
                    throw new ArgumentException("Issue 集合不能包含 null。", nameof(issues));
                }

                if (stableIds.Add(issue.StableId))
                {
                    result.Add(issue);
                }
            }

            return new ReadOnlyCollection<ReportCheckIssue>(result);
        }
    }
}
