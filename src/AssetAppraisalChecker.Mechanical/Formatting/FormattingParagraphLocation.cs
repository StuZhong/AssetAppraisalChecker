using System;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class FormattingParagraphLocation
    {
        public FormattingParagraphLocation(
            int paragraphIndex,
            int? bodyParagraphIndex,
            int? tableIndex,
            int? rowIndex,
            int? cellIndex,
            int? cellParagraphIndex)
        {
            if (paragraphIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
            }

            ParagraphIndex = paragraphIndex;
            BodyParagraphIndex = bodyParagraphIndex;
            TableIndex = tableIndex;
            RowIndex = rowIndex;
            CellIndex = cellIndex;
            CellParagraphIndex = cellParagraphIndex;
        }

        public int ParagraphIndex { get; }
        public int? BodyParagraphIndex { get; }
        public int? TableIndex { get; }
        public int? RowIndex { get; }
        public int? CellIndex { get; }
        public int? CellParagraphIndex { get; }
        public bool IsInTable => TableIndex.HasValue;
    }
}
