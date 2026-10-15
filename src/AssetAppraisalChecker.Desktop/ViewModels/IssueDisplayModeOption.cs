namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public sealed class IssueDisplayModeOption
    {
        public IssueDisplayModeOption(IssueDisplayMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }

        public IssueDisplayMode Mode { get; }
        public string DisplayName { get; }
    }
}
