using System;
using System.Linq;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    /// <summary>
    /// Combines an explicit aggregate phrase in the current amount clause with a
    /// summary/conclusion section. The section is only supporting evidence and can
    /// never establish a project subject or ProjectTotal scope on its own.
    /// </summary>
    internal static class ProjectAggregateContextMatcher
    {
        private static readonly string[] ExactTableRowLabels =
        {
            "合计", "总计", "项目合计", "评估结果合计", "评估总值", "评估结果汇总"
        };

        public static bool TryMatch(
            string localContext,
            SectionCategory sectionCategory,
            out string matchedCue)
        {
            matchedCue = string.Empty;
            if (sectionCategory != SectionCategory.ReportSummary &&
                sectionCategory != SectionCategory.ValuationConclusion)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(localContext) ||
                localContext.IndexOf("合计", StringComparison.Ordinal) < 0 ||
                localContext.IndexOf("小计", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            ValueCategory category;
            string valueKeyword;
            if (!BusinessKeywordMatcher.TryClassifyValue(
                localContext,
                out category,
                out valueKeyword))
            {
                return false;
            }

            matchedCue = valueKeyword + "…合计";
            return true;
        }

        public static bool TryMatchTableRowLabel(string cellText, out string matchedCue)
        {
            // Word/WPS may serialize a visually merged aggregate label with tabs,
            // line breaks or spacing between its CJK characters. Whitespace has no
            // business meaning for these exact table-only labels.
            string normalized = new string((cellText ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character))
                .ToArray());
            foreach (string label in ExactTableRowLabels)
            {
                if (string.Equals(normalized, label, StringComparison.Ordinal))
                {
                    matchedCue = label;
                    return true;
                }
            }

            matchedCue = string.Empty;
            return false;
        }
    }
}
