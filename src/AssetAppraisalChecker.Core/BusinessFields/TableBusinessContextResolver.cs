using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal sealed class TableBusinessContextResolver
    {
        private static readonly Regex UnitDeclarationPattern = new Regex(
            @"金额单位\s*[:：]?\s*(?:人民币)?\s*(?<unit>亿元|万元|元)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IReadOnlyList<DocumentElement> _elements;
        private readonly IDictionary<int, List<DocumentElement>> _cellsByTable;
        private readonly IDictionary<int, MoneyUnit> _unitByTable;
        private readonly IReadOnlyDictionary<int, string> _repeatedSingleDetailSubjects;
        private readonly IReadOnlyDictionary<int, MergedHeaderColumnSemanticResolver>
            _mergedHeaderResolvers;

        public TableBusinessContextResolver(IReadOnlyList<DocumentElement> elements)
        {
            _elements = elements;
            _cellsByTable = elements
                .Where(element => element.Location.TableIndex.HasValue)
                .GroupBy(element => element.Location.TableIndex.Value)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.DocumentOrderIndex).ToList());
            _unitByTable = BuildTableUnits();
            _repeatedSingleDetailSubjects =
                TableSubjectResolver.BuildRepeatedSingleDetailSubjectIndex(_cellsByTable);
            _mergedHeaderResolvers = _cellsByTable.ToDictionary(
                table => table.Key,
                table => new MergedHeaderColumnSemanticResolver(table.Value));
        }

        public TableBusinessContext Resolve(DocumentElement element)
        {
            if (!element.Location.TableIndex.HasValue ||
                !element.Location.RowIndex.HasValue ||
                !element.Location.CellIndex.HasValue)
            {
                return TableBusinessContext.Empty;
            }

            int tableIndex = element.Location.TableIndex.Value;
            List<DocumentElement> cells;
            if (!_cellsByTable.TryGetValue(tableIndex, out cells))
            {
                return TableBusinessContext.Empty;
            }

            MergedHeaderColumnSemanticResolver mergedResolver;
            MergedHeaderColumnSemantic mergedSemantic = null;
            bool hasMergedResolver = _mergedHeaderResolvers.TryGetValue(
                tableIndex,
                out mergedResolver);
            if (hasMergedResolver)
            {
                mergedResolver.TryResolve(element, out mergedSemantic);
            }

            string header = hasMergedResolver &&
                (mergedSemantic != null || mergedResolver.SuppressPhysicalHeaderFallback)
                    ? string.Empty
                    : FindColumnHeader(element, cells);
            string rowLabel = FindRowLabel(element, cells);
            MoneyUnit unit;
            if (!_unitByTable.TryGetValue(tableIndex, out unit))
            {
                unit = MoneyUnit.Unknown;
            }

            SubjectResolution subject = TableSubjectResolver.Resolve(
                element,
                cells,
                _repeatedSingleDetailSubjects);
            bool isMergedHeaderProjectAggregateRow =
                mergedSemantic != null &&
                subject.SubjectKey.SubjectKind == SubjectKind.Project &&
                cells.Where(cell =>
                        cell.Location.RowIndex == element.Location.RowIndex)
                    .Any(cell =>
                    {
                        string matchedCue;
                        return ProjectAggregateContextMatcher.TryMatchTableRowLabel(
                            cell.Text,
                            out matchedCue);
                    });
            return new TableBusinessContext(
                header,
                rowLabel,
                unit,
                subject,
                mergedSemantic,
                isMergedHeaderProjectAggregateRow);
        }

        private IDictionary<int, MoneyUnit> BuildTableUnits()
        {
            var units = new Dictionary<int, MoneyUnit>();
            foreach (KeyValuePair<int, List<DocumentElement>> table in _cellsByTable)
            {
                var detectedUnits = new HashSet<MoneyUnit>();
                foreach (DocumentElement cell in table.Value)
                {
                    AddDetectedUnit(cell.Text, detectedUnits);
                }

                int firstOrderIndex = table.Value.Min(element => element.DocumentOrderIndex);
                int inspectedEffectiveElements = 0;
                foreach (DocumentElement previous in _elements
                    .Where(element => element.DocumentOrderIndex < firstOrderIndex)
                    .OrderByDescending(element => element.DocumentOrderIndex))
                {
                    if (string.IsNullOrWhiteSpace(previous.Text))
                    {
                        continue;
                    }

                    // A preceding table is a hard boundary: its unit must not leak forward.
                    if (previous.Location.TableIndex.HasValue)
                    {
                        break;
                    }

                    AddDetectedUnit(previous.Text, detectedUnits);
                    inspectedEffectiveElements++;
                    if (inspectedEffectiveElements >= 3)
                    {
                        break;
                    }
                }

                if (detectedUnits.Count == 1)
                {
                    units[table.Key] = detectedUnits.Single();
                }
            }

            return units;
        }

        private static string FindColumnHeader(
            DocumentElement element,
            IEnumerable<DocumentElement> cells)
        {
            foreach (DocumentElement candidate in cells
                .Where(cell =>
                    cell.Location.CellIndex == element.Location.CellIndex &&
                    cell.Location.RowIndex < element.Location.RowIndex)
                .OrderByDescending(cell => cell.Location.RowIndex))
            {
                ValueCategory category;
                string keyword;
                if (BusinessKeywordMatcher.TryClassifyValue(candidate.Text, out category, out keyword))
                {
                    return candidate.Text;
                }

                string aggregateLabel;
                if (ProjectAggregateContextMatcher.TryMatchTableRowLabel(
                    candidate.Text,
                    out aggregateLabel))
                {
                    return aggregateLabel;
                }

                Scope scope;
                if (BusinessKeywordMatcher.TryClassifyScope(candidate.Text, out scope, out keyword))
                {
                    return candidate.Text;
                }
            }

            return string.Empty;
        }

        private static string FindRowLabel(
            DocumentElement element,
            IEnumerable<DocumentElement> cells)
        {
            foreach (DocumentElement candidate in cells
                .Where(cell =>
                    cell.Location.RowIndex == element.Location.RowIndex &&
                    cell.Location.CellIndex != element.Location.CellIndex)
                .OrderBy(cell => Math.Abs(cell.Location.CellIndex.Value - element.Location.CellIndex.Value))
                .ThenBy(cell => cell.Location.CellIndex > element.Location.CellIndex ? 1 : 0))
            {
                ValueCategory category;
                string keyword;
                if (BusinessKeywordMatcher.TryClassifyValue(candidate.Text, out category, out keyword))
                {
                    return candidate.Text;
                }

                Scope scope;
                if (BusinessKeywordMatcher.TryClassifyScope(candidate.Text, out scope, out keyword))
                {
                    return candidate.Text;
                }
            }

            return string.Empty;
        }

        private static void AddDetectedUnit(string text, ISet<MoneyUnit> detectedUnits)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (Match match in UnitDeclarationPattern.Matches(text))
            {
                switch (match.Groups["unit"].Value)
                {
                    case "万元":
                        detectedUnits.Add(MoneyUnit.TenThousandYuan);
                        break;
                    case "亿元":
                        detectedUnits.Add(MoneyUnit.HundredMillionYuan);
                        break;
                    case "元":
                        detectedUnits.Add(MoneyUnit.Yuan);
                        break;
                }
            }
        }
    }
}
