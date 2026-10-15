using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public interface IFactChecker
    {
        string CheckerName { get; }

        FactType FactType { get; }

        FactCheckExecutionEnvelope Execute(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument);
    }
}
