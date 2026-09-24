using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    /// <summary>
    /// Maps only explicit PN-B4 mismatch comparisons to Desktop issues. Incomplete
    /// or failed checks remain statuses and never become false mismatch issues.
    /// </summary>
    public sealed class ProjectNameExpectedIssueMapper
    {
        public IReadOnlyList<ReportCheckIssue> Map(
            ProjectNameExpectedCheckResult result,
            int sourceOrderStart)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (sourceOrderStart < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrderStart));
            }

            if (result.Status != ProjectNameExpectedCheckStatus.ValidExpectedValue)
            {
                return new ReadOnlyCollection<ReportCheckIssue>(
                    new ReportCheckIssue[0]);
            }

            var issues = new List<ReportCheckIssue>();
            foreach (ProjectNameExpectedCandidateComparison comparison in
                result.Comparisons.Where(item => item.IsMismatch))
            {
                issues.Add(MapMismatch(
                    comparison,
                    issues.Count,
                    sourceOrderStart + issues.Count));
            }

            return new ReadOnlyCollection<ReportCheckIssue>(issues);
        }

        private static ReportCheckIssue MapMismatch(
            ProjectNameExpectedCandidateComparison comparison,
            int mismatchIndex,
            int sourceOrder)
        {
            ReportLevelProjectNameCandidate candidate = comparison.Candidate;
            string authorityDisplay = GetAuthorityDisplayName(candidate.AuthorityRole);
            string sourceLocations = string.Join(
                "；",
                candidate.SourceParagraphs.Select(item =>
                    DocumentLocationFormatter.Format(item.Location)));
            var occurrence = new ReportCheckOccurrence(
                candidate.AssembledText,
                candidate.CanonicalText,
                candidate.SourceParagraphs[0].Location,
                DocumentLocationFormatter.Format(candidate.SourceParagraphs[0].Location),
                authorityDisplay,
                string.Empty,
                new[]
                {
                    string.Format("AuthorityRole={0}", candidate.AuthorityRole),
                    string.Format("ExtractionPattern={0}", candidate.ExtractionPattern),
                    "完整项目名称来源位置：" + sourceLocations,
                    "权威标题锚点位置：" +
                        DocumentLocationFormatter.Format(candidate.Anchor.Location),
                    "该位置仅与用户人工输入的正确项目名称进行精确标准值比较。"
                },
                new string[0],
                string.Empty,
                false,
                candidate);
            return new ReportCheckIssue(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "PROJECT-NAME-EXPECTED-{0}-{1:D2}",
                    candidate.AuthorityRole.ToString().ToUpperInvariant(),
                    mismatchIndex + 1),
                IssueType.ProjectNameExpectedMismatch,
                IssueSeverity.Serious,
                "项目名称",
                string.Format(
                    "{0}项目名称与输入的正确项目名称不一致",
                    authorityDisplay),
                string.Format(
                    "{0}的报告级项目名称“{1}”与用户输入的正确项目名称“{2}”不一致。",
                    authorityDisplay,
                    candidate.CanonicalText,
                    comparison.ExpectedCanonicalText),
                string.Format("请人工核对并修正报告{0}的正式项目名称。", authorityDisplay),
                new[] { occurrence },
                sourceOrder,
                BasicFieldType.ProjectName,
                comparison.ExpectedCanonicalText);
        }

        private static string GetAuthorityDisplayName(
            ReportLevelProjectNameAuthorityRole authorityRole)
        {
            switch (authorityRole)
            {
                case ReportLevelProjectNameAuthorityRole.Cover:
                    return "封面";
                case ReportLevelProjectNameAuthorityRole.Summary:
                    return "报告摘要";
                case ReportLevelProjectNameAuthorityRole.Body:
                    return "报告正文";
                default:
                    return "报告级位置";
            }
        }
    }
}
