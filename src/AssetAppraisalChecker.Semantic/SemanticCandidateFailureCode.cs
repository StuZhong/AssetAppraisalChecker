namespace AssetAppraisalChecker.Semantic
{
    public enum SemanticCandidateFailureCode
    {
        None = 0,
        Timeout = 1,
        ModelLoadFailure = 2,
        InvalidOutput = 3,
        LowConfidence = 4,
        MultipleCandidates = 5,
        UnsupportedText = 6
    }
}
