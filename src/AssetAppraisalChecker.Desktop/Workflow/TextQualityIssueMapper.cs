using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.TextQuality;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public sealed class TextQualityIssueMapper
    {
        public IReadOnlyList<ReportCheckIssue> Map(
            SpellCheckResult result,
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

            return result.Candidates
                .Select((candidate, index) => MapCandidate(
                    candidate,
                    sourceOrderStart + index))
                .ToList()
                .AsReadOnly();
        }

        private static ReportCheckIssue MapCandidate(
            TypoCandidate candidate,
            int sourceOrder)
        {
            var occurrence = new ReportCheckOccurrence(
                candidate.OriginalText,
                candidate.SuggestedTerm,
                candidate.Location,
                DocumentLocationFormatter.Format(candidate.Location),
                DocumentLocationFormatter.FormatArea(candidate.Location),
                string.Empty,
                candidate.Evidence.Concat(new[]
                {
                    "原文范围：Start=" + candidate.Span.Start +
                        "，Length=" + candidate.Span.Length,
                    "建议复核术语：" + candidate.SuggestedTerm,
                    "证据类型：" + string.Join("、", candidate.Reasons)
                }),
                new string[0],
                string.Empty,
                false,
                null,
                candidate.SourceDocument);
            return new ReportCheckIssue(
                "TEXT-" + candidate.Kind + "-" + sourceOrder.ToString("D4"),
                IssueType.BasicFieldParseFailed,
                IssueSeverity.Reminder,
                "文字质量",
                GetSummary(candidate.Kind),
                GetReason(candidate.Kind),
                "请结合原文语境人工复核；程序不会自动替换文字。",
                new[] { occurrence },
                sourceOrder,
                BasicFieldType.Unknown,
                candidate.SuggestedTerm,
                "文字质量",
                IssueCategory.Language,
                IssueAuthority.ReviewSuggested,
                "Spell." + candidate.Kind);
        }

        private static string GetSummary(TypoCandidateKind kind)
        {
            if (kind == TypoCandidateKind.KnownTypo) return "发现疑似用词异常";
            if (kind == TypoCandidateKind.ProfessionalTermNearMatch)
                return "发现需复核的近似术语片段";
            return "发现疑似专业术语错别字";
        }

        private static string GetReason(TypoCandidateKind kind)
        {
            if (kind == TypoCandidateKind.KnownTypo)
            {
                return "当前文字命中受控错字规则，建议人工确认。";
            }

            if (kind == TypoCandidateKind.ConfusableProfessionalTerm)
            {
                return "当前文字经受控形近字符替换后形成完整专业术语，建议人工确认。";
            }

            return "当前片段与受控术语的编辑距离为 1，候选唯一；尚不足以确认词语边界或错字，请在详细检查中人工复核。";
        }
    }
}
