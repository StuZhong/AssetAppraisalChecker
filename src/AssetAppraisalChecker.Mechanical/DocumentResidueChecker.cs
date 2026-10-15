using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class DocumentResidueChecker : IMechanicalDocumentChecker
    {
        private const string TemplateFields = "项目名称|报告编号|评估基准日|委托人|产权持有人|评估机构名称";
        private static readonly Regex InstructionPattern = new Regex(
            @"\A\s*(?<residue>【(?:编制说明|编制提示)[：:][^【】\r\n]{1,160}】)\s*\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex DeleteInstructionPattern = new Regex(
            @"[：:，,；;。]定稿前(?:请|应|须|务必)?删除(?:本段|此段|本条|本编制说明)(?=[。！!；;]?】\z)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NegativeInstructionPattern = new Regex(
            @"请勿|不得|禁止|无需|无须|不必|不应|不需|不要求",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PlaceholderPattern = new Regex(
            @"\A\s*(?:(?<label>" + TemplateFields + @")[：:]\s*)?(?<residue>【待填写[：:](?<field>" +
            TemplateFields + @")】|\{\{(?<field>" + TemplateFields + @")\}\})\s*\z",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex CitationContextPattern = new Regex(
            @"引用|引文|摘录|原文|示例|样例|例如|占位符",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => nameof(DocumentResidueChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<MechanicalIssue>();
            using (FileStream stream = new FileStream(
                context.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (WordprocessingDocument document = WordprocessingDocument.Open(stream, false))
            {
                MainDocumentPart mainPart = document.MainDocumentPart;
                AddComments(context, mainPart, issues);
                if (mainPart?.Document != null)
                {
                    AddRevisionResidues<InsertedRun>(
                        context,
                        mainPart.Document,
                        issues,
                        MechanicalIssueKind.InsertedRevision,
                        "文档中仍存在插入修订",
                        "插入修订尚未接受或拒绝。");
                    AddRevisionResidues<DeletedRun>(
                        context,
                        mainPart.Document,
                        issues,
                        MechanicalIssueKind.DeletedRevision,
                        "文档中仍存在删除修订",
                        "删除修订尚未接受或拒绝。");
                    AddRunFormattingResidues(context, mainPart.Document, issues);
                    AddTemplateTextResidues(context, mainPart, issues);
                }
            }

            return MechanicalCheckerSupport.ReadOnly(issues);
        }

        private void AddTemplateTextResidues(
            MechanicalCheckContext context, MainDocumentPart mainPart, List<MechanicalIssue> issues)
        {
            Body body = mainPart.Document.Body;
            if (body == null) return;
            foreach (Paragraph paragraph in body.Descendants<Paragraph>())
            {
                // Only closed, visible, structurally mapped text is eligible. Field results,
                // revisions and unsupported containers are not assumed to be authoring residue.
                if (paragraph.Descendants().Any(node => node.LocalName == "fldChar" ||
                    node.LocalName == "fldSimple" || node.LocalName == "instrText" ||
                    node.LocalName == "ins" || node.LocalName == "del" || node.LocalName == "txbxContent")) continue;
                string visible = string.Concat(paragraph.Descendants().Select(node =>
                    node is Text text ? text.Text : node is TabChar ? "\t" :
                    node is Break || node is CarriageReturn ? "\n" : string.Empty));
                Match instruction = InstructionPattern.Match(visible);
                Match placeholder = PlaceholderPattern.Match(visible);
                bool explicitDelete = instruction.Success && DeleteInstructionPattern.IsMatch(instruction.Value) &&
                    !NegativeInstructionPattern.IsMatch(instruction.Value);
                if (!explicitDelete && !placeholder.Success) continue;
                DocumentLocation location = OpenXmlEvidenceLocator.LocateParagraph(context, paragraph);
                if (!location.ParagraphIndex.HasValue && !location.TableIndex.HasValue) continue;
                DocumentElement element = context.Document.Elements.FirstOrDefault(item =>
                    SameLocation(item.Location, location) && string.Equals(item.Text, visible, StringComparison.Ordinal));
                if (element == null || IsQuotedOrIllustrative(context, element, paragraph, mainPart)) continue;

                if (explicitDelete)
                    AddTemplateIssue(context, issues, element, paragraph, instruction.Groups["residue"],
                        MechanicalIssueKind.TemplateInstructionResidue,
                        "发现未删除的模板编制指示", "独立编制说明明确要求定稿前删除本段，但该说明仍保留在可见正文中。",
                        "TEMPLATE-EXPLICIT-DELETE");

                if (placeholder.Success && (!placeholder.Groups["label"].Success ||
                    placeholder.Groups["label"].Value == placeholder.Groups["field"].Value))
                    AddTemplateIssue(context, issues, element, paragraph, placeholder.Groups["residue"],
                        MechanicalIssueKind.UnreplacedFieldPlaceholder,
                        "发现明确未替换的字段占位符", "独立字段仍为受控的待填写标记，而不是已填写的业务值。",
                        "TEMPLATE-EXPLICIT-PLACEHOLDER");
            }
        }

        private static bool SameLocation(DocumentLocation first, DocumentLocation second) =>
            first.DocumentPart == second.DocumentPart && first.ParagraphIndex == second.ParagraphIndex &&
            first.TableIndex == second.TableIndex && first.RowIndex == second.RowIndex && first.CellIndex == second.CellIndex;

        private static bool IsQuotedOrIllustrative(
            MechanicalCheckContext context, DocumentElement element, Paragraph paragraph, MainDocumentPart mainPart)
        {
            string styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
            string styleName = mainPart.StyleDefinitionsPart?.Styles?.Elements<Style>()
                .FirstOrDefault(style => style.StyleId?.Value == styleId)?.StyleName?.Val?.Value ?? string.Empty;
            if (Regex.IsMatch(styleId + "|" + styleName, "quote|引用|引文", RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant) || CitationContextPattern.IsMatch(element.Text) ||
                element.Text.IndexOfAny(new[] { '“', '”', '‘', '’', '"', '\'' }) >= 0) return true;

            // A literal template example is not a residue finding. Preserve multi-paragraph
            // Chinese quotations; do not treat their interior as the author's instruction.
            int quoteDepth = 0;
            DocumentElement previous = null;
            foreach (DocumentElement prior in context.Document.Elements.OrderBy(item => item.DocumentOrderIndex))
            {
                if (prior.DocumentOrderIndex >= element.DocumentOrderIndex) break;
                foreach (char character in prior.Text)
                {
                    if (character == '“' || character == '‘') quoteDepth++;
                    else if ((character == '”' || character == '’') && quoteDepth > 0) quoteDepth--;
                }
                if (!string.IsNullOrWhiteSpace(prior.Text)) previous = prior;
            }
            return quoteDepth > 0 || (previous != null && previous.Location.ParagraphIndex.HasValue &&
                element.Location.ParagraphIndex.HasValue && CitationContextPattern.IsMatch(previous.Text));
        }

        private void AddTemplateIssue(
            MechanicalCheckContext context, List<MechanicalIssue> issues, DocumentElement element,
            Paragraph paragraph, Group span, MechanicalIssueKind kind, string summary, string reason, string rule)
        {
            issues.Add(new MechanicalIssue(Name, kind, MechanicalIssueSeverity.Normal,
                "模板编制残留", summary, reason, "请人工确认并处理模板标记；程序不会修改或保存文档。",
                new[] { MechanicalCheckerSupport.Occurrence(context, element, span.Value,
                    "RuleId=" + rule, "字符偏移=" + span.Index + "；长度=" + span.Length,
                    "XML 节点=" + OpenXmlEvidenceLocator.Path(paragraph)) }));
        }

        private void AddComments(
            MechanicalCheckContext context,
            MainDocumentPart mainPart,
            List<MechanicalIssue> issues)
        {
            WordprocessingCommentsPart commentsPart = mainPart?.WordprocessingCommentsPart;
            if (commentsPart?.Comments == null)
            {
                return;
            }

            foreach (Comment comment in commentsPart.Comments.Elements<Comment>())
            {
                string text = comment.InnerText ?? string.Empty;
                DocumentFormat.OpenXml.Wordprocessing.Paragraph anchorParagraph = null;
                if (comment.Id != null && mainPart.Document != null)
                {
                    CommentRangeStart anchor = mainPart.Document
                        .Descendants<CommentRangeStart>()
                        .FirstOrDefault(item => item.Id != null && item.Id.Value == comment.Id.Value);
                    anchorParagraph = anchor?.Ancestors<Paragraph>().FirstOrDefault();
                }

                AddResidue(
                    context,
                    issues,
                    MechanicalIssueKind.CommentResidue,
                    "文档中仍存在 Word 批注",
                    "检测到尚未清理的批注。",
                    string.IsNullOrWhiteSpace(text) ? "（空批注）" : text,
                    anchorParagraph?.InnerText,
                    "批注 ID=" + (comment.Id?.Value ?? "未知"));
            }
        }

        private void AddRevisionResidues<T>(
            MechanicalCheckContext context,
            OpenXmlElement root,
            List<MechanicalIssue> issues,
            MechanicalIssueKind kind,
            string summary,
            string reason)
            where T : OpenXmlElement
        {
            foreach (T revision in root.Descendants<T>())
            {
                string raw = revision.InnerText ?? string.Empty;
                Paragraph paragraph = revision.Ancestors<Paragraph>().FirstOrDefault();
                AddResidue(context, issues, kind, summary, reason,
                    string.IsNullOrWhiteSpace(raw) ? "（无可见文字）" : raw,
                    paragraph?.InnerText,
                    "修订类型=" + kind + "；Open XML 元素=" + revision.LocalName,
                    paragraph, revision);
            }
        }

        private void AddRunFormattingResidues(
            MechanicalCheckContext context,
            OpenXmlElement root,
            List<MechanicalIssue> issues)
        {
            foreach (Run run in root.Descendants<Run>())
            {
                RunProperties properties = run.RunProperties;
                if (properties == null)
                {
                    continue;
                }

                Highlight highlight = properties.GetFirstChild<Highlight>();
                if (highlight?.Val != null && highlight.Val.Value != HighlightColorValues.None)
                {
                    AddRunResidue(context, issues, run,
                        MechanicalIssueKind.Highlight,
                        "文档中仍存在高亮",
                        "检测到高亮格式残留。",
                        "高亮颜色=" + highlight.Val.Value);
                }

                Strike strike = properties.GetFirstChild<Strike>();
                DoubleStrike doubleStrike = properties.GetFirstChild<DoubleStrike>();
                if (IsEnabled(strike) || IsEnabled(doubleStrike))
                {
                    AddRunResidue(context, issues, run,
                        MechanicalIssueKind.Strikethrough,
                        "文档中仍存在删除线",
                        "检测到删除线或双删除线格式残留。",
                        doubleStrike != null ? "双删除线" : "删除线");
                }
            }
        }

        private static bool IsEnabled(OnOffType value)
        {
            return value != null && (value.Val == null || value.Val.Value);
        }

        private void AddRunResidue(
            MechanicalCheckContext context,
            List<MechanicalIssue> issues,
            Run run,
            MechanicalIssueKind kind,
            string summary,
            string reason,
            string evidence)
        {
            Paragraph paragraph = run.Ancestors<Paragraph>().FirstOrDefault();
            AddResidue(context, issues, kind, summary, reason,
                string.IsNullOrWhiteSpace(run.InnerText) ? "（无可见文字）" : run.InnerText,
                paragraph?.InnerText,
                evidence);
        }

        private void AddResidue(
            MechanicalCheckContext context,
            List<MechanicalIssue> issues,
            MechanicalIssueKind kind,
            string summary,
            string reason,
            string rawText,
            string locationText,
            string evidence,
            Paragraph structuralParagraph = null,
            OpenXmlElement sourceNode = null)
        {
            var element = sourceNode == null ? MechanicalCheckerSupport.FindBestElement(context, locationText ?? rawText) : null;
            // Revision content (especially w:delText) is absent from visible extraction.
            // Use ancestry rather than matching its text against another paragraph.
            DocumentLocation location = sourceNode == null ? element?.Location ?? MechanicalCheckerSupport.UnknownLocation() :
                OpenXmlEvidenceLocator.LocateParagraph(context, structuralParagraph);
            var details = new List<string> { evidence };
            if (sourceNode != null)
                details.Add("XML 节点=" + OpenXmlEvidenceLocator.Path(sourceNode));
            details.Add(location.ParagraphIndex.HasValue || location.TableIndex.HasValue ?
                "已定位到结构化文本元素" : "未能可靠定位到可见正文元素");
            issues.Add(new MechanicalIssue(
                Name,
                kind,
                MechanicalIssueSeverity.Normal,
                "文档残留",
                summary,
                reason,
                "请在原文中人工确认并按业务要求处理；校检程序不会接受、拒绝或清理任何内容。",
                new[]
                {
                    new MechanicalIssueOccurrence(context.SourceDocument, rawText, location, details)
                }));
        }
    }
}
