namespace AssetAppraisalChecker.Semantic
{
    public interface ISemanticAnalyzer
    {
        SemanticAnalysisResult Analyze(string textContent);
    }
}
