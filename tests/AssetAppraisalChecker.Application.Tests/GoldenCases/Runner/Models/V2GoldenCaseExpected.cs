using System;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    public sealed class V2GoldenCaseExpected
    {
        public V2GoldenCaseExpected()
        {
            SchemaVersion = string.Empty;
            CaseId = string.Empty;
            ExpectedFacts = Array.Empty<V2GoldenCaseFactGroupAssertion>();
            Semantic = new V2GoldenCaseSemanticExpectation();
            ExpectedExecutions = Array.Empty<V2GoldenCaseExecutionAssertion>();
            ExpectedAnalysis = new V2GoldenCaseAnalysisAssertion();
        }

        public string SchemaVersion { get; set; }

        public string CaseId { get; set; }

        public int ExpectedSemanticCallCount { get; set; }

        public V2GoldenCaseFactGroupAssertion[] ExpectedFacts { get; set; }

        public V2GoldenCaseSemanticExpectation Semantic { get; set; }

        public V2GoldenCaseExecutionAssertion[] ExpectedExecutions { get; set; }

        public V2GoldenCaseAnalysisAssertion ExpectedAnalysis { get; set; }
    }
}
