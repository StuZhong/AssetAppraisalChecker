using System.Collections.Generic;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public interface IV2GoldenFactAssertor
    {
        FactType FactType { get; }

        IReadOnlyList<V2GoldenCaseFactSnapshot> Extract(
            DocumentReadResult document,
            string sourceDocument);
    }
}
