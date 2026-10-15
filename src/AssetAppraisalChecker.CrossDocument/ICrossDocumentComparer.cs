using System.Collections.Generic;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public interface ICrossDocumentComparer
    {
        IReadOnlyList<ConsistencyResult> Compare(
            IEnumerable<BusinessFact> firstDocumentFacts,
            IEnumerable<BusinessFact> secondDocumentFacts);
    }
}
