using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Identifies the conservative vertical table form where a project-name header
    /// has exactly one usable project value below it in the same column. A separate
    /// business-value column is required so a one-row generic list is not enough.
    /// </summary>
    internal static class ProjectNameTableMatcher
    {
        private const string ProjectNameSuffix = "项目名称";
        private const string MigrationProjectNameSubjectHeader = "迁改项目名称";

        private static readonly string[] PlaceholderValues =
        {
            "无", "暂无", "不适用", "待定", "未填写", "详见", "-", "—", "/"
        };

        private static readonly Regex DatePattern = new Regex(
            @"^(?:\d{4}年\d{1,2}月\d{1,2}日|\d{4}(?<separator>[-/.])\d{1,2}\k<separator>\d{1,2})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PercentagePattern = new Regex(
            @"^[+-]?\d+(?:\.\d+)?\s*(?:%|％)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex QuantityPattern = new Regex(
            @"^\d+(?:\.\d+)?\s*(?:项|台|宗|套|个|辆|处|栋|件)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BusinessUnitSuffixPattern = new Regex(
            @"[（(](?:人民币)?(?:元|万元|亿元)[）)]$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex TaxBasisSuffixPattern = new Regex(
            @"(?:（(?:不含增值税|含增值税|不含税|含税)）|\((?:不含增值税|含增值税|不含税|含税)\))$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<ProjectNameTableMatch> NoMatches =
            new ReadOnlyCollection<ProjectNameTableMatch>(new ProjectNameTableMatch[0]);

        public static IReadOnlyList<ProjectNameTableMatch> FindMatches(
            IEnumerable<DocumentElement> elements)
        {
            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            var matches = new List<ProjectNameTableMatch>();
            IEnumerable<IGrouping<int, DocumentElement>> tables = elements
                .Where(IsLocatedTableCell)
                .GroupBy(item => item.Location.TableIndex.Value);

            foreach (IGrouping<int, DocumentElement> table in tables)
            {
                ProjectNameTableMatch match = TryMatchTable(table.ToList());
                if (match != null)
                {
                    matches.Add(match);
                }
            }

            return matches.Count == 0
                ? NoMatches
                : new ReadOnlyCollection<ProjectNameTableMatch>(matches);
        }

        private static ProjectNameTableMatch TryMatchTable(List<DocumentElement> cells)
        {
            List<DocumentElement> headers = cells
                .Where(cell =>
                    // A migration project is the asset/subproject money subject,
                    // not the report-level ProjectName checked by this matcher.
                    !IsMigrationProjectNameSubjectHeader(cell.Text) &&
                    IsProjectNameHeaderCandidate(cell.Text) &&
                    HasBusinessValueHeaderInSameRow(cell, cells))
                .ToList();
            if (headers.Count != 1)
            {
                return null;
            }

            DocumentElement header = headers[0];
            int headerRow = header.Location.RowIndex.Value;
            int subjectColumn = header.Location.CellIndex.Value;
            var valueCells = new List<DocumentElement>();

            foreach (IGrouping<int, DocumentElement> row in cells
                .Where(cell => cell.Location.RowIndex.Value > headerRow)
                .GroupBy(cell => cell.Location.RowIndex.Value)
                .OrderBy(row => row.Key))
            {
                if (IsAggregateRow(row))
                {
                    continue;
                }

                DocumentElement subjectCell = row.FirstOrDefault(cell =>
                    cell.Location.CellIndex.Value == subjectColumn);
                if (subjectCell == null || string.IsNullOrWhiteSpace(subjectCell.Text))
                {
                    continue;
                }

                if (!IsUsableProjectNameValue(subjectCell.Text))
                {
                    // Any unexplained, non-empty value in the candidate column makes
                    // the table ambiguous. Only explicit aggregate rows are ignored.
                    return null;
                }

                valueCells.Add(subjectCell);
                if (valueCells.Count > 1)
                {
                    return null;
                }
            }

            if (valueCells.Count != 1)
            {
                return null;
            }

            DocumentElement valueCell = valueCells[0];
            string businessKeyword;
            if (!TryFindPopulatedBusinessValueColumn(
                    header,
                    valueCell,
                    cells,
                    out businessKeyword))
            {
                return null;
            }

            return new ProjectNameTableMatch(header, valueCell, businessKeyword);
        }

        private static bool TryFindPopulatedBusinessValueColumn(
            DocumentElement projectNameHeader,
            DocumentElement valueCell,
            IEnumerable<DocumentElement> cells,
            out string businessKeyword)
        {
            int subjectColumn = projectNameHeader.Location.CellIndex.Value;
            int detailRow = valueCell.Location.RowIndex.Value;

            foreach (DocumentElement possibleHeader in cells.Where(cell =>
                cell.Location.CellIndex.Value != subjectColumn &&
                cell.Location.RowIndex.Value < detailRow))
            {
                ValueCategory category;
                string keyword;
                if (!IsBusinessValueHeaderCandidate(
                        possibleHeader.Text,
                        out category,
                        out keyword))
                {
                    continue;
                }

                int businessColumn = possibleHeader.Location.CellIndex.Value;
                bool hasDetailValue = cells.Any(cell =>
                    cell.Location.RowIndex.Value == detailRow &&
                    cell.Location.CellIndex.Value == businessColumn &&
                    !string.IsNullOrWhiteSpace(cell.Text));
                if (hasDetailValue)
                {
                    businessKeyword = keyword;
                    return true;
                }
            }

            businessKeyword = string.Empty;
            return false;
        }

        private static bool HasBusinessValueHeaderInSameRow(
            DocumentElement projectNameHeader,
            IEnumerable<DocumentElement> cells)
        {
            int headerRow = projectNameHeader.Location.RowIndex.Value;
            int subjectColumn = projectNameHeader.Location.CellIndex.Value;
            return cells.Any(cell =>
            {
                if (cell.Location.RowIndex.Value != headerRow ||
                    cell.Location.CellIndex.Value == subjectColumn)
                {
                    return false;
                }

                ValueCategory category;
                string keyword;
                return IsBusinessValueHeaderCandidate(
                    cell.Text,
                    out category,
                    out keyword);
            });
        }

        internal static bool IsBusinessValueHeaderCandidate(string text)
        {
            ValueCategory category;
            string keyword;
            return IsBusinessValueHeaderCandidate(text, out category, out keyword);
        }

        private static bool IsBusinessValueHeaderCandidate(
            string text,
            out ValueCategory category,
            out string keyword)
        {
            string normalized = BasicFieldNormalizer.NormalizeWhitespace(text)
                .Replace(" ", string.Empty);
            string withoutTaxBasisSuffix = TaxBasisSuffixPattern.Replace(
                normalized,
                string.Empty);
            bool hasTaxBasisSuffix = withoutTaxBasisSuffix.Length != normalized.Length;
            normalized = withoutTaxBasisSuffix;
            normalized = BusinessUnitSuffixPattern.Replace(normalized, string.Empty);
            if (!BusinessKeywordMatcher.TryClassifyValue(
                    normalized,
                    out category,
                    out keyword))
            {
                return false;
            }

            string prefix = normalized.Substring(0, normalized.Length - keyword.Length);
            return normalized.EndsWith(keyword, StringComparison.Ordinal) &&
                   (prefix.Length == 0 ||
                    (!hasTaxBasisSuffix && IsSupportedTaxBasisPrefix(prefix)));
        }

        private static bool IsSupportedTaxBasisPrefix(string prefix)
        {
            return string.Equals(prefix, "含税", StringComparison.Ordinal) ||
                   string.Equals(prefix, "不含税", StringComparison.Ordinal) ||
                   string.Equals(prefix, "含增值税", StringComparison.Ordinal) ||
                   string.Equals(prefix, "不含增值税", StringComparison.Ordinal);
        }

        private static bool IsLocatedTableCell(DocumentElement element)
        {
            return element.ElementType == DocumentElementType.TableCell &&
                   element.Location.TableIndex.HasValue &&
                   element.Location.RowIndex.HasValue &&
                   element.Location.CellIndex.HasValue;
        }

        private static bool IsProjectNameHeaderCandidate(string text)
        {
            string normalized = NormalizeCellText(text);
            return string.Equals(normalized, ProjectNameSuffix, StringComparison.Ordinal) ||
                   (normalized.Length > ProjectNameSuffix.Length &&
                    normalized.EndsWith(ProjectNameSuffix, StringComparison.Ordinal));
        }

        private static bool IsMigrationProjectNameSubjectHeader(string text)
        {
            return string.Equals(
                NormalizeCellText(text),
                MigrationProjectNameSubjectHeader,
                StringComparison.Ordinal);
        }

        private static bool IsUsableProjectNameValue(string text)
        {
            string normalized = NormalizeCellText(text);
            if (normalized.Length == 0 ||
                IsProjectNameHeaderCandidate(normalized) ||
                PlaceholderValues.Contains(normalized, StringComparer.Ordinal) ||
                IsAggregateLabel(normalized) ||
                DatePattern.IsMatch(normalized) ||
                PercentagePattern.IsMatch(normalized) ||
                QuantityPattern.IsMatch(normalized))
            {
                return false;
            }

            decimal number;
            if (decimal.TryParse(
                normalized.Replace(",", string.Empty),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out number))
            {
                return false;
            }

            if (MoneyCandidateExtractor.Extract(normalized).Any(candidate =>
                candidate.StartIndex == 0 && candidate.Length == normalized.Length))
            {
                return false;
            }

            return normalized.Any(char.IsLetterOrDigit);
        }

        private static bool IsAggregateRow(IEnumerable<DocumentElement> row)
        {
            return row.Any(cell => IsAggregateLabel(cell.Text));
        }

        private static bool IsAggregateLabel(string text)
        {
            string cue;
            return ProjectAggregateContextMatcher.TryMatchTableRowLabel(text, out cue);
        }

        private static string NormalizeCellText(string text)
        {
            return BasicFieldNormalizer.NormalizeWhitespace(text)
                .TrimEnd(':', '：')
                .TrimEnd();
        }
    }

    internal sealed class ProjectNameTableMatch
    {
        public ProjectNameTableMatch(
            DocumentElement headerCell,
            DocumentElement valueCell,
            string businessKeyword)
        {
            HeaderCell = headerCell ?? throw new ArgumentNullException(nameof(headerCell));
            ValueCell = valueCell ?? throw new ArgumentNullException(nameof(valueCell));
            BusinessKeyword = businessKeyword ?? string.Empty;
        }

        public DocumentElement HeaderCell { get; }

        public DocumentElement ValueCell { get; }

        public string BusinessKeyword { get; }
    }
}
