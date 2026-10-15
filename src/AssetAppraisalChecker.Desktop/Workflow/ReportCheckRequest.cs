namespace AssetAppraisalChecker.Desktop.Workflow
{
    /// <summary>
    /// Explicit input for one or two local document checks. ExpectedProjectName is
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
            string expectedProjectName)
        {
            DocumentAPath = documentAPath;
            DocumentBPath = documentBPath;
            ExpectedProjectName = expectedProjectName;
        }

        public string DocumentAPath { get; }

        public string DocumentBPath { get; }

        public bool HasDocumentB => !string.IsNullOrWhiteSpace(DocumentBPath);

        public string FilePath => DocumentAPath;

        public string ExpectedProjectName { get; }
    }
}
