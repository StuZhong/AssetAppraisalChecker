using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldExtractor
    {
        private static readonly BasicFieldLabel[] Labels =
        {
            new BasicFieldLabel(BasicFieldType.ProjectName, "资产评估项目名称"),
            new BasicFieldLabel(BasicFieldType.ProjectName, "评估项目名称"),
            new BasicFieldLabel(BasicFieldType.ProjectName, "项目名称"),
            new BasicFieldLabel(BasicFieldType.ReportNumber, "资产评估报告编号"),
            new BasicFieldLabel(BasicFieldType.ReportNumber, "评估报告编号"),
            new BasicFieldLabel(BasicFieldType.ReportNumber, "报告编号"),
            new BasicFieldLabel(BasicFieldType.ValuationDate, "评估基准日期"),
            new BasicFieldLabel(BasicFieldType.ValuationDate, "评估基准日")
        };

        private static readonly Dictionary<string, BasicFieldLabel> LabelByText = Labels
            .ToDictionary(item => item.Text, item => item, StringComparer.Ordinal);

        private static readonly Regex InlineLabelPattern = new Regex(
            "(?<label>" + string.Join(
                "|",
                Labels
                    .OrderByDescending(item => item.Text.Length)
                    .Select(item => Regex.Escape(item.Text))) +
            @")(?<separator>\s*(?::|：|为)\s*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<BasicFieldOccurrence> NoOccurrences =
            new ReadOnlyCollection<BasicFieldOccurrence>(new BasicFieldOccurrence[0]);

        private static readonly string[] ValuationDateRangeOrPeriodTokens =
        {
            "起至",
            "截止",
            "截至",
            "期间",
            "至"
        };

        public IReadOnlyList<BasicFieldOccurrence> Extract(DocumentReadResult documentReadResult)
        {
            if (documentReadResult == null)
            {
                throw new ArgumentNullException(nameof(documentReadResult));
            }

            if (!documentReadResult.IsSuccess)
            {
                return NoOccurrences;
            }

            return Extract(documentReadResult.Elements);
        }

        public IReadOnlyList<BasicFieldOccurrence> Extract(IEnumerable<DocumentElement> documentElements)
        {
            if (documentElements == null)
            {
                throw new ArgumentNullException(nameof(documentElements));
            }

            List<DocumentElement> elements = documentElements
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();
            var extracted = new List<OrderedOccurrence>();
            IReadOnlyList<ReportLevelProjectNameCandidate> reportLevelProjectNames =
                new ReportLevelProjectNameBlockMatcher().FindMatches(elements);

            ExtractTableLabelValuePairs(elements, extracted);
            foreach (DocumentElement element in elements)
            {
                ExtractInlineLabels(element, extracted);
                ExtractSupplementalValuationDates(element, extracted);
                ExtractStandaloneReportNumber(element, extracted);
            }
            ExtractReportIssueDates(elements, extracted);

            ExtractAuthoritativeStandaloneProjectNames(
                elements,
                reportLevelProjectNames,
                extracted);
            ExtractVerticalTableProjectNames(elements, extracted);
            ExtractAuthoritativeAdjacentAlphanumericReportNumbers(elements, extracted);

            List<BasicFieldOccurrence> ordered = extracted
                .OrderBy(item => item.DocumentOrderIndex)
                .ThenBy(item => item.Occurrence.TextStartIndex)
                .ThenBy(item => (int)item.Occurrence.FieldType)
                .Select(item => item.Occurrence)
                .ToList();
            return new ReadOnlyCollection<BasicFieldOccurrence>(ordered);
        }

        private static void ExtractInlineLabels(
            DocumentElement element,
            ICollection<OrderedOccurrence> occurrences)
        {
            string text = element.Text ?? string.Empty;
            MatchCollection matches = InlineLabelPattern.Matches(text);
            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                BasicFieldLabel label = LabelByText[match.Groups["label"].Value];
                if (label.FieldType == BasicFieldType.ValuationDate &&
                    ContainsLineBreak(match.Groups["separator"].Value))
                {
                    continue;
                }

                int valueStart = match.Index + match.Length;
                int valueEnd = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
                string rawValue = TrimExtractedValue(text.Substring(valueStart, valueEnd - valueStart));
                if (rawValue.Length == 0)
                {
                    continue;
                }

                string rawText = match.Value + rawValue;
                int occurrenceStart = match.Index;
                int occurrenceLength = rawText.Length;
                IEnumerable<string> evidence = new[]
                {
                    string.Format("当前文本以明确标签“{0}”标识该值。", label.Text)
                };
                if (label.FieldType == BasicFieldType.ValuationDate)
                {
                    ValuationDateMatch preciseMatch;
                    if (TryPreferPreciseValuationDate(
                        element,
                        valueStart,
                        valueEnd,
                        rawValue,
                        out preciseMatch))
                    {
                        rawValue = preciseMatch.RawDate;
                        rawText = preciseMatch.RawDate;
                        occurrenceStart = preciseMatch.StartIndex;
                        occurrenceLength = preciseMatch.Length;
                        evidence = new[]
                        {
                            string.Format("当前文本以明确标签“{0}”标识该值。", label.Text),
                            "同一普通段落的宽候选无法标准化；唯一且可验证的精确日期候选优先，尾随叙述未计入字段值。"
                        };
                    }
                    else
                    {
                        occurrenceStart = text.IndexOf(
                            rawValue,
                            valueStart,
                            valueEnd - valueStart,
                            StringComparison.Ordinal);
                        occurrenceLength = rawValue.Length;
                        rawText = rawValue;
                    }
                }

                AddOccurrence(
                    occurrences,
                    element,
                    label.FieldType,
                    rawText,
                    rawValue,
                    ClassificationConfidence.High,
                    evidence,
                    occurrenceStart,
                    occurrenceLength);
            }
        }

        private static bool TryPreferPreciseValuationDate(
            DocumentElement element,
            int valueStart,
            int valueEnd,
            string wideRawValue,
            out ValuationDateMatch preciseMatch)
        {
            preciseMatch = null;
            if (element.ElementType != DocumentElementType.Paragraph ||
                BasicFieldNormalizer.Normalize(
                    BasicFieldType.ValuationDate,
                    wideRawValue).IsSuccess ||
                !ValuationDateMatcher.HasExactlyOneSupportedDateToken(
                    element.Text,
                    valueStart,
                    valueEnd - valueStart))
            {
                return false;
            }

            List<ValuationDateMatch> matches = ValuationDateMatcher
                .FindMatches(element.Text)
                .Where(item =>
                    item.StartIndex >= valueStart &&
                    item.StartIndex + item.Length <= valueEnd)
                .ToList();
            if (matches.Count != 1)
            {
                return false;
            }

            ValuationDateMatch candidate = matches[0];
            int candidateEnd = candidate.StartIndex + candidate.Length;
            if (candidateEnd >= valueEnd)
            {
                return false;
            }

            string trailingText = element.Text.Substring(
                candidateEnd,
                valueEnd - candidateEnd);
            if (string.IsNullOrWhiteSpace(trailingText) ||
                ValuationDateRangeOrPeriodTokens.Any(token =>
                    trailingText.IndexOf(token, StringComparison.Ordinal) >= 0))
            {
                return false;
            }

            preciseMatch = candidate;
            return true;
        }

        private static void ExtractSupplementalValuationDates(
            DocumentElement element,
            ICollection<OrderedOccurrence> occurrences)
        {
            if (element.ElementType != DocumentElementType.Paragraph)
            {
                return;
            }

            foreach (ValuationDateMatch match in ValuationDateMatcher.FindMatches(element.Text))
            {
                if (OverlapsExisting(
                    occurrences,
                    element,
                    match.StartIndex,
                    match.Length,
                    BasicFieldType.ValuationDate))
                {
                    continue;
                }

                AddOccurrence(
                    occurrences,
                    element,
                    BasicFieldType.ValuationDate,
                    match.RawDate,
                    match.RawDate,
                    ClassificationConfidence.High,
                    new[]
                    {
                        string.Format(
                            "当前普通段落中的明确标签“{0}”通过{1}绑定唯一且可解析的日期值。",
                            match.Label,
                            match.BoundaryEvidence)
                    },
                    match.StartIndex,
                    match.Length);
            }
        }

        private static bool ContainsLineBreak(string text)
        {
            return (text ?? string.Empty).IndexOfAny(new[] { '\r', '\n' }) >= 0;
        }

        private static void ExtractReportIssueDates(
            IEnumerable<DocumentElement> elements,
            ICollection<OrderedOccurrence> occurrences)
        {
            foreach (ReportIssueDateMatch match in
                new ReportIssueDateMatcher().FindMatches(elements))
            {
                string extractionPath =
                    match.ExtractionPath == ReportIssueDateExtractionPath.ExplicitLabel
                        ? "ExplicitLabel"
                        : "CoverUniqueDate";
                string evidence =
                    match.ExtractionPath == ReportIssueDateExtractionPath.ExplicitLabel
                        ? string.Format(
                            "ReportIssueDateMatcher/{0}：明确标签“{1}”绑定唯一且可验证的日期。",
                            extractionPath,
                            match.Label)
                        : "ReportIssueDateMatcher/CoverUniqueDate：首页可靠边界前仅有一个独立日期候选，且无评估基准日或有效期语义。";

                AddOccurrence(
                    occurrences,
                    match.Element,
                    BasicFieldType.ReportIssueDate,
                    match.RawDate,
                    match.RawDate,
                    ClassificationConfidence.High,
                    new[] { evidence },
                    match.StartIndex,
                    match.Length);
            }
        }

        private static void ExtractStandaloneReportNumber(
            DocumentElement element,
            ICollection<OrderedOccurrence> occurrences)
        {
            if (element.ElementType != DocumentElementType.Paragraph)
            {
                return;
            }

            int startIndex;
            int length;
            string text = element.Text ?? string.Empty;
            if (!StandaloneReportNumberMatcher.TryMatch(text, out startIndex, out length) ||
                OverlapsExisting(
                    occurrences,
                    element,
                    startIndex,
                    length,
                    BasicFieldType.ReportNumber))
            {
                return;
            }

            string reportNumber = text.Substring(startIndex, length);
            AddOccurrence(
                occurrences,
                element,
                BasicFieldType.ReportNumber,
                reportNumber,
                reportNumber,
                ClassificationConfidence.High,
                new[]
                {
                    "当前普通段落在去除首尾空白后整体符合独立资产评估报告编号的确定性结构。"
                },
                startIndex,
                length);
        }

        private static void ExtractTableLabelValuePairs(
            IEnumerable<DocumentElement> elements,
            ICollection<OrderedOccurrence> occurrences)
        {
            IEnumerable<IGrouping<string, DocumentElement>> rows = elements
                .Where(item => item.ElementType == DocumentElementType.TableCell &&
                               item.Location.TableIndex.HasValue &&
                               item.Location.RowIndex.HasValue)
                .GroupBy(item => string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}:{1}",
                    item.Location.TableIndex.Value,
                    item.Location.RowIndex.Value));

            foreach (IGrouping<string, DocumentElement> row in rows)
            {
                List<DocumentElement> cells = row
                    .OrderBy(item => item.Location.CellIndex ?? int.MaxValue)
                    .ToList();

                for (int index = 0; index + 1 < cells.Count; index++)
                {
                    BasicFieldLabel label;
                    if (!TryGetExactLabel(cells[index].Text, out label))
                    {
                        continue;
                    }

                    DocumentElement valueCell = cells[index + 1];
                    BasicFieldLabel nextLabel;
                    if (TryGetExactLabel(valueCell.Text, out nextLabel) ||
                        (label.FieldType == BasicFieldType.ProjectName &&
                         IsBusinessValueHeader(valueCell.Text)))
                    {
                        continue;
                    }

                    string rawValue = TrimExtractedValue(valueCell.Text);
                    AddOccurrence(
                        occurrences,
                        valueCell,
                        label.FieldType,
                        valueCell.Text,
                        rawValue,
                        ClassificationConfidence.High,
                        new[]
                        {
                            string.Format(
                                "同一表格行中，标签单元格“{0}”右侧相邻单元格提供该值。",
                                label.Text)
                        },
                        0,
                        valueCell.Text == null ? 0 : valueCell.Text.Length);
                    index++;
                }
            }
        }

        private static void ExtractVerticalTableProjectNames(
            IEnumerable<DocumentElement> elements,
            ICollection<OrderedOccurrence> occurrences)
        {
            foreach (ProjectNameTableMatch match in ProjectNameTableMatcher.FindMatches(elements))
            {
                DocumentElement valueCell = match.ValueCell;
                string rawValue = TrimExtractedValue(valueCell.Text);
                int textLength = valueCell.Text == null ? 0 : valueCell.Text.Length;
                if (rawValue.Length == 0 ||
                    OverlapsExisting(
                        occurrences,
                        valueCell,
                        0,
                        textLength,
                        BasicFieldType.ProjectName))
                {
                    continue;
                }

                AddOccurrence(
                    occurrences,
                    valueCell,
                    BasicFieldType.ProjectName,
                    valueCell.Text,
                    rawValue,
                    ClassificationConfidence.High,
                    new[]
                    {
                        string.Format(
                            "同一表格“{0}”候选列在非聚合数据区域仅有一个有效项目值，且“{1}”业务金额列在该行有对应数据。",
                            BasicFieldNormalizer.NormalizeWhitespace(match.HeaderCell.Text),
                            match.BusinessKeyword)
                    },
                    0,
                    textLength);
            }
        }

        private static void ExtractAuthoritativeStandaloneProjectNames(
            IEnumerable<DocumentElement> elements,
            IEnumerable<ReportLevelProjectNameCandidate> reportLevelProjectNames,
            ICollection<OrderedOccurrence> occurrences)
        {
            var reportLevelParagraphOrders = new HashSet<int>(
                (reportLevelProjectNames ?? Enumerable.Empty<ReportLevelProjectNameCandidate>())
                    .SelectMany(item => item.SourceParagraphs)
                    .Select(item => item.DocumentOrderIndex));
            foreach (AuthoritativeStandaloneProjectNameMatch match in
                AuthoritativeStandaloneProjectNameMatcher.FindMatches(elements))
            {
                DocumentElement valueElement = match.ValueElement;
                if (reportLevelParagraphOrders.Contains(valueElement.DocumentOrderIndex) ||
                    OverlapsExisting(
                    occurrences,
                    valueElement,
                    match.StartIndex,
                    match.Length,
                    BasicFieldType.ProjectName))
                {
                    continue;
                }

                string rawValue = valueElement.Text.Substring(
                    match.StartIndex,
                    match.Length);
                AddOccurrence(
                    occurrences,
                    valueElement,
                    BasicFieldType.ProjectName,
                    rawValue,
                    rawValue,
                    ClassificationConfidence.High,
                    new[]
                    {
                        BuildAuthoritativeProjectNameEvidence(match)
                    },
                    match.StartIndex,
                    match.Length);
            }
        }

        private static void ExtractAuthoritativeAdjacentAlphanumericReportNumbers(
            IEnumerable<DocumentElement> elements,
            ICollection<OrderedOccurrence> occurrences)
        {
            foreach (AuthoritativeAdjacentAlphanumericReportNumberMatch match in
                AuthoritativeAdjacentAlphanumericReportNumberMatcher.FindMatches(elements))
            {
                DocumentElement valueElement = match.ValueElement;
                if (OverlapsExisting(
                    occurrences,
                    valueElement,
                    match.StartIndex,
                    match.Length,
                    BasicFieldType.ReportNumber))
                {
                    continue;
                }

                string rawValue = valueElement.Text.Substring(
                    match.StartIndex,
                    match.Length);
                AddOccurrence(
                    occurrences,
                    valueElement,
                    BasicFieldType.ReportNumber,
                    rawValue,
                    rawValue,
                    ClassificationConfidence.High,
                    new[]
                    {
                        string.Format(
                            "独立报告编号使用受限字母数字序列，且立即位于{0}之后。",
                            match.AuthorityKind)
                    },
                    match.StartIndex,
                    match.Length);
            }
        }

        private static string BuildAuthoritativeProjectNameEvidence(
            AuthoritativeStandaloneProjectNameMatch match)
        {
            if (match.MatchKind ==
                AuthoritativeProjectNameMatchKind.AuthoritativeAdjacentStandalone)
            {
                return string.Format(
                    "独立完整项目名称段落紧邻下一正文元素中的{0}，通过受限权威相邻结构确定。",
                    match.AuthorityKind);
            }

            return string.Format(
                "独立完整项目名称以固定后缀“评估项目”结尾，且紧邻下一正文段落为{0}。",
                match.AuthorityKind);
        }

        private static bool TryGetExactLabel(string text, out BasicFieldLabel label)
        {
            string normalized = BasicFieldNormalizer.NormalizeWhitespace(text)
                .TrimEnd(':', '：')
                .TrimEnd();
            return LabelByText.TryGetValue(normalized, out label);
        }

        private static bool IsBusinessValueHeader(string text)
        {
            return ProjectNameTableMatcher.IsBusinessValueHeaderCandidate(text);
        }

        private static string TrimExtractedValue(string value)
        {
            string normalized = (value ?? string.Empty)
                .Trim()
                .TrimStart('，', ',', '；', ';', '。')
                .TrimEnd('，', ',', '；', ';', '。');
            int terminator = normalized.IndexOfAny(new[] { '。', '；', ';' });
            return terminator >= 0
                ? normalized.Substring(0, terminator).TrimEnd('，', ',')
                : normalized;
        }

        private static void AddOccurrence(
            ICollection<OrderedOccurrence> occurrences,
            DocumentElement element,
            BasicFieldType fieldType,
            string rawText,
            string rawValue,
            ClassificationConfidence confidence,
            IEnumerable<string> evidence,
            int textStartIndex,
            int textLength)
        {
            BasicFieldNormalizationResult normalization = BasicFieldNormalizer.Normalize(
                fieldType,
                rawValue);
            var occurrence = new BasicFieldOccurrence(
                fieldType,
                rawText,
                rawValue,
                normalization,
                element.Location,
                element.NearbyContext,
                confidence,
                evidence,
                textStartIndex,
                textLength);
            occurrences.Add(new OrderedOccurrence(element.DocumentOrderIndex, occurrence));
        }

        private static bool OverlapsExisting(
            IEnumerable<OrderedOccurrence> occurrences,
            DocumentElement element,
            int startIndex,
            int length,
            BasicFieldType fieldType)
        {
            int endIndex = startIndex + length;
            return occurrences.Any(item =>
                item.DocumentOrderIndex == element.DocumentOrderIndex &&
                item.Occurrence.FieldType == fieldType &&
                item.Occurrence.TextStartIndex < endIndex &&
                startIndex < item.Occurrence.TextStartIndex + item.Occurrence.TextLength);
        }

        private sealed class BasicFieldLabel
        {
            public BasicFieldLabel(BasicFieldType fieldType, string text)
            {
                FieldType = fieldType;
                Text = text;
            }

            public BasicFieldType FieldType { get; }

            public string Text { get; }
        }

        private sealed class OrderedOccurrence
        {
            public OrderedOccurrence(int documentOrderIndex, BasicFieldOccurrence occurrence)
            {
                DocumentOrderIndex = documentOrderIndex;
                Occurrence = occurrence;
            }

            public int DocumentOrderIndex { get; }

            public BasicFieldOccurrence Occurrence { get; }
        }
    }
}
