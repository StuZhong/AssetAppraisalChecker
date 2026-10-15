using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Mechanical.Formatting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class WhitespaceAndCharacterChecker : IMechanicalDocumentChecker
    {
        private static readonly Regex ConsecutiveSpacesPattern = new Regex(
            " {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex BoundedChineseTermPattern = new Regex(
            @"“(?<term>[\u4e00-\u9fff]+(?: [\u4e00-\u9fff]+)?)”|《(?<term>[\u4e00-\u9fff]+(?: [\u4e00-\u9fff]+)?)》|(?:^|[。；！？\r\n])(?<term>[\u4e00-\u9fff]+(?: [\u4e00-\u9fff]+)?)[：:]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EdgeSpacesPattern = new Regex(
            @"^[ \u3000]+|[ \u3000]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ProsePattern = new Regex(
            @"^[\u4e00-\u9fff].*[\u4e00-\u9fff][。，？！]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => nameof(WhitespaceAndCharacterChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<MechanicalIssue>();
            var confirmedTocTabs = new HashSet<int>();
            IReadOnlyDictionary<int, List<string>> tabEvidence =
                OpenXmlEvidenceLocator.ReadTabEvidence(context, confirmedTocTabs);
            foreach (DocumentElement element in context.Document.Elements)
            {
                string text = element.Text ?? string.Empty;
                AddChineseWordSpaceIssue(issues, context, element);

                if (text.IndexOf('\t') >= 0 && !confirmedTocTabs.Contains(element.DocumentOrderIndex))
                {
                    var evidence = new List<string>
                    {
                        "原始字符=\\t；Unicode=U+0009；元素内字符偏移（从 0 开始）=" +
                            string.Join(",", Enumerable.Range(0, text.Length).Where(index => text[index] == '\t'))
                    };
                    if (tabEvidence.TryGetValue(element.DocumentOrderIndex, out List<string> structuralEvidence))
                        evidence.AddRange(structuralEvidence);
                    AddSimpleIssue(issues, context, element, MechanicalIssueKind.Tab,
                        "发现异常制表符", "正文文本中包含制表符，请核对排版来源。", MechanicalIssueSeverity.Reminder,
                        evidence.ToArray());
                }

                int controlIndex = FindUnexpectedControlCharacter(text);
                if (controlIndex >= 0)
                {
                    AddSimpleIssue(issues, context, element, MechanicalIssueKind.ControlCharacter,
                        "发现异常控制字符", "正文包含可明确识别的非打印控制字符。", MechanicalIssueSeverity.Normal,
                        "Unicode=" + ((int)text[controlIndex]).ToString("X4"));
                }
            }

            AddProseSpaceIssues(context, issues);
            AddConsecutiveBlankParagraphs(context, issues);
            return MechanicalCheckerSupport.ReadOnly(issues);
        }

        private void AddProseSpaceIssues(MechanicalCheckContext context, List<MechanicalIssue> issues)
        {
            if (!context.Document.Elements.Any(item => item.Text.Contains(' ') || item.Text.Contains('\u3000'))) return;
            foreach (SpaceParagraphEvidence paragraph in SpaceParagraphEvidence.Read(context))
            {
                string text = paragraph.Text;
                if (!IsSpaceCheckProse(text)) continue;
                var reported = new List<Match>();
                foreach (Match edge in EdgeSpacesPattern.Matches(text))
                {
                    // Edge whitespace alone has no authority to determine layout. Retain
                    // only a plain-ASCII edge in an explicitly interrupted prose sentence,
                    // with an original adjacent same-style paragraph in the same container.
                    if (edge.Value.Any(value => value != ' ')) continue;
                    bool leading = edge.Index == 0;
                    SpaceParagraphEvidence neighbour = leading ? paragraph.Previous : paragraph.Next;
                    if (neighbour == null || !IsSpaceCheckProse(neighbour.Text)) continue;
                    string before = leading ? neighbour.Text.Trim() : text.Trim();
                    string after = leading ? text.Trim() : neighbour.Text.Trim();
                    if (!before.EndsWith("，", StringComparison.Ordinal) ||
                        !after.EndsWith("。", StringComparison.Ordinal)) continue;
                    AddSpaceIssue(context, issues, paragraph, edge,
                        leading ? MechanicalIssueKind.LeadingSpace : MechanicalIssueKind.TrailingSpace,
                        leading ? "发现正文续句起始处的疑似多余空格" : "发现正文未结束句尾的疑似多余空格",
                        "同容器中原始相邻、同样式正文存在逗号续句证据；ASCII 首尾空格需人工核对，不据此判定排版错误。");
                    reported.Add(edge);
                }

                foreach (Match match in ConsecutiveSpacesPattern.Matches(text))
                {
                    // Exact half-open interval comparison, not paragraph-wide deduplication.
                    if (reported.Any(edge => match.Index < edge.Index + edge.Length && edge.Index < match.Index + match.Length))
                        continue;
                    if (match.Index == 0 || match.Index + match.Length == text.Length ||
                        !IsChinese(text[match.Index - 1]) || !IsChinese(text[match.Index + match.Length])) continue;
                    AddSpaceIssue(context, issues, paragraph, match, MechanicalIssueKind.ConsecutiveSpaces,
                        "发现疑似打断中文正文的连续空格",
                        "完整中文正文中，连续普通空格位于相邻汉字之间；已排除可识别的公式、目录及特殊排版结构，请人工核对。");
                }
            }
        }

        private void AddSpaceIssue(MechanicalCheckContext context, List<MechanicalIssue> issues,
            SpaceParagraphEvidence paragraph, Match match, MechanicalIssueKind kind, string summary, string reason)
        {
            AddSimpleIssue(issues, context, paragraph.Element, kind, summary, reason, MechanicalIssueSeverity.Reminder,
                "字符偏移=" + (paragraph.ElementOffset + match.Index) + "；长度=" + match.Length,
                "段内相对起点=" + match.Index + "；XML 节点=" + paragraph.XmlPath + "（偏移从 0 开始）");
        }

        private static bool IsSpaceCheckProse(string text) => ProsePattern.IsMatch((text ?? "").Trim()) &&
            !Regex.IsMatch(text ?? "", @"[\r\n\t=＝×÷<>＜＞]");

        private static bool IsChinese(char value) => value >= '\u4e00' && value <= '\u9fff';

        private void AddConsecutiveBlankParagraphs(
            MechanicalCheckContext context,
            List<MechanicalIssue> issues)
        {
            // A filtered text list cannot prove adjacency: headings, tables and opaque
            // containers must remain barriers. Inspect the original read-only body blocks.
            // Text-only callers cannot prove the absence of fields/drawings/boundaries.
            if (!File.Exists(context.FilePath) ||
                context.Document.Elements.Count(item => IsBodyBlank(item)) < 2) return;

            var elements = context.Document.Elements
                .Where(item => item.Location.DocumentPart == DocumentPart.MainBody &&
                    !item.Location.TableIndex.HasValue && item.Location.ParagraphIndex.HasValue)
                .GroupBy(item => item.Location.ParagraphIndex.Value)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
            var snapshot = context.FormattingSnapshot;
            var nonProseStyles = new HashSet<string>(snapshot.Styles.Where(item =>
                IsNonProseStyle(item.StyleId) || IsNonProseStyle(item.Name)).Select(item => item.StyleId),
                StringComparer.Ordinal);
            var formats = snapshot.Paragraphs
                .Where(item => item.Location.BodyParagraphIndex.HasValue)
                .ToDictionary(item => item.Location.BodyParagraphIndex.Value);
            using (var stream = new FileStream(context.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (WordprocessingDocument package = WordprocessingDocument.Open(stream, false))
            {
                Body body = package.MainDocumentPart?.Document?.Body;
                if (body == null) return;
                var blocks = body.ChildElements.ToList();
                var paragraphIndexes = body.Elements<Paragraph>()
                    .Select((paragraph, index) => new { paragraph, index })
                    .ToDictionary(item => item.paragraph, item => item.index);
                for (int index = 0; index < blocks.Count; index++)
                {
                    if (!IsXmlBlank(blocks[index])) continue;
                    int start = index;
                    while (index + 1 < blocks.Count && IsXmlBlank(blocks[index + 1])) index++;
                    int end = index;
                    if (end == start || start == 0 || end + 1 >= blocks.Count) continue;

                    var group = new List<DocumentElement>();
                    string styleId = null;
                    bool reliable = true;
                    for (int current = start - 1; current <= end + 1; current++)
                    {
                        Paragraph paragraph = blocks[current] as Paragraph;
                        if (paragraph == null ||
                            !elements.TryGetValue(paragraphIndexes[paragraph], out DocumentElement element) ||
                            !formats.TryGetValue(paragraphIndexes[paragraph], out ParagraphFormattingSnapshot format) ||
                            nonProseStyles.Contains(format.StyleId) ||
                            !IsPlainBodyParagraph(paragraph, element, format) ||
                            (styleId != null && !string.Equals(styleId, format.StyleId, StringComparison.Ordinal)))
                        {
                            reliable = false;
                            break;
                        }
                        styleId = format.StyleId;
                        group.Add(element);
                    }
                    if (!reliable || !HasInterruptedProseEvidence(group[0].Text, group[group.Count - 1].Text))
                        continue;

                    // Retain the existing pair-based issue granularity, but validate the
                    // entire blank run and both neighbours before emitting any pair.
                    for (int current = 1; current + 1 < group.Count - 1; current += 2)
                    {
                        issues.Add(new MechanicalIssue(
                            Name, MechanicalIssueKind.ConsecutiveBlankParagraphs,
                            MechanicalIssueSeverity.Reminder, "空格与字符", "发现疑似打断正文续句的连续空白段落",
                            "前文以逗号结束、后文延续至句末；同样式正文之间存在连续空段，且未发现分页、分节等结构标记。",
                            "请人工核对是否为意外插入；不据此判定实际页面排版错误，程序不会修改文档。",
                            new[]
                            {
                                MechanicalCheckerSupport.Occurrence(context, group[current], string.Empty,
                                    "原始正文结构相邻；同样式正文续句；连续空行（前一处）"),
                                MechanicalCheckerSupport.Occurrence(context, group[current + 1], string.Empty,
                                    "原始正文结构相邻；已排除可识别的结构性空白；连续空行（后一处）")
                            }));
                    }
                }
            }
        }

        private static bool IsBodyBlank(DocumentElement element)
        {
            return MechanicalCheckerSupport.IsBodyParagraph(element) &&
                element.Location.DocumentPart == DocumentPart.MainBody &&
                !element.Location.TableIndex.HasValue && string.IsNullOrWhiteSpace(element.Text);
        }

        private static bool IsXmlBlank(OpenXmlElement block)
        {
            return block is Paragraph && string.IsNullOrWhiteSpace(block.InnerText);
        }

        private static bool IsPlainBodyParagraph(
            Paragraph paragraph, DocumentElement element, ParagraphFormattingSnapshot format)
        {
            if (!MechanicalCheckerSupport.IsBodyParagraph(element) ||
                element.BoundaryMetadata.ContainsExplicitPageBreak ||
                element.BoundaryMetadata.SectionBreakKind != DocumentSectionBreakKind.None ||
                format.StyleResolution != FormattingResolutionStatus.Resolved ||
                (format.PageBreakBefore.IsResolved && format.PageBreakBefore.Value) ||
                (format.KeepNext.IsResolved && format.KeepNext.Value) ||
                (format.NumberingId.IsResolved && format.NumberingId.Value != 0) ||
                (format.EffectiveAlignment.IsResolved && format.EffectiveAlignment.Value != "left" &&
                    format.EffectiveAlignment.Value != "both") ||
                !string.Equals(element.Text, string.Concat(paragraph.Descendants<Text>().Select(item => item.Text)),
                    StringComparison.Ordinal)) return false;

            // Conservative allow-list: fields, hyperlinks, revisions, drawings, equations,
            // breaks, anchors and unknown content are not textually empty spacing evidence.
            if (paragraph.ChildElements.Any(item => !(item is Run) && !(item is ParagraphProperties)) ||
                paragraph.Elements<Run>().Any(run => run.ChildElements.Any(item =>
                    !(item is RunProperties) && !(item is Text))) ||
                paragraph.Descendants().Any(item => item.LocalName == "vanish" ||
                    item.LocalName == "webHidden" || item.LocalName == "rStyle")) return false;
            return paragraph.ParagraphProperties == null || paragraph.ParagraphProperties.ChildElements.All(item =>
                item is ParagraphStyleId || item is Indentation || item is SpacingBetweenLines ||
                item is Justification || item is ParagraphMarkRunProperties);
        }

        private static bool HasInterruptedProseEvidence(string before, string after)
        {
            // Absence of structural markers alone is NOT evidence of an error. Limit this
            // rule to a visible continuation cue; completed paragraphs/layout remain unknown.
            // This is a review candidate, not a company spacing norm or a pagination claim.
            string left = (before ?? string.Empty).TrimEnd();
            string right = (after ?? string.Empty).Trim();
            return left.EndsWith("，", StringComparison.Ordinal) && right.EndsWith("。", StringComparison.Ordinal) &&
                Regex.IsMatch(left, @"[\u4e00-\u9fff]，$") &&
                Regex.IsMatch(right, @"^[\u4e00-\u9fff].*[\u4e00-\u9fff]。$");
        }

        private static bool IsNonProseStyle(string value)
        {
            return Regex.IsMatch(value ?? string.Empty,
                @"^(?:TOC(?:\s*[0-9]|$)|Heading(?:\s*[0-9]|$)|Title|Subtitle|Caption|目录|标题|题注)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private void AddChineseWordSpaceIssue(List<MechanicalIssue> issues,
            MechanicalCheckContext context, DocumentElement element)
        {
            // Han + space + Han is not a Chinese word boundary: it also matches
            // independent agencies and citation components. Require a complete, explicitly
            // bounded term AND an intact local counterpart, not a sliding substring or a
            // phrase exception list. This is review evidence, not an automatic correction.
            var terms = BoundedChineseTermPattern.Matches(element.Text).Cast<Match>()
                .Select(item => item.Groups["term"]).ToList();
            var intact = new HashSet<string>(terms.Where(item => item.Length >= 4 &&
                item.Length <= 20 && !item.Value.Contains(' ')).Select(item => item.Value), StringComparer.Ordinal);
            foreach (Group term in terms.Where(item => item.Value.Contains(' ')))
            {
                string compact = term.Value.Replace(" ", "");
                if (!intact.Contains(compact)) continue;
                int offset = term.Index + term.Value.IndexOf(' ') - 1;
                AddSimpleIssue(issues, context, element, MechanicalIssueKind.ChineseTextSpacing,
                    "完整词项对照中发现疑似插入空格", "同一文本元素内，完整引用或标签存在无空格对照；请人工核对词内空格。",
                    MechanicalIssueSeverity.Reminder, "字符偏移=" + offset + "；长度=3",
                    "依据=完整引用/标签边界与同元素无空格对照；非相邻汉字推断");
                // Preserve the existing one-candidate-per-element granularity.
                break;
            }
        }

        private void AddSimpleIssue(
            List<MechanicalIssue> issues,
            MechanicalCheckContext context,
            DocumentElement element,
            MechanicalIssueKind kind,
            string summary,
            string reason,
            MechanicalIssueSeverity severity,
            params string[] evidence)
        {
            issues.Add(new MechanicalIssue(
                Name,
                kind,
                severity,
                "空格与字符",
                summary,
                reason,
                "请人工确认是否需要在原文中处理。",
                new[] { MechanicalCheckerSupport.Occurrence(context, element, element.Text, evidence) }));
        }

        private static int FindUnexpectedControlCharacter(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (char.IsControl(value) && value != '\r' && value != '\n' && value != '\t')
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
