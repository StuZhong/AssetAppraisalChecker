using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Mechanical.Formatting;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class ParagraphFormatChecker : IMechanicalDocumentChecker
    {
        private static readonly Regex ResultLine = new Regex(
            @"^[=＝]\s*[+\-－−]?(?:\d{1,3}(?:[,，]\d{3})+|\d+)(?:[.．]\d+)?\s*(?:亿元|万元|元|%|％)?\s*(?:[（(](?:取整|四舍五入)[）)])?\s*[。;；]?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NumberedLabel = new Regex(
            @"^(?:[（(][一二三四五六七八九十百0-9]+[）)]|[一二三四五六七八九十百]+、|\d+[、.．])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Arithmetic = new Regex(@"[+×÷*/－−]|\d\s*-\s*\d",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => nameof(ParagraphFormatChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var builder = new FormattingPeerGroupBuilder();
            IReadOnlyList<FormattingPeerParagraph> paragraphs =
                builder.Build(context.FormattingSnapshot);
            var tableBuilder = paragraphs.Any(item => item.Paragraph.IsInTable)
                ? new TableParagraphFormatPeerGroupBuilder(context.FilePath) : null;
            var issues = new List<MechanicalIssue>();
            // Paragraph-format comparability is narrower than font comparability. Do not
            // change the shared builder (and thereby change FontStyleChecker's behavior).
            var roles = paragraphs.ToDictionary(item => item, ClassifyContent);
            var groups = paragraphs.GroupBy(item => roles[item]).ToDictionary(
                group => group.Key, group => (IReadOnlyList<FormattingPeerParagraph>)group.ToList());
            foreach (FormattingPeerParagraph current in paragraphs)
            {
                ContentRole role = roles[current];
                if (current.Role == FormattingParagraphRole.Other || role == ContentRole.Uncertain) continue;
                IReadOnlyList<FormattingPeerParagraph> comparable = groups[role];
                IReadOnlyList<FormattingPeerParagraph> peers = current.Paragraph.IsInTable
                    ? tableBuilder.GetLocalPeers(current, comparable) : builder.GetLocalPeers(current, comparable);
                if (peers.Count < FormattingPeerGroupBuilder.MinimumComparablePeers) continue;

                ParagraphFormattingSnapshot paragraph = current.Paragraph;
                var differences = new List<FormattingDifference>
                {
                    FormattingOutlierAnalysis.AnalyzeString(
                        "对齐方式", paragraph.EffectiveAlignment, peers,
                        item => item.Paragraph.EffectiveAlignment),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "首行缩进", paragraph.EffectiveFirstLineIndent, peers,
                        item => item.Paragraph.EffectiveFirstLineIndent),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "悬挂缩进", paragraph.EffectiveHangingIndent, peers,
                        item => item.Paragraph.EffectiveHangingIndent),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "左缩进", paragraph.EffectiveLeftIndent, peers,
                        item => item.Paragraph.EffectiveLeftIndent),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "右缩进", paragraph.EffectiveRightIndent, peers,
                        item => item.Paragraph.EffectiveRightIndent),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "行距", paragraph.EffectiveLineSpacing, peers,
                        item => item.Paragraph.EffectiveLineSpacing),
                    FormattingOutlierAnalysis.AnalyzeString(
                        "行距规则", paragraph.EffectiveLineRule, peers,
                        item => item.Paragraph.EffectiveLineRule),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "段前距", paragraph.EffectiveSpacingBefore, peers,
                        item => item.Paragraph.EffectiveSpacingBefore),
                    FormattingOutlierAnalysis.AnalyzeNumber(
                        "段后距", paragraph.EffectiveSpacingAfter, peers,
                        item => item.Paragraph.EffectiveSpacingAfter)
                };
                List<FormattingDifference> actual = differences
                    .Where(item => item != null)
                    .ToList();
                if (actual.Count == 0) continue;

                issues.Add(FormattingQualityIssueSupport.Create(
                    context,
                    MechanicalIssueKind.ParagraphFormatOutlier,
                    "段落格式",
                    "疑似格式异常：该段落的对齐、缩进或间距与附近同类内容不一致",
                    current.Paragraph.IsInTable
                        ? "同一表格、同一逻辑列、同一行角色及相同合并结构中，至少 4 个独立行的同类段落可比较，至少 80% 形成相同主要格式，当前段落存在明确差异。无法可靠换算的单位不会参与比较。"
                        : "同一局部同类段落组中至少 80% 的可比较段落形成相同主要格式，当前段落存在明确差异。无法可靠换算的单位不会参与比较。",
                    current,
                    actual));
            }

            return issues.AsReadOnly();
        }

        private enum ContentRole { Uncertain, Body, ShortLabel, Formula, FormulaContinuation, CalculationResult, TableContent }

        private static ContentRole ClassifyContent(FormattingPeerParagraph item)
        {
            // Use full run text, not the truncated UI preview. Empty/hidden-only paragraphs
            // have no visible text whose layout can be proven abnormal from spacing alone.
            string text = string.Concat(item.Paragraph.Runs
                .Where(run => !(run.Hidden.IsResolved && run.Hidden.Value))
                .Select(run => run.Text)).Trim();
            if (!text.Any(character => !char.IsWhiteSpace(character) && !char.IsControl(character) &&
                character != '\u200B' && character != '\uFEFF')) return ContentRole.Uncertain;
            if (item.Paragraph.IsInTable) return ContentRole.TableContent;

            if (ResultLine.IsMatch(text)) return ContentRole.CalculationResult;
            bool equals = text.IndexOf('=') >= 0 || text.IndexOf('＝') >= 0;
            bool arithmetic = Arithmetic.IsMatch(text) && text.Any(char.IsDigit);
            if (text[0] == '=' || text[0] == '＝')
                return arithmetic ? ContentRole.FormulaContinuation : ContentRole.Uncertain;
            if (equals) return arithmetic ? ContentRole.Formula : ContentRole.Uncertain;
            if (arithmetic && "+×÷*/－−".IndexOf(text[0]) >= 0)
                return ContentRole.FormulaContinuation;
            if (text.Length <= 60 && NumberedLabel.IsMatch(text) &&
                !Regex.IsMatch(NumberedLabel.Replace(text, ""), @"[，。！？；.!?;]")) return ContentRole.ShortLabel;
            return ContentRole.Body;
        }
    }
}
