using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    /// <summary>
    /// Resolves only the audited two-level pattern: two appraisal-value parent
    /// headers (tax excluded/included), each spanning Original and Net child cells.
    /// Any incomplete or competing structure fails closed instead of falling back
    /// to physical-cell proximity.
    /// </summary>
    internal sealed class MergedHeaderColumnSemanticResolver
    {
        private readonly Resolution _resolution;

        public MergedHeaderColumnSemanticResolver(IEnumerable<DocumentElement> tableCells)
        {
            _resolution = BuildResolution((tableCells ?? Enumerable.Empty<DocumentElement>()).ToList());
        }

        public bool SuppressPhysicalHeaderFallback =>
            _resolution.Kind == ResolutionKind.Ambiguous;

        public bool TryResolve(
            DocumentElement element,
            out MergedHeaderColumnSemantic semantic)
        {
            semantic = null;
            if (_resolution.Kind != ResolutionKind.Resolved ||
                !element.Location.RowIndex.HasValue ||
                element.Location.RowIndex.Value <= _resolution.ChildHeaderRow ||
                element.Location.LogicalGridSpan != 1 ||
                !element.Location.LogicalGridStart.HasValue)
            {
                return false;
            }

            return _resolution.Semantics.TryGetValue(
                element.Location.LogicalGridStart.Value,
                out semantic);
        }

        private static Resolution BuildResolution(IReadOnlyList<DocumentElement> cells)
        {
            List<DocumentElement> appraisalParents = cells
                .Where(IsMergedAppraisalParentCandidate)
                .ToList();
            if (appraisalParents.Count == 0)
            {
                return Resolution.NotApplicable;
            }

            if (appraisalParents.Any(parent =>
                    !parent.Location.LogicalGridStart.HasValue ||
                    parent.Location.LogicalGridSpan != 2 ||
                    !parent.Location.LogicalGridEnd.HasValue ||
                    !parent.Location.TableGridColumnCount.HasValue ||
                    parent.Location.LogicalGridEnd.Value >=
                        parent.Location.TableGridColumnCount.Value))
            {
                return Resolution.Ambiguous;
            }

            List<int> parentRows = appraisalParents
                .Where(parent => parent.Location.RowIndex.HasValue)
                .Select(parent => parent.Location.RowIndex.Value)
                .Distinct()
                .ToList();
            if (appraisalParents.Count != 2 || parentRows.Count != 1)
            {
                return Resolution.Ambiguous;
            }

            ParentSemantic excluded;
            ParentSemantic included;
            if (!TryCreateUniqueParent(appraisalParents, TaxBasis.TaxExcluded, out excluded) ||
                !TryCreateUniqueParent(appraisalParents, TaxBasis.TaxIncluded, out included) ||
                RangesOverlap(excluded, included))
            {
                return Resolution.Ambiguous;
            }

            int childRow = parentRows[0] + 1;
            var semantics = new Dictionary<int, MergedHeaderColumnSemantic>();
            if (!TryAddChildren(cells, childRow, excluded, semantics) ||
                !TryAddChildren(cells, childRow, included, semantics) ||
                semantics.Count != 4)
            {
                return Resolution.Ambiguous;
            }

            return Resolution.Resolved(childRow, semantics);
        }

        private static bool IsMergedAppraisalParentCandidate(DocumentElement cell)
        {
            if (cell.ElementType != DocumentElementType.TableCell)
            {
                return false;
            }

            ValueCategory category;
            string keyword;
            bool isAppraisedValue = BusinessKeywordMatcher.TryClassifyValue(
                       cell.Text,
                       out category,
                       out keyword) &&
                   category == ValueCategory.AppraisedValue;
            if (!isAppraisedValue)
            {
                return false;
            }

            return cell.Location.LogicalGridSpan.GetValueOrDefault() > 1 ||
                   (cell.Location.TableGridColumnCount.HasValue &&
                    !cell.Location.LogicalGridSpan.HasValue);
        }

        private static bool TryCreateUniqueParent(
            IEnumerable<DocumentElement> candidates,
            TaxBasis expectedTaxBasis,
            out ParentSemantic parent)
        {
            parent = null;
            List<DocumentElement> matches = candidates.Where(candidate =>
            {
                TaxBasis taxBasis;
                string keyword;
                return BusinessKeywordMatcher.TryClassifyTaxBasis(
                           candidate.Text,
                           out taxBasis,
                           out keyword) &&
                       taxBasis == expectedTaxBasis;
            }).ToList();
            if (matches.Count != 1)
            {
                return false;
            }

            DocumentElement match = matches[0];
            parent = new ParentSemantic(
                match.Location.LogicalGridStart.Value,
                match.Location.LogicalGridEnd.Value,
                expectedTaxBasis);
            return true;
        }

        private static bool TryAddChildren(
            IEnumerable<DocumentElement> cells,
            int childRow,
            ParentSemantic parent,
            IDictionary<int, MergedHeaderColumnSemantic> semantics)
        {
            DocumentElement original = FindUniqueChild(
                cells,
                childRow,
                parent.Start,
                "原值");
            DocumentElement net = FindUniqueChild(
                cells,
                childRow,
                parent.Start + 1,
                "净值");
            if (original == null || net == null || parent.End != parent.Start + 1)
            {
                return false;
            }

            if (semantics.ContainsKey(parent.Start) ||
                semantics.ContainsKey(parent.Start + 1))
            {
                return false;
            }

            semantics[parent.Start] = new MergedHeaderColumnSemantic(
                ValueCategory.AppraisedOriginalValue,
                parent.TaxBasis);
            semantics[parent.Start + 1] = new MergedHeaderColumnSemantic(
                ValueCategory.AppraisedNetValue,
                parent.TaxBasis);
            return true;
        }

        private static DocumentElement FindUniqueChild(
            IEnumerable<DocumentElement> cells,
            int rowIndex,
            int logicalGridIndex,
            string expectedText)
        {
            List<DocumentElement> matches = cells.Where(cell =>
                    cell.Location.RowIndex == rowIndex &&
                    cell.Location.LogicalGridStart == logicalGridIndex &&
                    cell.Location.LogicalGridSpan == 1 &&
                    string.Equals(
                        RemoveWhitespace(cell.Text),
                        expectedText,
                        StringComparison.Ordinal))
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        private static string RemoveWhitespace(string text)
        {
            return new string((text ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character))
                .ToArray());
        }

        private static bool RangesOverlap(ParentSemantic left, ParentSemantic right)
        {
            return left.Start <= right.End && right.Start <= left.End;
        }

        private enum ResolutionKind
        {
            NotApplicable,
            Resolved,
            Ambiguous
        }

        private sealed class Resolution
        {
            public static readonly Resolution NotApplicable = new Resolution(
                ResolutionKind.NotApplicable,
                -1,
                new Dictionary<int, MergedHeaderColumnSemantic>());

            public static readonly Resolution Ambiguous = new Resolution(
                ResolutionKind.Ambiguous,
                -1,
                new Dictionary<int, MergedHeaderColumnSemantic>());

            private Resolution(
                ResolutionKind kind,
                int childHeaderRow,
                IDictionary<int, MergedHeaderColumnSemantic> semantics)
            {
                Kind = kind;
                ChildHeaderRow = childHeaderRow;
                Semantics = new ReadOnlyDictionary<int, MergedHeaderColumnSemantic>(
                    new Dictionary<int, MergedHeaderColumnSemantic>(semantics));
            }

            public ResolutionKind Kind { get; }

            public int ChildHeaderRow { get; }

            public IReadOnlyDictionary<int, MergedHeaderColumnSemantic> Semantics { get; }

            public static Resolution Resolved(
                int childHeaderRow,
                IDictionary<int, MergedHeaderColumnSemantic> semantics)
            {
                return new Resolution(ResolutionKind.Resolved, childHeaderRow, semantics);
            }
        }

        private sealed class ParentSemantic
        {
            public ParentSemantic(int start, int end, TaxBasis taxBasis)
            {
                Start = start;
                End = end;
                TaxBasis = taxBasis;
            }

            public int Start { get; }

            public int End { get; }

            public TaxBasis TaxBasis { get; }
        }
    }
}
