namespace AssetAppraisalChecker.Semantic
{
    public enum SemanticCandidateContractValidationReasonCode
    {
        Valid = 1,
        NullAnalysis = 2,
        InvalidOutput = 3,
        UnsupportedCandidateType = 4,
        IdentityMismatch = 5,
        MissingModelMetadata = 6,
        InvalidModelHash = 7,
        InvalidEvidence = 8,
        InvalidScore = 9,
        DuplicateCandidateIdentity = 10,
        InvalidFailureState = 11
    }
}
