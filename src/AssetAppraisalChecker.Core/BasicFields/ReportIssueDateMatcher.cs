using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    internal enum ReportIssueDateExtractionPath
    {
        ExplicitLabel = 0,
        CoverUniqueDate = 1
    }

    internal sealed class ReportIssueDateMatch
    {
        public ReportIssueDateMatch(
            DocumentElement element,
            string rawDate,
            int startIndex,
            int length,
            ReportIssueDateExtractionPath extractionPath,
            string label)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            RawDate = rawDate ?? string.Empty;
            StartIndex = startIndex;
            Length = length;
            ExtractionPath = extractionPath;
            Label = label ?? string.Empty;
        }

        public DocumentElement Element { get; }

        public string RawDate { get; }

        public int StartIndex { get; }

        public int Length { get; }

        public ReportIssueDateExtractionPath ExtractionPath { get; }

        public string Label { get; }
    }

    internal sealed class ReportIssueDateMatcher
    {
        private static readonly Regex LabelPattern = new Regex(
            @"(?<label>评估报告日期|评估报告日|报告日期)(?<bridge>\s*(?::|：|为)?\s*)(?<date>" +
            @"(?:\d{4}年\d{1,2}月\d{1,2}日|" +
            @"[〇○零一二三四五六七八九]{4}年[〇○零一二三四五六七八九十]{1,3}月[〇○零一二三四五六七八九十]{1,3}日))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SupportedDatePattern = new Regex(
            @"(?:\d{4}年\d{1,2}月\d{1,2}日|" +
            @"[〇○零一二三四五六七八九]{4}年[〇○零一二三四五六七八九十]{1,3}月[〇○零一二三四五六七八九十]{1,3}日)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly string[] ForbiddenCoverSemantics =
        {
            "评估基准日期",
            "评估基准日",
            "有效期限",
            "有效期",
            "起至",
            "截至",
            "截止",
            "期间"
        };

        public IReadOnlyList<ReportIssueDateMatch> FindMatches(
            IEnumerable<DocumentElement> documentElements)
        {
            if (documentElements == null)
            {
                throw new ArgumentNullException(nameof(documentElements));
            }

            List<DocumentElement> elements = documentElements
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();
            List<ReportIssueDateMatch> labelled = FindExplicitLabelMatches(elements);
            if (labelled.Count > 0)
            {
                return new ReadOnlyCollection<ReportIssueDateMatch>(labelled);
            }

            ReportIssueDateMatch coverMatch = FindUniqueCoverDate(elements);
            if (coverMatch == null)
            {
                return new ReadOnlyCollection<ReportIssueDateMatch>(
                    new ReportIssueDateMatch[0]);
            }

            return new ReadOnlyCollection<ReportIssueDateMatch>(
                new[] { coverMatch });
        }

        private static List<ReportIssueDateMatch> FindExplicitLabelMatches(
            IEnumerable<DocumentElement> elements)
        {
            var results = new List<ReportIssueDateMatch>();
            foreach (DocumentElement element in elements)
            {
                string text = element.Text ?? string.Empty;
                foreach (Match match in LabelPattern.Matches(text))
                {
                    Group bridge = match.Groups["bridge"];
                    Group date = match.Groups["date"];
                    if (bridge.Value.Length > 4 ||
                        !HasSafeDateEnd(text, date.Index + date.Length) ||
                        HasAnotherDateInSameClause(text, date.Index + date.Length) ||
                        !BasicFieldNormalizer.Normalize(
                            BasicFieldType.ReportIssueDate,
                            date.Value).IsSuccess)
                    {
                        continue;
                    }

                    results.Add(new ReportIssueDateMatch(
                        element,
                        date.Value,
                        date.Index,
                        date.Length,
                        ReportIssueDateExtractionPath.ExplicitLabel,
                        match.Groups["label"].Value));
                }
            }

            return results;
        }

        private static ReportIssueDateMatch FindUniqueCoverDate(
            IReadOnlyCollection<DocumentElement> elements)
        {
            if (!elements.Any(item =>
                item.BoundaryMetadata.DocumentHasReliableFirstPageBoundary))
            {
                return null;
            }

            List<DocumentElement> coverElements = elements
                .Where(item =>
                    item.BoundaryMetadata.IsBeforeFirstPageBoundary &&
                    item.Location.DocumentPart == DocumentPart.MainBody)
                .ToList();
            if (coverElements.Count == 0 ||
                coverElements.Any(item => ContainsForbiddenCoverSemantic(item.Text)))
            {
                return null;
            }

            var candidates = new List<ReportIssueDateMatch>();
            foreach (DocumentElement element in coverElements)
            {
                string text = element.Text ?? string.Empty;
                foreach (Match date in SupportedDatePattern.Matches(text))
                {

                    candidates.Add(new ReportIssueDateMatch(
                        element,
                        date.Value,
                        date.Index,
                        date.Length,
                        ReportIssueDateExtractionPath.CoverUniqueDate,
                        string.Empty));
                }
            }

            if (candidates.Count != 1)
            {
                return null;
            }

            ReportIssueDateMatch candidate = candidates[0];
            string candidateText = candidate.Element.Text ?? string.Empty;
            if ((candidate.Element.ElementType != DocumentElementType.Paragraph &&
                 candidate.Element.ElementType != DocumentElementType.Heading) ||
                !string.Equals(
                    BasicFieldNormalizer.NormalizeWhitespace(candidateText),
                    candidate.RawDate,
                    StringComparison.Ordinal) ||
                !BasicFieldNormalizer.Normalize(
                    BasicFieldType.ReportIssueDate,
                    candidate.RawDate).IsSuccess)
            {
                return null;
            }

            return candidate;
        }

        private static bool ContainsForbiddenCoverSemantic(string text)
        {
            string value = text ?? string.Empty;
            return ForbiddenCoverSemantics.Any(token =>
                value.IndexOf(token, StringComparison.Ordinal) >= 0);
        }

        private static bool HasSafeDateEnd(string text, int dateEnd)
        {
            if (dateEnd >= text.Length)
            {
                return true;
            }

            char next = text[dateEnd];
            return char.IsWhiteSpace(next) ||
                   next == '。' ||
                   next == '；' ||
                   next == ';' ||
                   next == '，' ||
                   next == ',' ||
                   next == '、' ||
                   next == ')' ||
                   next == '）';
        }

        private static bool HasAnotherDateInSameClause(string text, int startIndex)
        {
            int clauseEnd = text.IndexOfAny(
                new[] { '。', '；', ';', '\r', '\n' },
                startIndex);
            if (clauseEnd < 0)
            {
                clauseEnd = text.Length;
            }

            return SupportedDatePattern.IsMatch(
                text.Substring(startIndex, clauseEnd - startIndex));
        }
    }
}
