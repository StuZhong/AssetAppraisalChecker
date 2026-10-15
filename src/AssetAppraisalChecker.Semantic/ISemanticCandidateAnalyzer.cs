namespace AssetAppraisalChecker.Semantic
{
    public interface ISemanticCandidateAnalyzer
    {
        SemanticCandidateAnalysis Analyze(SemanticCandidateRequest request);
    }
}
