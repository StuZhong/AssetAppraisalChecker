using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    /// <summary>Only known Word structural terminators are removed; never Trim user text.</summary>
    public static class WordTextMatcher
    {
        public static string ContainerText(string wordText, bool cell)
        {
            string text = wordText ?? string.Empty;
            if (cell && text.EndsWith("\r\a", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 2);
            else if (!cell && text.EndsWith("\r", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 1);
            return text.Replace('\v', '\n').Replace('\r', '\n');
        }

        public static string ParagraphText(string wordText) =>
            ContainerText(wordText, (wordText ?? string.Empty).EndsWith("\r\a", StringComparison.Ordinal));

        // WPS keeps the cell terminator in Range.Text but omits only U+0007 from
        // Selection.Text after selecting that same Start/End/StoryType range.
        public static bool MatchesSelectedRangeText(string rangeText, string selectedText, bool tableRange) =>
            string.Equals(rangeText, selectedText, StringComparison.Ordinal) ||
            tableRange && rangeText != null && rangeText.EndsWith("\r\a", StringComparison.Ordinal) &&
            string.Equals(rangeText.Substring(0, rangeText.Length - 1), selectedText, StringComparison.Ordinal);

        // WPS omits every structural U+0007 cell marker from Selection.Text for
        // a row selection, while retaining all CRs and the same Start/End.
        // Call only after the row's complete cell sequence was verified.
        public static bool MatchesSelectedRowText(string rangeText, string selectedText) =>
            rangeText != null && rangeText.EndsWith("\r\a", StringComparison.Ordinal) &&
            string.Equals(rangeText.Replace("\a", string.Empty), selectedText, StringComparison.Ordinal);

        public static bool MatchesRowRange(IReadOnlyList<string> expectedCells, string rangeText)
        {
            if (expectedCells == null || rangeText == null || !rangeText.EndsWith("\r\a", StringComparison.Ordinal))
                return false;
            string[] cells = rangeText.Split('\a');
            // WPS appends one extra CR+cell marker to Row.Range after the actual
            // cells; Word may expose only the cells. Accept only that exact marker.
            bool wpsRowTerminator = cells.Length == expectedCells.Count + 2 &&
                cells[cells.Length - 2] == "\r";
            if (!(cells.Length == expectedCells.Count + 1 || wpsRowTerminator) ||
                cells[cells.Length - 1].Length != 0) return false;
            for (int i = 0; i < expectedCells.Count; i++)
                if (!string.Equals(ContainerText(cells[i] + "\a", true), expectedCells[i], StringComparison.Ordinal))
                    return false;
            return true;
        }

        public static bool MatchesSequence(IReadOnlyList<string> source, IReadOnlyList<string> actual) =>
            source != null && actual != null && source.Count == actual.Count &&
            source.Zip(actual, (a, b) => string.Equals(a, b, StringComparison.Ordinal)).All(equal => equal);

        /// <summary>
        /// Verify the supplied structural index, never search for another occurrence. WPS can
        /// expose section/page markers and drawing placeholders in unrelated paragraphs.
        /// A fallback requires equal paragraph counts, a unique complete target in BOTH
        /// sequences and exact immediate context. Changed/ambiguous local mappings are refused.
        /// </summary>
        public static bool MatchesParagraphAt(IReadOnlyList<string> source, IReadOnlyList<string> actual, int index)
        {
            if (source == null || actual == null || source.Count != actual.Count || index < 0 || index >= source.Count)
                return false;
            if (MatchesSequence(source, actual)) return true;
            string target = source[index];
            if (string.IsNullOrEmpty(target) || !string.Equals(target, actual[index], StringComparison.Ordinal) ||
                source.Count(t => string.Equals(t, target, StringComparison.Ordinal)) != 1 ||
                actual.Count(t => string.Equals(t, target, StringComparison.Ordinal)) != 1) return false;
            return (index == 0 || string.Equals(source[index - 1], actual[index - 1], StringComparison.Ordinal)) &&
                (index == source.Count - 1 || string.Equals(source[index + 1], actual[index + 1], StringComparison.Ordinal));
        }

        // Field instructions occupy editor character positions but may not appear in Range.Text.
        // Matching visible text confirms only the paragraph; it NEVER enables a field span.
        // Deleted content absent from the visible text still requires the XML verification path.
        public static bool MatchesVisibleParagraph(SourceNavigationTarget target, string actualText) =>
            target != null && !string.IsNullOrEmpty(target.Occurrence.RawText) &&
            target.ContainerText.Contains(target.Occurrence.RawText) &&
            string.Equals(target.ContainerText, actualText, StringComparison.Ordinal);

        // WPS includes a terminal U+000C for this paragraph's section break. Accept ONLY
        // that one extra marker, backed by both the checked source and this editor range's XML.
        // The caller must still verify structural index, uniqueness and immediate neighbours.
        // Do not normalize general controls/spaces or use this to claim exact character offsets.
        public static bool MatchesSectionBreakParagraph(SourceNavigationTarget target, string actualText, string rangeXml)
        {
            if (target == null || target.DisplaySensitive || target.Location.TableIndex.HasValue ||
                string.IsNullOrEmpty(target.ContainerText) ||
                !string.Equals(actualText, target.ContainerText + "\f", StringComparison.Ordinal) ||
                !target.Source.Document.Elements.Any(e => e.Location.ParagraphIndex == target.Location.ParagraphIndex &&
                    !e.Location.TableIndex.HasValue && e.BoundaryMetadata.HasPageCreatingSectionBreak)) return false;
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XElement body = XDocument.Parse(rangeXml).Descendants(w + "body").SingleOrDefault();
            var paragraphs = body?.Descendants(w + "p").ToList();
            if (paragraphs == null || paragraphs.Count != 1 || paragraphs[0].Parent != body ||
                paragraphs[0].Element(w + "pPr")?.Element(w + "sectPr") == null) return false;
            string visible = string.Concat(paragraphs[0].Descendants().Select(e =>
                e.Name == w + "t" ? e.Value : e.Name == w + "tab" ? "\t" :
                e.Name == w + "br" || e.Name == w + "cr" ? "\n" : string.Empty));
            return string.Equals(visible, target.ContainerText, StringComparison.Ordinal);
        }

        public static bool MatchesSpan(SourceNavigationTarget target, string actualText) =>
            target != null && !target.DisplaySensitive && target.Offset.HasValue &&
            string.Equals(actualText?.Replace('\v', '\n').Replace('\r', '\n'), target.SpanText, StringComparison.Ordinal);

        public static bool MatchesPrefix(SourceNavigationTarget target, string actualPrefix) =>
            target != null && target.Offset.HasValue && string.Equals(
                actualPrefix?.Replace('\v', '\n').Replace('\r', '\n'),
                target.ContainerText.Substring(0, target.Offset.Value), StringComparison.Ordinal);

        public static bool SameFullPath(string a, string b) =>
            string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
