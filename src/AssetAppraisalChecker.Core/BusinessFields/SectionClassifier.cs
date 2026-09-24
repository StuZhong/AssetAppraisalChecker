using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class SectionClassifier
    {
        public static SectionCategory Classify(
            IReadOnlyList<string> headingPath,
            out string matchedHeading)
        {
            foreach (string heading in (headingPath ?? new string[0]).Reverse())
            {
                if (ContainsAny(heading, "评估结果汇总表", "评估结果表", "评估明细表", "评估结果汇总"))
                {
                    matchedHeading = heading;
                    return SectionCategory.ResultTable;
                }

                if (ContainsAny(heading, "评估结论", "评估结果"))
                {
                    matchedHeading = heading;
                    return SectionCategory.ValuationConclusion;
                }

                if (ContainsAny(heading, "报告摘要", "摘要"))
                {
                    matchedHeading = heading;
                    return SectionCategory.ReportSummary;
                }

                if (ContainsAny(heading, "计算过程", "测算过程", "计算案例", "测算案例"))
                {
                    matchedHeading = heading;
                    return SectionCategory.CalculationProcess;
                }

                if (ContainsAny(heading, "评估说明"))
                {
                    matchedHeading = heading;
                    return SectionCategory.Explanation;
                }

                if (ContainsAny(heading, "附件", "附录"))
                {
                    matchedHeading = heading;
                    return SectionCategory.Appendix;
                }
            }

            matchedHeading = string.Empty;
            return SectionCategory.Other;
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            return keywords.Any(keyword =>
                !string.IsNullOrEmpty(text) &&
                text.IndexOf(keyword, StringComparison.Ordinal) >= 0);
        }
    }
}
