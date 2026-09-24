namespace AssetAppraisalChecker.Core.Documents
{
    /// <summary>
    /// Holds only the nearest effective elements so callers do not duplicate the full document text.
    /// </summary>
    public sealed class DocumentNearbyContext
    {
        public DocumentNearbyContext(string previousText, string currentText, string nextText)
        {
            PreviousText = previousText ?? string.Empty;
            CurrentText = currentText ?? string.Empty;
            NextText = nextText ?? string.Empty;
        }

        public string PreviousText { get; }

        public string CurrentText { get; }

        public string NextText { get; }
    }
}
