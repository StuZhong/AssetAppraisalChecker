using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow.Governance;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    internal sealed class TableCalculationIssueMapper
    {
        internal IEnumerable<ReportCheckIssue> Map(IEnumerable<TableCalculationFinding> findings, int order, string label)
        {
            foreach (var finding in findings)
            {
                bool rate = finding.Kind == TableCalculationKind.Rate;
                string field = rate ? "增值率" : "增减值";
                string formula = rate ? "增值率 = 增减值 ÷ 账面价值 × 100%" : "增减值 = 评估价值 − 账面价值";
                yield return new ReportCheckIssue("TABLE-CALC-" + order, IssueType.TableCalculationConflict,
                    IssueSeverity.Normal, field, "评估结果表" + field + "与同一行数据计算不一致",
                    formula + "。按同一行数据计算为 " + finding.CalculatedValue.ToString("0.##########", CultureInfo.InvariantCulture) +
                        (rate ? "%" : "元") + "；已考虑所显示数值的四舍五入范围。不能据此判断具体哪个单元格有误。",
                    "请核对同一行账面价值、评估价值、增减值及增值率；原文不会被修改。",
                    finding.Values.Select(value => new ReportCheckOccurrence(value.Element.Text,
                        value.Value.ToString(CultureInfo.InvariantCulture) + (value.Role.EndsWith("率") ? "%" : "元"),
                        value.Element.Location, DocumentLocationFormatter.Format(value.Element.Location),
                        DocumentLocationFormatter.FormatArea(value.Element.Location), "同一表格、同一行",
                        new[] { "计算字段=" + value.Role, formula, "文档字符偏移=0", "长度=" + value.Element.Text.Length },
                        new string[0], string.Empty, false, documentLabel: label)), order++,
                    issueTypeDisplayOverride: "评估结果表计算不一致", authority: IssueAuthority.Confirmed,
                    governanceKind: "TableCalculation." + finding.Kind);
            }
        }
    }
}
