namespace AssetAppraisalChecker.Desktop.Workflow
{
    /// <summary>
    /// Explicit input for one local report check. ExpectedProjectName is supplied
    /// only by the caller and is never inferred from document content or state.
    /// </summary>
    public sealed class ReportCheckRequest
    {
        public ReportCheckRequest(string filePath, string expectedProjectName)
        {
            FilePath = filePath;
            ExpectedProjectName = expectedProjectName;
        }

        public string FilePath { get; }

        public string ExpectedProjectName { get; }
    }
}
