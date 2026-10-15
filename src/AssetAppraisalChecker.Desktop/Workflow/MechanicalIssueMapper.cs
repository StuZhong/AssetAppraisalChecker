using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Desktop.Workflow.Governance;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class MechanicalIssueMapper
    {
        public IReadOnlyList<ReportCheckIssue> Map(
            IEnumerable<MechanicalIssue> source,
            int sourceOrderStart)
        {
            if (sourceOrderStart < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrderStart));
            }

            return (source ?? throw new ArgumentNullException(nameof(source)))
                .Select((issue, index) => MapIssue(issue, sourceOrderStart + index))
                .ToList()
                .AsReadOnly();
        }

        private static ReportCheckIssue MapIssue(MechanicalIssue issue, int sourceOrder)
        {
            IssueGovernanceClassification governance =
                IssueGovernancePolicy.ForMechanical(issue.Kind);
            IReadOnlyList<ReportCheckOccurrence> occurrences = issue.Occurrences
                .Select(item => new ReportCheckOccurrence(
                    item.RawText,
                    string.Empty,
                    item.Location,
                    DocumentLocationFormatter.Format(item.Location),
                    DocumentLocationFormatter.FormatArea(item.Location),
                    string.Empty,
                    item.Evidence,
                    new string[0],
                    string.Empty,
                    false,
                    null,
                    item.SourceDocument))
                .ToList()
                .AsReadOnly();

            return new ReportCheckIssue(
                "MECH-" + issue.Kind + "-" + sourceOrder.ToString("D4"),
                IssueType.BasicFieldParseFailed,
                issue.Severity == MechanicalIssueSeverity.Normal
                    ? IssueSeverity.Normal
                    : IssueSeverity.Reminder,
                issue.FieldName,
                issue.Summary,
                issue.Reason,
                issue.SuggestedAction,
                occurrences,
                sourceOrder,
                BasicFieldType.Unknown,
                null,
                GetIssueTypeDisplay(issue.Kind),
                governance.Category,
                governance.Authority,
                governance.GovernanceKind);
        }

        private static string GetIssueTypeDisplay(MechanicalIssueKind kind)
        {
            switch (kind)
            {
                case MechanicalIssueKind.DuplicateWord:
                case MechanicalIssueKind.DuplicateSentence:
                case MechanicalIssueKind.DuplicateParagraph:
                    return "重复内容";
                case MechanicalIssueKind.ConsecutiveSpaces:
                case MechanicalIssueKind.ChineseTextSpacing:
                case MechanicalIssueKind.LeadingSpace:
                case MechanicalIssueKind.TrailingSpace:
                case MechanicalIssueKind.Tab:
                case MechanicalIssueKind.ConsecutiveBlankParagraphs:
                case MechanicalIssueKind.ControlCharacter:
                    return "空格与异常字符";
                case MechanicalIssueKind.RepeatedPunctuation:
                case MechanicalIssueKind.UnpairedChineseBracket:
                case MechanicalIssueKind.BracketWidthMismatch:
                case MechanicalIssueKind.UnpairedChineseQuote:
                case MechanicalIssueKind.UnpairedBookTitle:
                case MechanicalIssueKind.MissingEndingPunctuation:
                    return "标点问题";
                case MechanicalIssueKind.SequenceJump:
                case MechanicalIssueKind.SequenceDuplicate:
                case MechanicalIssueKind.SequenceReverse:
                case MechanicalIssueKind.SequenceStyleMismatch:
                    return "序号问题";
                case MechanicalIssueKind.FontStyleOutlier:
                    return "字体/字号";
                case MechanicalIssueKind.ParagraphFormatOutlier:
                    return "段落格式";
                case MechanicalIssueKind.UnitConsistency:
                    return "单位问题";
                default:
                    return "文档残留";
            }
        }
    }
}
