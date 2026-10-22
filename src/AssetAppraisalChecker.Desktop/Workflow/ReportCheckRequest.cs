namespace AssetAppraisalChecker.Desktop.Workflow
{
    public enum ReportCheckMode
    {
        WordDocuments = 0,
        WordExcel = 1
    }

    /// <summary>
    /// Explicit input for Word document checks or the three-role Word–Excel mode. ExpectedProjectName is
    /// an optional authority supplied by the caller and is not bound to either
    /// document identity.
    /// </summary>
    public sealed class ReportCheckRequest
    {
        public ReportCheckRequest(string filePath, string expectedProjectName)
            : this(filePath, null, expectedProjectName)
        {
        }

        public ReportCheckRequest(
            string documentAPath,
            string documentBPath,
            string expectedProjectName,
            ReportCheckMode mode = ReportCheckMode.WordDocuments,
            string excelPath = null)
        {
            DocumentAPath = documentAPath;
            DocumentBPath = documentBPath;
            ExpectedProjectName = expectedProjectName;
            Mode = mode;
            ExcelPath = excelPath;
        }

        public string DocumentAPath { get; }

        public string DocumentBPath { get; }

        public bool HasDocumentB => !string.IsNullOrWhiteSpace(DocumentBPath);

        public string FilePath => DocumentAPath;

        public string ExpectedProjectName { get; }

        public ReportCheckMode Mode { get; }

        public string ExcelPath { get; }
    }
}
