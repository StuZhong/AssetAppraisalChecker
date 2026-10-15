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
    // Evidence-only mapping. Deleted text need not exist in the visible DocumentReadResult.
    internal static class OpenXmlEvidenceLocator
    {
        public static DocumentLocation LocateParagraph(MechanicalCheckContext context, Paragraph paragraph)
        {
            if (paragraph?.Parent is Body body)
            {
                int index = body.Elements<Paragraph>().TakeWhile(item => item != paragraph).Count();
                return ExistingLocation(context, index, null, null, null);
            }

            TableCell cell = paragraph?.Ancestors<TableCell>().FirstOrDefault();
            TableRow row = cell?.Parent as TableRow;
            Table table = row?.Parent as Table;
            if (table?.Parent is Body tableBody)
            {
                int tableIndex = tableBody.Elements<Table>().TakeWhile(item => item != table).Count();
                int rowIndex = table.Elements<TableRow>().TakeWhile(item => item != row).Count();
                int cellIndex = row.Elements<TableCell>().TakeWhile(item => item != cell).Count();
                return ExistingLocation(context, null, tableIndex, rowIndex, cellIndex);
            }

            // Do not invent a visible-text index for an unsupported container.
            return MechanicalCheckerSupport.UnknownLocation();
        }

        public static string Path(OpenXmlElement node)
        {
            var parts = new List<string>();
            for (OpenXmlElement current = node; current != null; current = current.Parent)
            {
                int index = current.Parent == null ? 1 : current.Parent.ChildElements
                    .TakeWhile(item => item != current)
                    .Count(item => item.LocalName == current.LocalName && item.NamespaceUri == current.NamespaceUri) + 1;
                parts.Add(current.LocalName + "[" + index + "]");
            }

            parts.Reverse();
            return "/" + string.Join("/", parts);
        }

        public static IReadOnlyDictionary<int, List<string>> ReadTabEvidence(
            MechanicalCheckContext context, ISet<int> confirmedTocTabs = null)
        {
            var result = new Dictionary<int, List<string>>();
            // Text-only checker callers may have no physical DOCX; they still receive text offsets.
            if (!context.Document.Elements.Any(item => item.Text.Contains('\t')) || !File.Exists(context.FilePath))
                return result;

            using (var stream = new FileStream(context.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (WordprocessingDocument document = WordprocessingDocument.Open(stream, false))
            {
                if (document.MainDocumentPart?.Document?.Body == null) return result;
                var elements = context.Document.Elements.ToDictionary(item => LocationKey(item.Location), item => item);
                foreach (Paragraph paragraph in document.MainDocumentPart.Document.Body.Descendants<Paragraph>())
                {
                    if (!paragraph.Descendants<TabChar>().Any() &&
                        !paragraph.Descendants<Text>().Any(item => item.Text.Contains('\t'))) continue;
                    DocumentLocation location = LocateParagraph(context, paragraph);
                    if (!elements.TryGetValue(LocationKey(location), out DocumentElement element) ||
                        !element.Text.Contains('\t')) continue;

                    if (IsConfirmedTocParagraph(paragraph, document.MainDocumentPart))
                    {
                        confirmedTocTabs?.Add(element.DocumentOrderIndex);
                        continue;
                    }

                    List<Run> runs = paragraph.Descendants<Run>().ToList();
                    var runOffsets = new Dictionary<Run, int>();
                    int paragraphOffset = 0;
                    foreach (OpenXmlElement node in paragraph.Descendants())
                    {
                        // Mirror the existing Document layer's visible character representation,
                        // without admitting w:delText as visible text or modifying that layer.
                        string text = node is Text value ? value.Text : node is TabChar ? "\t" :
                            node is Break || node is CarriageReturn ? "\n" : string.Empty;
                        Run run = node.Ancestors<Run>().FirstOrDefault();
                        int runOffset = run != null && runOffsets.TryGetValue(run, out int offset) ? offset : 0;
                        for (int index = 0; index < text.Length; index++)
                        {
                            if (text[index] != '\t') continue;
                            if (!result.TryGetValue(element.DocumentOrderIndex, out List<string> evidence))
                                result[element.DocumentOrderIndex] = evidence = new List<string>();
                            evidence.Add("原始字符=\\t；Unicode=U+0009；段内字符偏移=" + (paragraphOffset + index) +
                                "；RunIndex=" + (run == null ? "未知" : runs.IndexOf(run).ToString()) +
                                "；RunOffset=" + (runOffset + index) + "；XML 节点=" + Path(node) +
                                "；节点内偏移=" + index + "（偏移及 RunIndex 均从 0 开始）");
                        }

                        paragraphOffset += text.Length;
                        if (run != null) runOffsets[run] = runOffset + text.Length;
                    }
                }
            }

            return result;
        }

        private static bool IsConfirmedTocParagraph(Paragraph paragraph, MainDocumentPart mainPart)
        {
            string styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (!string.IsNullOrEmpty(styleId))
            {
                if (Regex.IsMatch(styleId, @"^TOC[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return true;

                Style style = mainPart.StyleDefinitionsPart?.Styles?.Elements<Style>()
                    .FirstOrDefault(item => string.Equals(item.StyleId, styleId, StringComparison.Ordinal));
                string styleName = style?.StyleName?.Val?.Value;
                if (!string.IsNullOrEmpty(styleName) && Regex.IsMatch(styleName,
                    @"^(?:toc|目录)\s*[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    return true;
            }

            if (paragraph.Descendants<FieldCode>().Any(item => Regex.IsMatch(item.Text ?? string.Empty,
                @"^\s*TOC(?=\s|\\|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ||
                paragraph.Descendants<SimpleField>().Any(item => Regex.IsMatch(item.Instruction?.Value ?? string.Empty,
                    @"^\s*TOC(?=\s|\\|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                return true;

            // A TOC link alone is not proof: pair it with a right-aligned leader tab.
            bool hasTocLink = paragraph.Descendants<Hyperlink>().Any(link =>
                link.Anchor?.Value?.StartsWith("_Toc", StringComparison.OrdinalIgnoreCase) == true);
            return hasTocLink && paragraph.ParagraphProperties?.Tabs?.Elements<TabStop>().Any(stop =>
                stop.Val != null && stop.Val.Value.ToString() == "Right" && stop.Leader != null &&
                stop.Leader.Value.ToString() != "None") == true;
        }

        private static DocumentLocation ExistingLocation(
            MechanicalCheckContext context, int? paragraph, int? table, int? row, int? cell)
        {
            return context.Document.Elements.FirstOrDefault(item => item.Location.DocumentPart == DocumentPart.MainBody &&
                item.Location.ParagraphIndex == paragraph && item.Location.TableIndex == table &&
                item.Location.RowIndex == row && item.Location.CellIndex == cell)?.Location ??
                new DocumentLocation(DocumentPart.MainBody, paragraph, table, row, cell, new string[0]);
        }

        private static string LocationKey(DocumentLocation location) =>
            location.DocumentPart + "|" + location.ParagraphIndex + "|" + location.TableIndex + "|" +
            location.RowIndex + "|" + location.CellIndex;
    }
}
