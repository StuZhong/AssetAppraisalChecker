using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.Documents
{
    /// <summary>
    /// Describes a structural location in a document. All indexes are zero-based.
    /// A dimension that does not apply to the element is represented by null.
    /// </summary>
    public sealed class DocumentLocation
    {
        public DocumentLocation(
            DocumentPart documentPart,
            int? paragraphIndex,
            int? tableIndex,
            int? rowIndex,
            int? cellIndex,
            IEnumerable<string> headingPath)
            : this(
                documentPart,
                paragraphIndex,
                tableIndex,
                rowIndex,
                cellIndex,
                headingPath,
                null,
                null,
                null)
        {
        }

        public DocumentLocation(
            DocumentPart documentPart,
            int? paragraphIndex,
            int? tableIndex,
            int? rowIndex,
            int? cellIndex,
            IEnumerable<string> headingPath,
            int? tableGridColumnCount,
            int? logicalGridStart,
            int? logicalGridSpan)
        {
            ValidateIndex(paragraphIndex, nameof(paragraphIndex));
            ValidateIndex(tableIndex, nameof(tableIndex));
            ValidateIndex(rowIndex, nameof(rowIndex));
            ValidateIndex(cellIndex, nameof(cellIndex));
            ValidateIndex(tableGridColumnCount, nameof(tableGridColumnCount));
            ValidateIndex(logicalGridStart, nameof(logicalGridStart));
            if (logicalGridSpan.HasValue && logicalGridSpan.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(logicalGridSpan),
                    "逻辑网格跨度必须为正数。");
            }

            DocumentPart = documentPart;
            ParagraphIndex = paragraphIndex;
            TableIndex = tableIndex;
            RowIndex = rowIndex;
            CellIndex = cellIndex;
            TableGridColumnCount = tableGridColumnCount;
            LogicalGridStart = logicalGridStart;
            LogicalGridSpan = logicalGridSpan;
            HeadingPath = new ReadOnlyCollection<string>(
                (headingPath ?? Enumerable.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList());
        }

        public DocumentPart DocumentPart { get; }

        public int? ParagraphIndex { get; }

        public int? TableIndex { get; }

        public int? RowIndex { get; }

        public int? CellIndex { get; }

        /// <summary>
        /// Declared number of logical columns in the containing Word table grid.
        /// Null means that no reliable TableGrid was available.
        /// </summary>
        public int? TableGridColumnCount { get; }

        /// <summary>
        /// Zero-based logical grid start. This is deliberately separate from the
        /// physical cell index because preceding cells may use GridSpan.
        /// </summary>
        public int? LogicalGridStart { get; }

        public int? LogicalGridSpan { get; }

        public int? LogicalGridEnd =>
            LogicalGridStart.HasValue && LogicalGridSpan.HasValue
                ? LogicalGridStart.Value + LogicalGridSpan.Value - 1
                : (int?)null;

        public IReadOnlyList<string> HeadingPath { get; }

        private static void ValidateIndex(int? index, string parameterName)
        {
            if (index.HasValue && index.Value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "文档结构索引不能为负数。");
            }
        }
    }
}
