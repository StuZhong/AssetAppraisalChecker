namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class TableFormattingContext
    {
        public TableFormattingContext(
            int tableIndex,
            string tableStyleId,
            FormattingResolutionStatus conditionalTableStyleResolution)
        {
            TableIndex = tableIndex;
            TableStyleId = tableStyleId ?? string.Empty;
            ConditionalTableStyleResolution = conditionalTableStyleResolution;
        }

        public int TableIndex { get; }
        public string TableStyleId { get; }
        public FormattingResolutionStatus ConditionalTableStyleResolution { get; }
    }
}
