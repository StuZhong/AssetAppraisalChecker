namespace AssetAppraisalChecker.Semantic
{
    public enum SemanticCandidateRankingAcceptanceReasonCode
    {
        AcceptedForCandidatePriority = 1,
        ShadowOnly = 2,
        NoValidCandidate = 3,
        Ambiguous = 4,
        RankingFailed = 5,
        RankingAbstained = 6,
        SubjectUnresolved = 7,
        SubjectConflict = 8,
        NegationDetected = 9,
        HardRuleConflict = 10,
        HardRuleProtectedType = 11,
        EvidenceInsufficient = 12,
        ContractInvalid = 13
    }
}
