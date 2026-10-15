using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Mechanical.Formatting;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class FontStyleChecker : IMechanicalDocumentChecker
    {
        public string Name => nameof(FontStyleChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var builder = new FormattingPeerGroupBuilder();
            IReadOnlyList<FormattingPeerParagraph> paragraphs =
                builder.Build(context.FormattingSnapshot);
            // Numeric script alone does not make serial numbers and amounts comparable.
            // Reuse the structural column/row-role/merge isolation used by paragraph formatting.
            var tableBuilder = paragraphs.Any(item => item.Paragraph.IsInTable)
                ? new TableParagraphFormatPeerGroupBuilder(context.FilePath) : null;
            var issues = new List<MechanicalIssue>();
            foreach (FormattingPeerParagraph current in paragraphs)
            {
                if (current.Role == FormattingParagraphRole.Other ||
                    !current.Representative.IsUsable)
                {
                    continue;
                }

                IReadOnlyList<FormattingPeerParagraph> peers = (current.Paragraph.IsInTable
                    ? tableBuilder.GetLocalPeers(current, paragraphs)
                    : builder.GetLocalPeers(current, paragraphs))
                    .Where(item => item.Representative.IsUsable)
                    .ToList()
                    .AsReadOnly();
                if (peers.Count < FormattingPeerGroupBuilder.MinimumComparablePeers) continue;

                RunFormattingSnapshot run = current.Representative.Run;
                var differences = new List<FormattingDifference>();
                if (current.ScriptClass == FormattingScriptClass.Cjk ||
                    current.ScriptClass == FormattingScriptClass.Mixed)
                {
                    differences.Add(FormattingOutlierAnalysis.AnalyzeString(
                        "中文字体",
                        run.EffectiveFontEastAsia,
                        peers,
                        item => item.Representative.Run.EffectiveFontEastAsia));
                }
                if (current.ScriptClass == FormattingScriptClass.LatinOrNumber ||
                    current.ScriptClass == FormattingScriptClass.Mixed)
                {
                    differences.Add(FormattingOutlierAnalysis.AnalyzeString(
                        "西文字体（Ascii）",
                        run.EffectiveFontAscii,
                        peers,
                        item => item.Representative.Run.EffectiveFontAscii));
                    differences.Add(FormattingOutlierAnalysis.AnalyzeString(
                        "西文字体（HAnsi）",
                        run.EffectiveFontHighAnsi,
                        peers,
                        item => item.Representative.Run.EffectiveFontHighAnsi));
                }

                differences.Add(FormattingOutlierAnalysis.AnalyzeNumber(
                    "字号", run.EffectiveFontSize, peers,
                    item => item.Representative.Run.EffectiveFontSize));
                differences.Add(FormattingOutlierAnalysis.AnalyzeBoolean(
                    "粗体", run.Bold, peers,
                    item => item.Representative.Run.Bold));
                differences.Add(FormattingOutlierAnalysis.AnalyzeBoolean(
                    "斜体", run.Italic, peers,
                    item => item.Representative.Run.Italic));
                differences.Add(FormattingOutlierAnalysis.AnalyzeString(
                    "下划线", run.Underline, peers,
                    item => item.Representative.Run.Underline));
                differences.Add(FormattingOutlierAnalysis.AnalyzeString(
                    "颜色", run.Color, peers,
                    item => item.Representative.Run.Color));

                List<FormattingDifference> actual = differences
                    .Where(item => item != null)
                    .ToList();
                if (actual.Count == 0) continue;

                issues.Add(FormattingQualityIssueSupport.Create(
                    context,
                    MechanicalIssueKind.FontStyleOutlier,
                    "字体/字号",
                    "疑似格式异常：该段落的文字格式与附近同类内容不一致",
                    current.Paragraph.IsInTable
                        ? "同一表格、同一逻辑列、同一行角色及合并结构下，至少 4 个独立行的可比较段落中至少 80% 形成相同主要格式，当前段落存在明确差异。该结果不代表公司固定格式标准。"
                        : "同一局部同类段落组中至少 80% 的可比较段落形成相同主要格式，当前段落存在明确差异。该结果不代表公司固定格式标准。",
                    current,
                    actual));
            }

            return issues.AsReadOnly();
        }
    }
}
