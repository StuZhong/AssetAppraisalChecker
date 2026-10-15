using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Mechanical.Units
{
    public sealed class UnitConsistencyChecker : IMechanicalDocumentChecker
    {
        private static readonly UnitFamily[] QuantityFamilies =
        {
            UnitFamily.Count,
            UnitFamily.Mass,
            UnitFamily.Length,
            UnitFamily.Area,
            UnitFamily.Volume,
            UnitFamily.Time
        };

        private readonly UnitParser _parser;

        public UnitConsistencyChecker()
            : this(new UnitParser())
        {
        }

        public UnitConsistencyChecker(UnitParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public string Name => nameof(UnitConsistencyChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var relations = new List<UnitRelation>();
            AddTableRelations(context.Document.Elements, relations);
            AddExplicitTextRelations(context.Document.Elements, relations);

            List<MechanicalIssue> issues = relations
                .Where(item => item.IsComparable && !item.IsConsistent)
                .Select(item => CreateIssue(context, item))
                .ToList();
            return MechanicalCheckerSupport.ReadOnly(issues);
        }

        private void AddTableRelations(
            IEnumerable<DocumentElement> elements,
            ICollection<UnitRelation> relations)
        {
            var tables = elements
                .Where(item => item.ElementType == DocumentElementType.TableCell &&
                    item.Location.TableIndex.HasValue &&
                    item.Location.RowIndex.HasValue)
                .GroupBy(item => item.Location.TableIndex.Value);

            foreach (IGrouping<int, DocumentElement> table in tables)
            {
                List<IGrouping<int, DocumentElement>> rows = table
                    .GroupBy(item => item.Location.RowIndex.Value)
                    .OrderBy(item => item.Key)
                    .ToList();
                HeaderLayout layout = FindHeaderLayout(rows);

                foreach (IGrouping<int, DocumentElement> row in rows)
                {
                    List<DocumentElement> cells = row
                        .OrderBy(item => item.Location.CellIndex ?? int.MaxValue)
                        .ToList();
                    UnitRelation relation = null;
                    if (layout != null && row.Key > layout.RowIndex)
                    {
                        relation = FindHeaderGuidedRelation(cells, layout);
                    }
                    if (relation == null) relation = FindUniqueInlineRelation(cells);

                    if (relation != null)
                    {
                        relations.Add(relation);
                    }
                }
            }
        }


        private UnitRelation FindUniqueInlineRelation(IReadOnlyList<DocumentElement> cells)
        {
            var scalar = new List<Candidate>();
            var prices = new List<Candidate>();

            foreach (DocumentElement cell in cells)
            {
                foreach (UnitExpression expression in _parser.FindExpressions(cell.Text))
                {
                    var candidate = new Candidate(expression, cell);
                    if (IsQuantity(expression))
                    {
                        scalar.Add(candidate);
                    }
                    else if (IsUnitPrice(expression))
                    {
                        prices.Add(candidate);
                    }
                }
            }

            return scalar.Count == 1 && prices.Count == 1
                ? new UnitRelation(
                    scalar[0].Expression,
                    prices[0].Expression,
                    scalar[0].Element,
                    prices[0].Element)
                : null;
        }

        private UnitRelation FindHeaderGuidedRelation(
            IReadOnlyList<DocumentElement> cells,
            HeaderLayout layout)
        {
            DocumentElement quantityCell = FindCell(cells, layout.QuantityColumn);
            DocumentElement priceCell = FindCell(cells, layout.PriceColumn);
            if (quantityCell == null || priceCell == null)
            {
                return null;
            }
            if (quantityCell.Location.LogicalGridSpan > 1 || priceCell.Location.LogicalGridSpan > 1 ||
                quantityCell.Text.Contains('\n') || priceCell.Text.Contains('\n')) return null;

            UnitExpression quantity = _parser.FindExpressions(quantityCell.Text)
                .SingleOrDefaultOrNull(IsQuantity);
            UnitExpression price = _parser.FindExpressions(priceCell.Text)
                .SingleOrDefaultOrNull(IsUnitPrice);
            DocumentElement quantityEvidence = quantityCell;

            if (quantity == null && layout.UnitColumn.HasValue)
            {
                DocumentElement unitCell = FindCell(cells, layout.UnitColumn.Value);
                if (unitCell != null &&
                    ContainsNumericValue(quantityCell.Text) &&
                    _parser.TryParse(unitCell.Text, out UnitExpression standalone) &&
                    IsQuantity(standalone))
                {
                    quantity = standalone;
                    quantityEvidence = unitCell;
                }
            }

            return quantity != null && price != null
                ? new UnitRelation(quantity, price, quantityEvidence, priceCell, true)
                : null;
        }

        private void AddExplicitTextRelations(
            IEnumerable<DocumentElement> elements,
            ICollection<UnitRelation> relations)
        {
            foreach (DocumentElement element in elements.Where(item =>
                item.ElementType != DocumentElementType.TableCell &&
                HasExplicitRelationLabels(item.Text)))
            {
                IReadOnlyList<UnitExpression> expressions = _parser.FindExpressions(element.Text);
                List<UnitExpression> quantities = expressions.Where(IsQuantity).ToList();
                List<UnitExpression> prices = expressions.Where(IsUnitPrice).ToList();
                if (quantities.Count == 1 && prices.Count == 1)
                {
                    relations.Add(new UnitRelation(quantities[0], prices[0], element, element, true));
                }
            }
        }

        private static HeaderLayout FindHeaderLayout(
            IEnumerable<IGrouping<int, DocumentElement>> rows)
        {
            foreach (IGrouping<int, DocumentElement> row in rows)
            {
                List<DocumentElement> cells = row
                    .Where(item => item.Location.CellIndex.HasValue)
                    .OrderBy(item => item.Location.CellIndex.Value)
                    .ToList();
                List<int> quantityColumns = cells
                    .Where(item => IsQuantityHeader(item.Text) && IsSimpleHeader(item))
                    .Select(item => item.Location.CellIndex.Value)
                    .ToList();
                List<int> priceColumns = cells
                    .Where(item => IsPriceHeader(item.Text) && IsSimpleHeader(item))
                    .Select(item => item.Location.CellIndex.Value)
                    .ToList();
                List<int> unitColumns = cells
                    .Where(item => string.Equals(item.Text.Trim(), "单位", StringComparison.Ordinal))
                    .Select(item => item.Location.CellIndex.Value)
                    .ToList();

                if (quantityColumns.Count == 1 && priceColumns.Count == 1 && unitColumns.Count <= 1)
                {
                    return new HeaderLayout(
                        row.Key,
                        quantityColumns[0],
                        priceColumns[0],
                        unitColumns.Count == 1 ? unitColumns[0] : (int?)null);
                }
            }

            return null;
        }

        private static DocumentElement FindCell(
            IEnumerable<DocumentElement> cells,
            int cellIndex)
        {
            return cells.SingleOrDefault(item => item.Location.CellIndex == cellIndex);
        }

        private static bool IsQuantity(UnitExpression expression)
        {
            return expression != null &&
                !expression.IsCompound &&
                QuantityFamilies.Contains(expression.Family);
        }

        private static bool IsUnitPrice(UnitExpression expression)
        {
            return expression != null &&
                expression.IsCompound &&
                expression.NumeratorFamily == UnitFamily.Currency;
        }

        private static bool IsQuantityHeader(string text)
        {
            string value = (text ?? string.Empty).Trim();
            return Regex.IsMatch(value,
                @"^(?:资产|设备|建筑|土地)?(?:数量|面积|体积|质量|重量|长度)(?:[（(][^）)\r\n]+[）)])?$",
                RegexOptions.CultureInvariant);
        }

        private static bool IsPriceHeader(string text)
        {
            return Regex.IsMatch((text ?? string.Empty).Trim(),
                @"^(?:含税|不含税)?单价(?:[（(][^）)\r\n]+[）)])?$", RegexOptions.CultureInvariant);
        }

        private static bool IsSimpleHeader(DocumentElement element) =>
            element.Location.LogicalGridSpan.GetValueOrDefault(1) == 1 && !element.Text.Contains('\n');

        private static bool HasExplicitRelationLabels(string text)
        {
            string value = text ?? string.Empty;
            // Adjacent labelled values, not labels somewhere in the same explanatory paragraph.
            return Regex.IsMatch(value,
                @"(?:数量|面积|体积|质量|重量|长度)\s*(?:为|[:：=])?\s*[+-]?\d+(?:,\d{3})*(?:\.\d+)?\s*[A-Za-z\u4e00-\u9fff㎡²³]+\s*[,，]\s*单价\s*(?:为|[:：=])?\s*[+-]?\d",
                RegexOptions.CultureInvariant);
        }

        private static bool ContainsNumericValue(string text)
        {
            return Regex.IsMatch((text ?? string.Empty).Trim(),
                @"^[+-]?\d+(?:,\d{3})*(?:\.\d+)?$", RegexOptions.CultureInvariant);
        }

        private static MechanicalIssue CreateIssue(
            MechanicalCheckContext context,
            UnitRelation relation)
        {
            string quantityFamily = FamilyDisplay(relation.QuantityUnit.Family);
            string denominatorFamily = FamilyDisplay(relation.UnitPrice.DenominatorFamily);
            var occurrences = new List<MechanicalIssueOccurrence>
            {
                MechanicalCheckerSupport.Occurrence(
                    context,
                    relation.QuantityElement,
                    relation.QuantityElement.Text,
                    "数量单位原文：" + relation.QuantityUnit.OriginalText,
                    "标准化单位：" + relation.QuantityUnit.CanonicalExpression,
                    "单位族：" + quantityFamily,
                    relation.HasExplicitBinding ? MechanicalEvidenceStrength.HardDeterministic : MechanicalEvidenceStrength.ContextDependent,
                    relation.HasExplicitBinding ? "关系依据=数量/单价标签或唯一表头列绑定" : "关系依据=同行共现，尚未证明数量/单价关系"),
                MechanicalCheckerSupport.Occurrence(
                    context,
                    relation.PriceElement,
                    relation.PriceElement.Text,
                    "单价单位原文：" + relation.UnitPrice.OriginalText,
                    "标准化单位：" + relation.UnitPrice.CanonicalExpression,
                    "单价分母单位族：" + denominatorFamily)
            };

            return new MechanicalIssue(
                nameof(UnitConsistencyChecker),
                MechanicalIssueKind.UnitConsistency,
                MechanicalIssueSeverity.Normal,
                "单位一致性",
                "数量单位与单价分母不一致",
                "数量单位属于“" + quantityFamily + "”，单价分母属于“" +
                    denominatorFamily + "”，两者量纲不一致。",
                "请人工核对原单位。",
                occurrences);
        }

        private static string FamilyDisplay(UnitFamily family)
        {
            switch (family)
            {
                case UnitFamily.Count: return "件数";
                case UnitFamily.Mass: return "质量";
                case UnitFamily.Length: return "长度";
                case UnitFamily.Area: return "面积";
                case UnitFamily.Volume: return "体积";
                case UnitFamily.Time: return "时间";
                case UnitFamily.Currency: return "货币";
                case UnitFamily.Percentage: return "比例";
                default: return "未知";
            }
        }

        private sealed class Candidate
        {
            public Candidate(UnitExpression expression, DocumentElement element)
            {
                Expression = expression;
                Element = element;
            }

            public UnitExpression Expression { get; }
            public DocumentElement Element { get; }
        }

        private sealed class HeaderLayout
        {
            public HeaderLayout(int rowIndex, int quantityColumn, int priceColumn, int? unitColumn)
            {
                RowIndex = rowIndex;
                QuantityColumn = quantityColumn;
                PriceColumn = priceColumn;
                UnitColumn = unitColumn;
            }

            public int RowIndex { get; }
            public int QuantityColumn { get; }
            public int PriceColumn { get; }
            public int? UnitColumn { get; }
        }
    }

    internal static class UnitEnumerableExtensions
    {
        public static T SingleOrDefaultOrNull<T>(
            this IEnumerable<T> source,
            Func<T, bool> predicate)
            where T : class
        {
            List<T> values = source.Where(predicate).Take(2).ToList();
            return values.Count == 1 ? values[0] : null;
        }
    }
}
