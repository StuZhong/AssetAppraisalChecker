namespace AssetAppraisalChecker.Facts.Models.Conclusions
{
    public enum WordConclusionDocumentKind { Report, Explanation }
    public enum WordConclusionTarget { ReportSummary, ReportChapterTen, ExplanationChapterFour }
    public enum WordConclusionSourceKind { BodyArabic, BodyChineseFinancial, TableAssetRow, TableTotalRow }
    public enum WordConclusionExtractionStatus
    {
        Success, InvalidInput, DocumentReadFailed, TargetNotFound, AmbiguousTarget,
        IncompleteAmounts, AmbiguousAmounts, UnsupportedTableStructure
    }
}
