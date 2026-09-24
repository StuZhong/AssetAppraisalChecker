using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal sealed class TableBusinessContext
    {
        public static readonly TableBusinessContext Empty = new TableBusinessContext(
            string.Empty,
            string.Empty,
            MoneyUnit.Unknown,
            SubjectResolution.Unknown,
            null,
            false);

        public TableBusinessContext(
            string columnHeaderText,
            string rowLabelText,
            MoneyUnit inheritedUnit,
            SubjectResolution subject,
            MergedHeaderColumnSemantic mergedHeaderSemantic,
            bool isMergedHeaderProjectAggregateRow)
        {
            ColumnHeaderText = columnHeaderText ?? string.Empty;
            RowLabelText = rowLabelText ?? string.Empty;
            InheritedUnit = inheritedUnit;
            Subject = subject ?? SubjectResolution.Unknown;
            MergedHeaderSemantic = mergedHeaderSemantic;
            IsMergedHeaderProjectAggregateRow = isMergedHeaderProjectAggregateRow;
        }

        public string ColumnHeaderText { get; }

        public string RowLabelText { get; }

        public MoneyUnit InheritedUnit { get; }

        public SubjectResolution Subject { get; }

        public MergedHeaderColumnSemantic MergedHeaderSemantic { get; }

        public bool IsMergedHeaderProjectAggregateRow { get; }

        public bool HasExplicitMoneyFieldLabel
        {
            get
            {
                ValueCategory category;
                string keyword;
                return MergedHeaderSemantic != null ||
                       BusinessKeywordMatcher.TryClassifyValue(ColumnHeaderText, out category, out keyword) ||
                       BusinessKeywordMatcher.TryClassifyValue(RowLabelText, out category, out keyword);
            }
        }
    }
}
