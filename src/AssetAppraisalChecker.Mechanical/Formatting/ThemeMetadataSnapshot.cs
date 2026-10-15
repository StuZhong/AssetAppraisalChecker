namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class ThemeMetadataSnapshot
    {
        public ThemeMetadataSnapshot(
            bool isPresent,
            string majorLatin,
            string majorEastAsia,
            string majorComplexScript,
            string minorLatin,
            string minorEastAsia,
            string minorComplexScript)
        {
            IsPresent = isPresent;
            MajorLatin = majorLatin ?? string.Empty;
            MajorEastAsia = majorEastAsia ?? string.Empty;
            MajorComplexScript = majorComplexScript ?? string.Empty;
            MinorLatin = minorLatin ?? string.Empty;
            MinorEastAsia = minorEastAsia ?? string.Empty;
            MinorComplexScript = minorComplexScript ?? string.Empty;
        }

        public bool IsPresent { get; }
        public string MajorLatin { get; }
        public string MajorEastAsia { get; }
        public string MajorComplexScript { get; }
        public string MinorLatin { get; }
        public string MinorEastAsia { get; }
        public string MinorComplexScript { get; }
    }
}
