using System.Collections.Generic;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public interface IV2AnalysisPipeline
    {
        V2AnalysisResult Analyze(
            IEnumerable<BusinessFact> firstDocumentFacts,
            IEnumerable<BusinessFact> secondDocumentFacts);
    }
}
