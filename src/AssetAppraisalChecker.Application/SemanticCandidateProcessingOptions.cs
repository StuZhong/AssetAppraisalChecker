namespace AssetAppraisalChecker.Application
{
    public sealed class SemanticCandidateProcessingOptions
    {
        public SemanticCandidateProcessingOptions(bool shadowOnly)
        {
            ShadowOnly = shadowOnly;
        }

        public bool ShadowOnly { get; }

        public static SemanticCandidateProcessingOptions Shadow { get; } =
            new SemanticCandidateProcessingOptions(true);

        public static SemanticCandidateProcessingOptions Active { get; } =
            new SemanticCandidateProcessingOptions(false);
    }
}
