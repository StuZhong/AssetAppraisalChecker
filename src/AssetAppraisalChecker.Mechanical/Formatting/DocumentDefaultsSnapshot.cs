namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class DocumentDefaultsSnapshot
    {
        public DocumentDefaultsSnapshot(bool hasRunDefaults, bool hasParagraphDefaults)
        {
            HasRunDefaults = hasRunDefaults;
            HasParagraphDefaults = hasParagraphDefaults;
        }

        public bool HasRunDefaults { get; }
        public bool HasParagraphDefaults { get; }
    }
}
