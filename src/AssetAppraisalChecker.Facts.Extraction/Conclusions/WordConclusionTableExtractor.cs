using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Facts.Extraction.Conclusions
{
    internal static class WordConclusionTableExtractor
    {
        public static ConclusionTableAmounts Extract(ConclusionSection section, string source)
        {
            var candidates = section.Elements.Where(element => element.ElementType == DocumentElementType.TableCell
                && element.Location.TableIndex.HasValue).GroupBy(element => element.Location.TableIndex.Value)
                .Where(LooksLikeResultTable).ToArray();
            if (candidates.Length != 1)
                throw Failure(candidates.Length == 0 ? WordConclusionExtractionStatus.IncompleteAmounts
                    : WordConclusionExtractionStatus.AmbiguousAmounts, "未找到唯一具有四类评估金额表头的结果表格。");
            DocumentElement[] table = candidates[0].ToArray();
            int tableIndex = candidates[0].Key;
            ValidateGrid(table);

            // Classify by logical columns in this V3-only view. A merged “合计” label
            // shifts physical cell indexes; the original locations are restored below.
            DocumentElement[] canonical = section.Elements.Where(element => !element.Location.TableIndex.HasValue
                || element.Location.TableIndex.Value == tableIndex).Select(element =>
                    element.Location.TableIndex.HasValue ? WithLogicalColumn(element) : element).ToArray();
            MoneyOccurrence[] occurrences = new MoneyOccurrenceExtractor().Extract(canonical).Where(occurrence =>
                occurrence.Location.TableIndex == tableIndex && IsRequiredField(occurrence)).ToArray();
            if (occurrences.Length != 8)
                throw Failure(occurrences.Length > 8 ? WordConclusionExtractionStatus.UnsupportedTableStructure
                    : WordConclusionExtractionStatus.IncompleteAmounts, occurrences.Length > 8
                    ? "结果表存在额外金额或资产记录，当前仅支持一条资产记录和一条合计行。"
                    : "结果表未能完整且唯一地提取资产行和合计行的四类金额，请核对表头、单位及金额文本。");

            var rows = occurrences.GroupBy(item => item.Location.RowIndex.Value).OrderBy(group => group.Key).ToArray();
            if (rows.Length != 2 || rows.Any(row => row.Count() != 4 || row.Select(FieldKey).Distinct().Count() != 4))
                throw Failure(WordConclusionExtractionStatus.UnsupportedTableStructure,
                    "当前结论结果表仅支持一条资产记录和一条合计行，且每行四类金额必须唯一。");
            int[] columns = occurrences.Select(item => item.Location.LogicalGridStart.Value).Distinct().ToArray();
            if (columns.Length != 4 || occurrences.GroupBy(item => item.Location.LogicalGridStart.Value)
                .Any(group => group.Select(FieldKey).Distinct().Count() != 1))
                throw Failure(WordConclusionExtractionStatus.AmbiguousAmounts, "资产行与合计行的逻辑金额列无法唯一对应表头。");
            int firstColumn = columns.Min();
            var actualRows = table.GroupBy(cell => cell.Location.RowIndex.Value).Where(row =>
                row.Any(cell => IsTotalLabel(cell, firstColumn) || (cell.Location.LogicalGridStart < firstColumn
                    && Regex.IsMatch(cell.Text.Trim(), @"^[1-9][0-9]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))))
                || row.Any(cell => columns.Contains(cell.Location.LogicalGridStart.Value)
                    && Regex.IsMatch(cell.Text.Trim(), @"^[0-9]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))).ToArray();
            if (actualRows.Length != 2 || actualRows.Any(row => !rows.Any(parsed => parsed.Key == row.Key))
                || table.Where(cell => cell.Location.RowIndex == rows[0].Key).Any(cell => IsTotalLabel(cell, firstColumn))
                || table.Where(cell => cell.Location.RowIndex == rows[1].Key).Count(cell => IsTotalLabel(cell, firstColumn)) != 1)
                throw Failure(WordConclusionExtractionStatus.UnsupportedTableStructure,
                    "资产/合计行标识不明确，或存在额外资产记录；未猜测行对应关系。");

            var amounts = new List<WordConclusionAmount>();
            foreach (MoneyOccurrence occurrence in occurrences.OrderBy(item => item.Location.RowIndex)
                .ThenBy(item => item.Location.LogicalGridStart))
            {
                DocumentElement original = table.Single(cell => cell.Location.RowIndex == occurrence.Location.RowIndex
                    && cell.Location.LogicalGridStart == occurrence.Location.LogicalGridStart);
                if (original.Location.LogicalGridSpan != 1)
                    throw Failure(WordConclusionExtractionStatus.UnsupportedTableStructure, "金额单元格本身跨列合并，无法唯一确定字段。");
                amounts.Add(new WordConclusionAmount(source, section.Target,
                    occurrence.Location.RowIndex == rows[0].Key ? WordConclusionSourceKind.TableAssetRow : WordConclusionSourceKind.TableTotalRow,
                    occurrence.FieldIdentity.ValueCategory, occurrence, original.Location,
                    "依据结果表表头、税务口径及逻辑网格列提取；保留原始物理单元格位置用于原文定位。"));
            }
            return new ConclusionTableAmounts(tableIndex, table.Min(cell => cell.DocumentOrderIndex), amounts);
        }

        private static bool LooksLikeResultTable(IEnumerable<DocumentElement> table)
        {
            string[] labels = table.Select(cell => WordConclusionLocator.Normalize(cell.Text)).ToArray();
            return labels.Any(text => text.Contains("原值")) && labels.Any(text => text.Contains("净值"))
                && labels.Any(text => text.Contains("不含税") || text.Contains("不含增值税"))
                && labels.Any(text => text.Replace("不含税", string.Empty).Replace("不含增值税", string.Empty)
                    .Contains("含税") || text.Replace("不含增值税", string.Empty).Contains("含增值税"));
        }

        private static bool IsRequiredField(MoneyOccurrence occurrence) =>
            (occurrence.FieldIdentity.ValueCategory == ValueCategory.AppraisedOriginalValue
                || occurrence.FieldIdentity.ValueCategory == ValueCategory.AppraisedNetValue)
            && occurrence.FieldIdentity.TaxBasis != TaxBasis.Unspecified
            && occurrence.ValueCategoryConfidence == ClassificationConfidence.High
            && occurrence.TaxBasisConfidence == ClassificationConfidence.High;

        private static string FieldKey(MoneyOccurrence occurrence) =>
            occurrence.FieldIdentity.ValueCategory + "/" + occurrence.FieldIdentity.TaxBasis;

        private static bool IsTotalLabel(DocumentElement cell, int firstColumn) =>
            cell.Location.LogicalGridStart < firstColumn
            && (WordConclusionLocator.Normalize(cell.Text) == "合计" || WordConclusionLocator.Normalize(cell.Text) == "总计");

        private static DocumentElement WithLogicalColumn(DocumentElement element)
        {
            DocumentLocation location = element.Location;
            var canonicalLocation = new DocumentLocation(location.DocumentPart, location.ParagraphIndex, location.TableIndex,
                location.RowIndex, location.LogicalGridStart, location.HeadingPath, location.TableGridColumnCount,
                location.LogicalGridStart, location.LogicalGridSpan);
            return new DocumentElement(element.ElementType, element.Text, canonicalLocation, element.DocumentOrderIndex,
                element.StyleId, element.NearbyContext, element.BoundaryMetadata);
        }

        private static void ValidateGrid(IEnumerable<DocumentElement> table)
        {
            foreach (var row in table.GroupBy(cell => cell.Location.RowIndex))
            {
                int end = -1;
                foreach (DocumentElement cell in row.OrderBy(cell => cell.Location.LogicalGridStart))
                {
                    DocumentLocation location = cell.Location;
                    if (!location.RowIndex.HasValue || !location.CellIndex.HasValue
                        || !location.LogicalGridStart.HasValue || !location.LogicalGridSpan.HasValue
                        || !location.TableGridColumnCount.HasValue || location.LogicalGridStart.Value <= end
                        || location.LogicalGridEnd.Value >= location.TableGridColumnCount.Value)
                        throw Failure(WordConclusionExtractionStatus.UnsupportedTableStructure,
                            "结果表缺少可靠逻辑网格，或合并单元格关系不明确。");
                    end = location.LogicalGridEnd.Value;
                }
            }
        }

        private static ConclusionExtractionException Failure(WordConclusionExtractionStatus status, string reason) =>
            new ConclusionExtractionException(status, reason);
    }

    internal sealed class ConclusionTableAmounts
    {
        public ConclusionTableAmounts(int tableIndex, int firstOrder, IReadOnlyList<WordConclusionAmount> amounts)
        { TableIndex = tableIndex; FirstOrder = firstOrder; Amounts = amounts; }
        public int TableIndex { get; }
        public int FirstOrder { get; }
        public IReadOnlyList<WordConclusionAmount> Amounts { get; }
    }
}
