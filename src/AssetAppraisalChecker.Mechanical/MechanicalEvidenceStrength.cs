using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical
{
    // Evidence strength is presentation admission, not severity, authority or business adjudication.
    public static class MechanicalEvidenceStrength
    {
        public const string HardDeterministic = "质控依据=HardDeterministic";
        public const string ContextDependent = "质控依据=ContextDependent";

        internal static ISet<int> CompleteVisibleRanges(MechanicalCheckContext context)
        {
            var result = new HashSet<int>();
            if (!File.Exists(context.FilePath)) return result;
            using (var stream = new FileStream(context.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var doc = WordprocessingDocument.Open(stream, false))
            {
                Body body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return result;
                var paragraphs = body.Elements<Paragraph>().ToList();
                var tables = body.Elements<Table>().ToList();
                foreach (DocumentElement element in context.Document.Elements)
                {
                    var location = element.Location;
                    if (location.DocumentPart != DocumentPart.MainBody) continue;
                    Paragraph paragraph = null;
                    if (location.ParagraphIndex.HasValue && location.ParagraphIndex < paragraphs.Count)
                        paragraph = paragraphs[location.ParagraphIndex.Value];
                    else if (location.TableIndex.HasValue && location.TableIndex < tables.Count &&
                        location.RowIndex.HasValue && location.CellIndex.HasValue)
                    {
                        TableRow row = tables[location.TableIndex.Value].Elements<TableRow>().ElementAtOrDefault(location.RowIndex.Value);
                        TableCell cell = row?.Elements<TableCell>().ElementAtOrDefault(location.CellIndex.Value);
                        if (cell == null || cell.Descendants<Table>().Any() ||
                            cell.TableCellProperties?.GridSpan != null || cell.TableCellProperties?.VerticalMerge != null) continue;
                        var values = cell.Elements<Paragraph>().ToList();
                        if (values.Count == 1) paragraph = values[0];
                    }
                    if (paragraph == null || paragraph.Descendants().Any(node =>
                        node.LocalName == "fldChar" || node.LocalName == "fldSimple" || node.LocalName == "instrText" ||
                        node.LocalName == "del" || node.LocalName == "ins" || node.LocalName == "txbxContent")) continue;
                    string visible = string.Concat(paragraph.Descendants().Select(node =>
                        node is Text text ? text.Text : node is TabChar ? "\t" :
                        node is Break || node is CarriageReturn ? "\n" : string.Empty));
                    // A label, heading or partial citation is not a closed linguistic range.
                    if (string.Equals(visible, element.Text, StringComparison.Ordinal) &&
                        element.ElementType != DocumentElementType.Heading && visible.Length > 0 &&
                        "。！？".IndexOf(visible.TrimEnd().LastOrDefault()) >= 0)
                        result.Add(element.DocumentOrderIndex);
                }
            }
            return result;
        }
    }
}
