using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Mechanical.Formatting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical
{
    // Local evidence for the three space rules only. No changes to document extraction,
    // tab/control-character checks, blank-paragraph rules or source navigation.
    internal sealed class SpaceParagraphEvidence
    {
        public DocumentElement Element { get; private set; }
        public string Text { get; private set; }
        public int ElementOffset { get; private set; }
        public string XmlPath { get; private set; }
        public SpaceParagraphEvidence Previous { get; private set; }
        public SpaceParagraphEvidence Next { get; private set; }

        public static IReadOnlyList<SpaceParagraphEvidence> Read(MechanicalCheckContext context)
        {
            if (!File.Exists(context.FilePath))
            {
                // Text-only rule tests have no evidence for paragraph-edge layout.
                return context.Document.Elements.Where(item => item.ElementType == DocumentElementType.Paragraph &&
                    item.Location.DocumentPart == DocumentPart.MainBody && !item.Location.TableIndex.HasValue)
                    .Select(item => new SpaceParagraphEvidence
                    { Element = item, Text = item.Text, XmlPath = "文本输入（无 OOXML 结构证据）" }).ToList();
            }

            var elements = context.Document.Elements.GroupBy(item => Key(item.Location))
                .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());
            DocumentFormattingSnapshot snapshot = context.FormattingSnapshot;
            var formats = snapshot.Paragraphs.ToDictionary(item => FormatKey(item.Location));
            var records = new Dictionary<Paragraph, SpaceParagraphEvidence>();
            using (var stream = new FileStream(context.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (WordprocessingDocument package = WordprocessingDocument.Open(stream, false))
            {
                Body body = package.MainDocumentPart?.Document?.Body;
                if (body == null) return new SpaceParagraphEvidence[0];
                foreach (Paragraph paragraph in body.Descendants<Paragraph>())
                {
                    DocumentLocation location = OpenXmlEvidenceLocator.LocateParagraph(context, paragraph);
                    if (!elements.TryGetValue(Key(location), out DocumentElement element) ||
                        element.ElementType == DocumentElementType.Heading) continue;
                    TableCell cell = paragraph.Parent as TableCell;
                    var siblings = cell == null ? null : cell.Descendants<Paragraph>()
                        .Where(item => item.Ancestors<TableCell>().FirstOrDefault() == cell).ToList();
                    int cellParagraphIndex = cell == null ? 0 : siblings.IndexOf(paragraph);
                    string formatKey = Key(location) + "|" + (cell == null ? "" : cellParagraphIndex.ToString());
                    if (!formats.TryGetValue(formatKey, out ParagraphFormattingSnapshot format) ||
                        !IsPlainProseStructure(paragraph, format, snapshot)) continue;
                    string text = VisibleText(paragraph);
                    string whole = cell == null ? text : string.Join("\n", siblings.Select(VisibleText));
                    if (!string.Equals(whole, element.Text, StringComparison.Ordinal)) continue;
                    records.Add(paragraph, new SpaceParagraphEvidence
                    {
                        Element = element, Text = text,
                        ElementOffset = cell == null ? 0 : siblings.Take(cellParagraphIndex).Sum(item => VisibleText(item).Length + 1),
                        XmlPath = OpenXmlEvidenceLocator.Path(paragraph)
                    });
                }

                foreach (var pair in records)
                {
                    // Never skip over a heading, table, field container or other block.
                    Paragraph previous = pair.Key.PreviousSibling() as Paragraph;
                    Paragraph next = pair.Key.NextSibling() as Paragraph;
                    if (previous != null && SameStyle(previous, pair.Key) && records.TryGetValue(previous, out SpaceParagraphEvidence before))
                        pair.Value.Previous = before;
                    if (next != null && SameStyle(pair.Key, next) && records.TryGetValue(next, out SpaceParagraphEvidence after))
                        pair.Value.Next = after;
                }
            }
            return records.Values.OrderBy(item => item.Element.DocumentOrderIndex).ThenBy(item => item.ElementOffset).ToList();
        }

        private static bool IsPlainProseStructure(Paragraph paragraph, ParagraphFormattingSnapshot format,
            DocumentFormattingSnapshot snapshot)
        {
            if (format.StyleResolution != FormattingResolutionStatus.Resolved ||
                (format.PageBreakBefore.IsResolved && format.PageBreakBefore.Value) ||
                (format.KeepNext.IsResolved && format.KeepNext.Value) ||
                (format.EffectiveAlignment.IsResolved && format.EffectiveAlignment.Value != "left" &&
                    format.EffectiveAlignment.Value != "both") ||
                (format.NumberingId.IsResolved && format.NumberingId.Value != 0) ||
                snapshot.Styles.Any(item => item.StyleId == format.StyleId &&
                    (IsLayoutStyle(item.StyleId) || IsLayoutStyle(item.Name))) ||
                paragraph.ParagraphProperties?.SectionProperties != null ||
                paragraph.ParagraphProperties?.OutlineLevel != null ||
                paragraph.ParagraphProperties?.Tabs != null ||
                paragraph.ChildElements.Any(item => !(item is Run) && !(item is ParagraphProperties)) ||
                paragraph.Elements<Run>().Any(run => run.ChildElements.Any(item => !(item is Text) && !(item is RunProperties))) ||
                format.Runs.Any(item => item.CharacterStyleResolution != FormattingResolutionStatus.Resolved ||
                    (item.Hidden.IsResolved && item.Hidden.Value))) return false;
            return !paragraph.Descendants().Any(item => item.LocalName == "vanish" || item.LocalName == "webHidden");
        }

        private static bool IsLayoutStyle(string value) => System.Text.RegularExpressions.Regex.IsMatch(value ?? "",
            @"^(?:TOC(?:\s*[0-9]|$)|Heading(?:\s*[0-9]|$)|Title|Subtitle|Caption|目录|标题|题注)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static bool SameStyle(Paragraph a, Paragraph b) => string.Equals(
            a.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "",
            b.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "", StringComparison.Ordinal);

        private static string VisibleText(Paragraph paragraph) => string.Concat(paragraph.Descendants().Select(item =>
            item is Text text ? text.Text : item is TabChar ? "\t" : item is Break || item is CarriageReturn ? "\n" : ""));

        private static string Key(DocumentLocation location) => location.DocumentPart + "|" + location.ParagraphIndex +
            "|" + location.TableIndex + "|" + location.RowIndex + "|" + location.CellIndex;

        private static string FormatKey(FormattingParagraphLocation location) => DocumentPart.MainBody + "|" +
            location.BodyParagraphIndex + "|" + location.TableIndex + "|" + location.RowIndex + "|" + location.CellIndex +
            "|" + location.CellParagraphIndex;
    }
}
