using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.TextQuality.Grammar;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class GrammarIssueMapper
    {
        public IReadOnlyList<ReportCheckIssue> Map(
            GrammarCheckResult result,
            int sourceOrderStart)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (sourceOrderStart < 0) throw new ArgumentOutOfRangeException(nameof(sourceOrderStart));

            return result.Candidates
                .Select((candidate, index) => MapCandidate(
                    candidate,
                    sourceOrderStart + index))
                .ToList()
                .AsReadOnly();
        }

        private static ReportCheckIssue MapCandidate(
            ExpressionCandidate candidate,
            int sourceOrder)
        {
            ExpressionEvidence evidence = candidate.Evidence;
            var occurrence = new ReportCheckOccurrence(
                candidate.OriginalSentence,
                evidence.ExpectedStructure,
                candidate.Location,
                DocumentLocationFormatter.Format(candidate.Location),
                DocumentLocationFormatter.FormatArea(candidate.Location),
                string.Empty,
                new[]
                {
                    "规则：" + evidence.RuleId,
                    "表达类型：" + evidence.Kind,
                    "命中表达：" + evidence.MatchedText,
                    "句子范围：Start=" + candidate.Sentence.Span.Start +
                        "，Length=" + candidate.Sentence.Span.Length,
                    "证据范围：Start=" + candidate.EvidenceSpan.Start +
                        "，Length=" + candidate.EvidenceSpan.Length,
                    "稳定身份：" + candidate.StableIdentity
                },
                new string[0],
                candidate.Sentence.Element.NearbyContext?.CurrentText ?? string.Empty,
                false,
                null,
                candidate.SourceDocument);
            return new ReportCheckIssue(
                "GRAMMAR-" + evidence.RuleId + "-" + sourceOrder.ToString("D4"),
                IssueType.BasicFieldParseFailed,
                IssueSeverity.Reminder,
                "语句表达",
                GetSummary(evidence.Kind),
                evidence.Explanation,
                "请结合上下文人工复核语句是否完整；程序不会自动修改原文。",
                new[] { occurrence },
                sourceOrder,
                BasicFieldType.Unknown,
                evidence.ExpectedStructure,
                "语句表达",
                IssueCategory.Language,
                IssueAuthority.ReviewSuggested,
                "Grammar." + evidence.Kind + "." + evidence.RuleId);
        }

        private static string GetSummary(GrammarExpressionKind kind)
        {
            switch (kind)
            {
                case GrammarExpressionKind.IncompleteControlledExpression:
                    return "发现疑似不完整固定表达";
                case GrammarExpressionKind.RepeatedSyntacticSkeleton:
                    return "发现疑似重复句法结构";
                case GrammarExpressionKind.MissingCorrelativeStructure:
                    return "发现疑似不完整关联结构";
                default:
                    return "发现疑似语句承接不完整";
            }
        }
    }
}
