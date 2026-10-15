namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class StyleMetadataSnapshot
    {
        public StyleMetadataSnapshot(
            string styleId,
            string styleType,
            string basedOnStyleId,
            string name,
            bool isDefault)
        {
            StyleId = styleId ?? string.Empty;
            StyleType = styleType ?? string.Empty;
            BasedOnStyleId = basedOnStyleId ?? string.Empty;
            Name = name ?? string.Empty;
            IsDefault = isDefault;
        }

        public string StyleId { get; }
        public string StyleType { get; }
        public string BasedOnStyleId { get; }
        public string Name { get; }
        public bool IsDefault { get; }
    }
}
