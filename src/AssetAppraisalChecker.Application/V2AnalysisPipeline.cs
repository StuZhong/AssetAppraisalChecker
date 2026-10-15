using System;
using System.Collections.Generic;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public sealed class V2AnalysisPipeline : IV2AnalysisPipeline
    {
        private readonly ICrossDocumentComparer _crossDocumentComparer;

        public V2AnalysisPipeline(ICrossDocumentComparer crossDocumentComparer)
        {
            _crossDocumentComparer = crossDocumentComparer ??
                throw new ArgumentNullException(nameof(crossDocumentComparer));
        }

        public V2AnalysisResult Analyze(
            IEnumerable<BusinessFact> firstDocumentFacts,
            IEnumerable<BusinessFact> secondDocumentFacts)
        {
            IReadOnlyList<ConsistencyResult> results = _crossDocumentComparer.Compare(
                firstDocumentFacts,
                secondDocumentFacts);

            return new V2AnalysisResult(results);
        }
    }
}
