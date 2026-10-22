namespace AssetAppraisalChecker.Document.Excel
{
    public enum ExcelSummaryReadStatus
    {
        Success,
        InvalidPath,
        FileNotFound,
        UnsupportedFileType,
        InvalidWorkbook,
        AccessDenied,
        FileInUse,
        IoError,
        MissingWorksheet,
        MissingRequiredField,
        AmbiguousLayout,
        UnsupportedAssetStructure,
        InvalidAmount,
        FormulaResultUnavailable,
        UnitNotSupported,
        UnexpectedError
    }
}
