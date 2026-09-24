using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class TableSubjectResolver
    {
        private static readonly string[] ExactAssetSubjectHeaders =
        {
            "资产名称", "设备名称", "名称", "资产项目", "标的名称"
        };

        private static readonly string[] CompetingLineNameSubjectSemantics =
        {
            "项目名称", "资产名称", "设备名称", "标的名称",
            "产权持有人", "委托人", "公司名称", "单位名称", "企业名称"
        };

        private static readonly string[] UnitOnlySubjectValues =
        {
            "元", "万元", "亿元", "台", "项", "个", "套", "辆",
            "宗", "处", "栋", "件", "米", "千米", "公里",
            "m", "km", "V", "kV", "KV", "伏", "千伏"
        };

        private const string LineNameSemantic = "线路名称";
        private const string MigrationProjectNameSemantic = "迁改项目名称";
        private const string LineNameLeftBoundaries = "（(【[、，,/／：:";
        private const string LineNameRightBoundaries = "）)】]、，,/／：:";

        private static readonly string[] ProhibitedSubjectHeaders =
        {
            "序号", "数量", "规格型号", "型号", "金额", "评估原值", "评估净值", "备注", "项目名称"
        };

        private static readonly string[] GenericProjectDetailSubjectIds =
        {
            "项目", "评估项目", "工程项目", "资产项目", "费用项目",
            "资产", "评估资产", "评估对象", "项目名称"
        };

        private static readonly Regex QuantitySubjectPattern = new Regex(
            @"^\s*\d+(?:\.\d+)?\s*(?:台|项|个|套|辆|宗|处|栋|件)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static SubjectResolution Resolve(
            DocumentElement amountElement,
            IEnumerable<DocumentElement> tableCells,
            IReadOnlyDictionary<int, string> repeatedSingleDetailSubjects)
        {
            List<DocumentElement> cells = tableCells.ToList();
            SubjectResolution project;
            if (TryResolveProjectRow(amountElement, cells, out project))
            {
                return project;
            }

            SubjectResolution projectNameRow = ResolveExplicitProjectNameRow(
                amountElement,
                cells,
                repeatedSingleDetailSubjects);
            if (projectNameRow.SubjectKey.SubjectKind != SubjectKind.Unknown)
            {
                return projectNameRow;
            }

            return ResolveAssetRow(amountElement, cells);
        }

        public static IReadOnlyDictionary<int, string> BuildRepeatedSingleDetailSubjectIndex(
            IDictionary<int, List<DocumentElement>> cellsByTable)
        {
            List<SingleDetailTableProfile> profiles = cellsByTable
                .OrderBy(item => item.Key)
                .Select(item => TryCreateSingleDetailTableProfile(item.Key, item.Value))
                .Where(profile => profile != null)
                .ToList();
            var confirmed = new Dictionary<int, string>();

            foreach (SingleDetailTableProfile profile in profiles)
            {
                bool hasExactRepeatedPeer = profiles.Any(peer =>
                    peer.TableIndex != profile.TableIndex &&
                    string.Equals(peer.SubjectId, profile.SubjectId, StringComparison.Ordinal) &&
                    peer.MoneyCategories.Overlaps(profile.MoneyCategories));
                if (hasExactRepeatedPeer)
                {
                    confirmed[profile.TableIndex] = profile.SubjectId;
                }
            }

            return confirmed;
        }

        private static bool TryResolveProjectRow(
            DocumentElement amountElement,
            IEnumerable<DocumentElement> cells,
            out SubjectResolution resolution)
        {
            foreach (DocumentElement cell in cells.Where(item =>
                item.Location.RowIndex == amountElement.Location.RowIndex))
            {
                if (ProjectSubjectResolver.TryResolve(
                    cell.Text,
                    "当前表格同一行",
                    out resolution))
                {
                    return true;
                }

                string aggregateLabel;
                if (ProjectAggregateContextMatcher.TryMatchTableRowLabel(
                        cell.Text,
                        out aggregateLabel) &&
                    (!string.Equals(aggregateLabel, "合计", StringComparison.Ordinal) ||
                     HasSingleAggregateRow(cells)))
                {
                    resolution = new SubjectResolution(
                        ComparisonSubjectKey.Project,
                        ClassificationConfidence.High,
                        new[]
                        {
                            string.Format(
                                "当前表格同一行包含唯一、明确的聚合标签“{0}”，识别为当前文档的项目主体。",
                                aggregateLabel)
                        });
                    return true;
                }
            }

            resolution = null;
            return false;
        }

        private static SubjectResolution ResolveExplicitProjectNameRow(
            DocumentElement amountElement,
            IEnumerable<DocumentElement> cells,
            IReadOnlyDictionary<int, string> repeatedSingleDetailSubjects)
        {
            List<DocumentElement> projectNameHeaders = cells
                .Where(cell =>
                    cell.Location.RowIndex < amountElement.Location.RowIndex &&
                    IsProjectNameSubjectHeaderCandidate(cell.Text))
                .GroupBy(cell => cell.Location.CellIndex.Value)
                .Select(group => group.OrderByDescending(cell => cell.Location.RowIndex).First())
                .ToList();
            if (projectNameHeaders.Count != 1)
            {
                return SubjectResolution.Unknown;
            }

            DocumentElement header = projectNameHeaders[0];
            DocumentElement subjectCell = cells.FirstOrDefault(cell =>
                cell.Location.RowIndex == amountElement.Location.RowIndex &&
                cell.Location.CellIndex == header.Location.CellIndex);
            string subjectId = SubjectTextNormalizer.Normalize(
                subjectCell == null ? string.Empty : subjectCell.Text);
            if (!IsUsableProjectDetailSubjectId(subjectId))
            {
                return SubjectResolution.Unknown;
            }

            bool hasMultipleDetailEvidence = HasReliableProjectDetailStructure(header, cells);
            string repeatedSubjectId;
            bool hasRepeatedSingleDetailEvidence =
                amountElement.Location.TableIndex.HasValue &&
                repeatedSingleDetailSubjects != null &&
                repeatedSingleDetailSubjects.TryGetValue(
                    amountElement.Location.TableIndex.Value,
                    out repeatedSubjectId) &&
                string.Equals(repeatedSubjectId, subjectId, StringComparison.Ordinal);
            if (!hasMultipleDetailEvidence && !hasRepeatedSingleDetailEvidence)
            {
                return SubjectResolution.Unknown;
            }

            // SubjectKind.Asset continues to represent a concrete, non-project-total
            // business row. The explicit row text keeps different project rows isolated.
            return new SubjectResolution(
                new ComparisonSubjectKey(SubjectKind.Asset, subjectId),
                ClassificationConfidence.High,
                new[]
                {
                    hasMultipleDetailEvidence
                        ? string.Format(
                            "同一表格“{0}”候选列具有明确金额业务列和至少两个不同有效主体值，按多行明细结构识别业务行主体“{1}”。",
                            SubjectTextNormalizer.Normalize(header.Text),
                            subjectId)
                        : string.Format(
                            "同一表格“{0}”候选列具有单一有效明细、聚合行和明确金额业务列；文档中另一独立重复单明细业务表提供相同主体及共享金额字段，识别业务行主体“{1}”。",
                            SubjectTextNormalizer.Normalize(header.Text),
                            subjectId)
                });
        }

        private static SingleDetailTableProfile TryCreateSingleDetailTableProfile(
            int tableIndex,
            IEnumerable<DocumentElement> cells)
        {
            List<DocumentElement> cellList = cells.ToList();
            List<DocumentElement> headers = cellList
                .Where(cell => IsProjectNameSubjectHeaderCandidate(cell.Text))
                .GroupBy(cell => cell.Location.CellIndex.Value)
                .Select(group => group.OrderBy(cell => cell.Location.RowIndex).First())
                .ToList();
            if (headers.Count != 1)
            {
                return null;
            }

            DocumentElement header = headers[0];
            List<DocumentElement> subjectColumnCells = cellList
                .Where(cell =>
                    cell.Location.CellIndex == header.Location.CellIndex &&
                    cell.Location.RowIndex > header.Location.RowIndex)
                .ToList();
            List<DocumentElement> detailCells = subjectColumnCells
                .Where(cell => IsUsableProjectDetailSubjectId(
                    SubjectTextNormalizer.Normalize(cell.Text)))
                .ToList();
            if (detailCells.Count != 1 ||
                subjectColumnCells.Any(cell =>
                    !string.IsNullOrWhiteSpace(cell.Text) &&
                    !IsAggregateLabel(cell.Text) &&
                    !IsUsableProjectDetailSubjectId(
                        SubjectTextNormalizer.Normalize(cell.Text))))
            {
                return null;
            }

            DocumentElement detailCell = detailCells[0];
            List<MoneyColumn> moneyColumns = cellList
                .Where(cell =>
                    cell.Location.CellIndex != header.Location.CellIndex &&
                    cell.Location.RowIndex < detailCell.Location.RowIndex)
                .Select(TryCreateMoneyColumn)
                .Where(column => column != null)
                .GroupBy(column => column.ColumnIndex)
                .Select(group => group.First())
                .ToList();
            if (moneyColumns.Count == 0)
            {
                return null;
            }

            // Aggregate evidence is strictly row-local. A label in another row,
            // nearby context, heading, or paragraph can never qualify this row.
            List<int> aggregateRows = cellList
                .Where(cell =>
                    cell.Location.RowIndex > header.Location.RowIndex &&
                    cell.Location.RowIndex != detailCell.Location.RowIndex)
                .GroupBy(cell => cell.Location.RowIndex.Value)
                .Where(row =>
                    row.Any(cell => IsAggregateLabel(cell.Text)) &&
                    moneyColumns.Any(column => row.Any(cell =>
                        cell.Location.CellIndex == column.ColumnIndex &&
                        !string.IsNullOrWhiteSpace(cell.Text))))
                .Select(row => row.Key)
                .ToList();
            if (aggregateRows.Count != 1)
            {
                return null;
            }

            var moneyCategories = new HashSet<ValueCategory>();
            foreach (MoneyColumn moneyColumn in moneyColumns)
            {
                bool hasDetailValue = cellList.Any(cell =>
                    cell.Location.RowIndex == detailCell.Location.RowIndex &&
                    cell.Location.CellIndex == moneyColumn.ColumnIndex &&
                    !string.IsNullOrWhiteSpace(cell.Text));
                bool hasAggregateValue = aggregateRows.Any(row => cellList.Any(cell =>
                    cell.Location.RowIndex == row &&
                    cell.Location.CellIndex == moneyColumn.ColumnIndex &&
                    !string.IsNullOrWhiteSpace(cell.Text)));
                if (hasDetailValue && hasAggregateValue)
                {
                    moneyCategories.Add(moneyColumn.ValueCategory);
                }
            }

            if (moneyCategories.Count == 0)
            {
                return null;
            }

            return new SingleDetailTableProfile(
                tableIndex,
                SubjectTextNormalizer.Normalize(detailCell.Text),
                moneyCategories);
        }

        private static MoneyColumn TryCreateMoneyColumn(DocumentElement possibleHeader)
        {
            ValueCategory category;
            string keyword;
            return BusinessKeywordMatcher.TryClassifyValue(
                possibleHeader.Text,
                out category,
                out keyword)
                ? new MoneyColumn(possibleHeader.Location.CellIndex.Value, category)
                : null;
        }

        private static bool IsProjectNameSubjectHeaderCandidate(string text)
        {
            string normalized = SubjectTextNormalizer.Normalize(text);
            return string.Equals(normalized, "项目名称", StringComparison.Ordinal) ||
                   (normalized.Length > "项目名称".Length &&
                    normalized.EndsWith("项目名称", StringComparison.Ordinal));
        }

        private static bool HasReliableProjectDetailStructure(
            DocumentElement header,
            IEnumerable<DocumentElement> cells)
        {
            // A “项目名称” suffix is only a candidate: multiple distinct usable values
            // and a populated business-money column are required to prove a row-oriented
            // detail table instead of a label/value project metadata table.
            List<DocumentElement> detailSubjectCells = cells
                .Where(cell =>
                    cell.Location.CellIndex == header.Location.CellIndex &&
                    cell.Location.RowIndex > header.Location.RowIndex)
                .Where(cell => IsUsableProjectDetailSubjectId(
                    SubjectTextNormalizer.Normalize(cell.Text)))
                .ToList();
            List<int> detailRows = detailSubjectCells
                .Select(cell => cell.Location.RowIndex.Value)
                .Distinct()
                .ToList();
            int distinctSubjectCount = detailSubjectCells
                .Select(cell => SubjectTextNormalizer.Normalize(cell.Text))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count();
            if (detailRows.Count < 2 || distinctSubjectCount < 2)
            {
                return false;
            }

            int firstDetailRow = detailRows.Min();
            foreach (DocumentElement possibleMoneyHeader in cells.Where(cell =>
                cell.Location.CellIndex != header.Location.CellIndex &&
                cell.Location.RowIndex < firstDetailRow))
            {
                ValueCategory category;
                string keyword;
                if (!BusinessKeywordMatcher.TryClassifyValue(
                    possibleMoneyHeader.Text,
                    out category,
                    out keyword))
                {
                    continue;
                }

                int moneyColumn = possibleMoneyHeader.Location.CellIndex.Value;
                int coveredDetailRows = detailRows.Count(row => cells.Any(cell =>
                    cell.Location.RowIndex == row &&
                    cell.Location.CellIndex == moneyColumn));
                if (coveredDetailRows >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsableProjectDetailSubjectId(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId) ||
                subjectId.Length > 80 ||
                IsAggregateLabel(subjectId) ||
                GenericProjectDetailSubjectIds.Any(item =>
                    string.Equals(subjectId, item, StringComparison.Ordinal)) ||
                ProhibitedSubjectHeaders.Any(header =>
                    string.Equals(subjectId, header, StringComparison.Ordinal)) ||
                QuantitySubjectPattern.IsMatch(subjectId))
            {
                return false;
            }

            decimal numericValue;
            if (decimal.TryParse(
                subjectId.Replace(",", string.Empty),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out numericValue))
            {
                return false;
            }

            if (MoneyCandidateExtractor.Extract(subjectId)
                .Any(candidate => MoneyNormalizer.Parse(candidate.RawText).IsSuccess))
            {
                return false;
            }

            ValueCategory category;
            string keyword;
            if (BusinessKeywordMatcher.TryClassifyValue(subjectId, out category, out keyword))
            {
                return false;
            }

            return subjectId.Any(char.IsLetterOrDigit);
        }

        private sealed class SingleDetailTableProfile
        {
            public SingleDetailTableProfile(
                int tableIndex,
                string subjectId,
                HashSet<ValueCategory> moneyCategories)
            {
                TableIndex = tableIndex;
                SubjectId = subjectId;
                MoneyCategories = moneyCategories;
            }

            public int TableIndex { get; }

            public string SubjectId { get; }

            public HashSet<ValueCategory> MoneyCategories { get; }
        }

        private sealed class MoneyColumn
        {
            public MoneyColumn(int columnIndex, ValueCategory valueCategory)
            {
                ColumnIndex = columnIndex;
                ValueCategory = valueCategory;
            }

            public int ColumnIndex { get; }

            public ValueCategory ValueCategory { get; }
        }

        private static bool HasSingleAggregateRow(IEnumerable<DocumentElement> cells)
        {
            return cells
                .Where(cell => IsAggregateLabel(cell.Text))
                .Select(cell => cell.Location.RowIndex.Value)
                .Distinct()
                .Take(2)
                .Count() == 1;
        }

        private static bool IsAggregateLabel(string text)
        {
            string matchedCue;
            return ProjectAggregateContextMatcher.TryMatchTableRowLabel(text, out matchedCue);
        }

        private static SubjectResolution ResolveAssetRow(
            DocumentElement amountElement,
            IEnumerable<DocumentElement> cells)
        {
            List<DocumentElement> cellList = cells.ToList();
            List<DocumentElement> subjectHeaders = cellList
                .Where(cell =>
                    cell.Location.RowIndex < amountElement.Location.RowIndex &&
                    (IsAssetSubjectHeader(cell.Text) ||
                     IsAuditedLineNameSubjectHeader(cell.Text) ||
                     IsMigrationProjectNameSubjectHeader(cell.Text)))
                .GroupBy(cell => cell.Location.CellIndex.Value)
                .Select(group => group.OrderByDescending(cell => cell.Location.RowIndex).First())
                .ToList();

            if (subjectHeaders.Count != 1)
            {
                return SubjectResolution.Unknown;
            }

            DocumentElement header = subjectHeaders[0];
            DocumentElement subjectCell = cellList.FirstOrDefault(cell =>
                cell.Location.RowIndex == amountElement.Location.RowIndex &&
                cell.Location.CellIndex == header.Location.CellIndex);
            string subjectId = SubjectTextNormalizer.Normalize(subjectCell == null ? string.Empty : subjectCell.Text);
            bool isAuditedLineNameHeader = IsAuditedLineNameSubjectHeader(header.Text);
            bool isMigrationProjectNameHeader =
                IsMigrationProjectNameSubjectHeader(header.Text);
            if (isMigrationProjectNameHeader
                    ? !IsSafeMigrationProjectNameDetailSubject(
                        amountElement,
                        header,
                        subjectCell,
                        cellList,
                        subjectId)
                    : isAuditedLineNameHeader
                    ? !IsSafeAuditedLineNameDetailSubject(
                        amountElement,
                        header,
                        subjectCell,
                        cellList,
                        subjectId)
                    : !IsUsableSubjectId(subjectId))
            {
                return SubjectResolution.Unknown;
            }

            return new SubjectResolution(
                new ComparisonSubjectKey(SubjectKind.Asset, subjectId),
                ClassificationConfidence.High,
                new[]
                {
                    isMigrationProjectNameHeader
                        ? string.Format(
                            "同一表格存在唯一、精确匹配的迁改项目名称主体列，当前明细行只有一个安全文本主体候选“{0}”。",
                            subjectId)
                        : isAuditedLineNameHeader
                        ? string.Format(
                            "同一表格存在唯一、边界明确的线路名称主体列，当前明细行只有一个安全文本主体候选“{0}”。",
                            subjectId)
                        : string.Format(
                            "同一表格“{0}”列识别为资产主体“{1}”。",
                            SubjectTextNormalizer.Normalize(header.Text),
                            subjectId)
                });
        }

        private static bool IsMigrationProjectNameSubjectHeader(string text)
        {
            string normalized = RemoveWhitespace(SubjectTextNormalizer.Normalize(text));
            return string.Equals(
                normalized,
                MigrationProjectNameSemantic,
                StringComparison.Ordinal);
        }

        private static bool IsSafeMigrationProjectNameDetailSubject(
            DocumentElement amountElement,
            DocumentElement header,
            DocumentElement subjectCell,
            IEnumerable<DocumentElement> cells,
            string subjectId)
        {
            List<DocumentElement> cellList = cells.ToList();
            List<DocumentElement> migrationHeaders = cellList
                .Where(cell =>
                    cell.Location.RowIndex < amountElement.Location.RowIndex &&
                    IsMigrationProjectNameSubjectHeader(cell.Text))
                .GroupBy(cell => cell.Location.CellIndex.Value)
                .Select(group => group.OrderByDescending(cell => cell.Location.RowIndex).First())
                .ToList();
            if (migrationHeaders.Count != 1 ||
                !ReferenceEquals(migrationHeaders[0], header))
            {
                return false;
            }

            bool hasCompetingSubjectColumn = cellList.Any(cell =>
                cell.Location.RowIndex < amountElement.Location.RowIndex &&
                cell.Location.CellIndex != header.Location.CellIndex &&
                (IsAssetSubjectHeader(cell.Text) ||
                 IsAuditedLineNameSubjectHeader(cell.Text) ||
                 IsProjectNameSubjectHeaderCandidate(cell.Text)));
            if (hasCompetingSubjectColumn ||
                subjectCell == null ||
                !IsUsableMigrationProjectNameSubjectId(subjectId))
            {
                return false;
            }

            List<DocumentElement> rowCells = cellList
                .Where(cell => cell.Location.RowIndex == amountElement.Location.RowIndex)
                .ToList();
            if (rowCells.Any(cell => IsAggregateSubjectValue(cell.Text)))
            {
                return false;
            }

            List<DocumentElement> candidates = rowCells
                .Where(cell => IsUsableMigrationProjectNameSubjectId(
                    SubjectTextNormalizer.Normalize(cell.Text)))
                .ToList();
            return candidates.Count == 1 &&
                   ReferenceEquals(candidates[0], subjectCell) &&
                   subjectCell.Location.CellIndex == header.Location.CellIndex;
        }

        private static bool IsUsableMigrationProjectNameSubjectId(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId) ||
                IsAggregateSubjectValue(subjectId) ||
                QuantitySubjectPattern.IsMatch(subjectId) ||
                UnitOnlySubjectValues.Any(unit =>
                    string.Equals(subjectId, unit, StringComparison.Ordinal)) ||
                ProhibitedSubjectHeaders.Any(header =>
                    string.Equals(subjectId, header, StringComparison.Ordinal)))
            {
                return false;
            }

            decimal numericValue;
            if (decimal.TryParse(
                subjectId.Replace(",", string.Empty),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out numericValue))
            {
                return false;
            }

            if (MoneyCandidateExtractor.Extract(subjectId)
                .Any(candidate => MoneyNormalizer.Parse(candidate.RawText).IsSuccess))
            {
                return false;
            }

            ValueCategory category;
            string keyword;
            if (BusinessKeywordMatcher.TryClassifyValue(subjectId, out category, out keyword))
            {
                return false;
            }

            return subjectId.Any(char.IsLetterOrDigit);
        }

        private static bool IsAuditedLineNameSubjectHeader(string text)
        {
            string normalized = RemoveWhitespace(SubjectTextNormalizer.Normalize(text));
            int semanticIndex = normalized.IndexOf(LineNameSemantic, StringComparison.Ordinal);
            if (semanticIndex < 0 ||
                normalized.IndexOf(
                    LineNameSemantic,
                    semanticIndex + LineNameSemantic.Length,
                    StringComparison.Ordinal) >= 0 ||
                !HasLineNameSemanticBoundary(
                    normalized,
                    semanticIndex,
                    LineNameSemantic.Length))
            {
                return false;
            }

            return !CompetingLineNameSubjectSemantics.Any(semantic =>
                normalized.IndexOf(semantic, StringComparison.Ordinal) >= 0);
        }

        private static bool HasLineNameSemanticBoundary(
            string text,
            int semanticIndex,
            int semanticLength)
        {
            bool hasLeftBoundary = semanticIndex == 0 ||
                LineNameLeftBoundaries.IndexOf(text[semanticIndex - 1]) >= 0;
            int semanticEnd = semanticIndex + semanticLength;
            bool hasRightBoundary = semanticEnd == text.Length ||
                LineNameRightBoundaries.IndexOf(text[semanticEnd]) >= 0;
            return hasLeftBoundary && hasRightBoundary;
        }

        private static bool IsSafeAuditedLineNameDetailSubject(
            DocumentElement amountElement,
            DocumentElement header,
            DocumentElement subjectCell,
            IEnumerable<DocumentElement> cells,
            string subjectId)
        {
            if (subjectCell == null ||
                !IsUsableAuditedLineNameSubjectId(subjectId))
            {
                return false;
            }

            List<DocumentElement> rowCells = cells
                .Where(cell => cell.Location.RowIndex == amountElement.Location.RowIndex)
                .ToList();
            if (rowCells.Any(cell => IsAggregateSubjectValue(cell.Text)))
            {
                return false;
            }

            List<DocumentElement> candidates = rowCells
                .Where(cell => IsPotentialAuditedLineNameSubjectCandidate(cell.Text))
                .ToList();
            return candidates.Count == 1 &&
                   ReferenceEquals(candidates[0], subjectCell) &&
                   subjectCell.Location.CellIndex == header.Location.CellIndex;
        }

        private static bool IsPotentialAuditedLineNameSubjectCandidate(string text)
        {
            string normalized = SubjectTextNormalizer.Normalize(text);
            return IsUsableAuditedLineNameSubjectId(normalized);
        }

        private static bool IsUsableAuditedLineNameSubjectId(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId) ||
                subjectId.Length > 80 ||
                IsAggregateSubjectValue(subjectId) ||
                UnitOnlySubjectValues.Any(unit =>
                    string.Equals(subjectId, unit, StringComparison.Ordinal)) ||
                ProhibitedSubjectHeaders.Any(header =>
                    string.Equals(subjectId, header, StringComparison.Ordinal)))
            {
                return false;
            }

            decimal numericValue;
            if (decimal.TryParse(
                subjectId.Replace(",", string.Empty),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out numericValue))
            {
                return false;
            }

            if (MoneyCandidateExtractor.Extract(subjectId)
                .Any(candidate => MoneyNormalizer.Parse(candidate.RawText).IsSuccess))
            {
                return false;
            }

            return subjectId.Any(char.IsLetterOrDigit);
        }

        private static bool IsAggregateSubjectValue(string text)
        {
            string normalized = RemoveWhitespace(SubjectTextNormalizer.Normalize(text));
            return string.Equals(normalized, "合计", StringComparison.Ordinal) ||
                   string.Equals(normalized, "总计", StringComparison.Ordinal) ||
                   string.Equals(normalized, "小计", StringComparison.Ordinal);
        }

        private static string RemoveWhitespace(string text)
        {
            return new string((text ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character) && character != '\u3000')
                .ToArray());
        }

        private static bool IsAssetSubjectHeader(string text)
        {
            string normalized = SubjectTextNormalizer.Normalize(text);
            if (ProhibitedSubjectHeaders.Any(header =>
                string.Equals(normalized, header, StringComparison.Ordinal)))
            {
                return false;
            }

            return ExactAssetSubjectHeaders.Any(header =>
                       string.Equals(normalized, header, StringComparison.Ordinal)) ||
                   normalized.EndsWith("资产名称", StringComparison.Ordinal) ||
                   normalized.EndsWith("设备名称", StringComparison.Ordinal) ||
                   normalized.EndsWith("标的名称", StringComparison.Ordinal);
        }

        private static bool IsUsableSubjectId(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId) ||
                ProhibitedSubjectHeaders.Any(header =>
                    string.Equals(subjectId, header, StringComparison.Ordinal)))
            {
                return false;
            }

            decimal numericValue;
            return !decimal.TryParse(
                subjectId.Replace(",", string.Empty),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out numericValue);
        }
    }
}
