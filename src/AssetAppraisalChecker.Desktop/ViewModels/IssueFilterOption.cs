namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public sealed class IssueFilterOption
    {
        public IssueFilterOption(IssueSeverityFilter filter, string displayName)
        {
            Filter = filter;
            DisplayName = displayName;
        }

        public IssueSeverityFilter Filter { get; }

        public string DisplayName { get; }
    }
}
