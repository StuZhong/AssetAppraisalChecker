namespace AssetAppraisalChecker.Application
{
    public interface IV2DocumentAnalysisPipeline
    {
        V2AnalysisResult Analyze(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument);
    }
}
