namespace AssetAppraisalChecker.Semantic
{
    public interface ISemanticCandidateRanker
    {
        SemanticCandidateRankingResult Rank(
            SemanticCandidateRequest request,
            SemanticCandidateSelectionContext selectionContext);
    }
}
