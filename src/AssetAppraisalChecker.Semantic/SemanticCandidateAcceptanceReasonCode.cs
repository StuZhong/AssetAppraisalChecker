namespace AssetAppraisalChecker.Semantic
{
    public enum SemanticCandidateAcceptanceReasonCode
    {
        Accepted = 1,
        ContractInvalid = 2,
        AnalyzerFailure = 3,
        NoCandidate = 4,
        SubjectUnresolved = 5,
        SubjectConflict = 6,
        NegationDetected = 7,
        HardRuleConflict = 8,
        HardRuleProtectedType = 9,
        LowModelConfidence = 10,
        SimilarityScoreMissing = 11,
        SimilarityThresholdNotMet = 12,
        MultipleCandidates = 13,
        EvidenceInsufficient = 14
    }
}
