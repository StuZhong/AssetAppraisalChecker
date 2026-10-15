using System;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    internal sealed class V2GoldenCaseExpectedV1Schema
    {
        public string SchemaVersion { get; set; }
        public string CaseId { get; set; }
        public int ExpectedSemanticCallCount { get; set; }
        public V2GoldenCaseFactGroupAssertion[] ExpectedFacts { get; set; }
        public V2GoldenCaseSemanticExpectation Semantic { get; set; }
        public V2GoldenCaseExecutionAssertionV1Schema[] ExpectedExecutions { get; set; }
        public V2GoldenCaseAnalysisAssertionV1Schema ExpectedAnalysis { get; set; }
    }

    internal sealed class V2GoldenCaseExecutionAssertionV1Schema
    {
        public int Order { get; set; }
        public string CheckerName { get; set; }
        public string FactType { get; set; }
        public string ExecutionStatus { get; set; }
        public bool HasConsistencyResult { get; set; }
        public string ErrorInformation { get; set; }
        public string[] RequiredEvidence { get; set; }
        public string[] ForbiddenEvidence { get; set; }
    }

    internal sealed class V2GoldenCaseAnalysisAssertionV1Schema
    {
        public string StatusSummary { get; set; }
        public int ExpectedResultCount { get; set; }
        public V2GoldenCaseConsistencyResultAssertionV1Schema[] Results { get; set; }
    }

    internal sealed class V2GoldenCaseConsistencyResultAssertionV1Schema
    {
        public int Order { get; set; }
        public string FactType { get; set; }
        public string Status { get; set; }
        public string ReasonContains { get; set; }
        public string[] RequiredEvidence { get; set; }
        public string[] ForbiddenEvidence { get; set; }
    }

    internal sealed class V2GoldenCaseExpectedV2Schema
    {
        public string SchemaVersion { get; set; }
        public string CaseId { get; set; }
        public int ExpectedSemanticCallCount { get; set; }
        public V2GoldenCaseFactGroupAssertion[] ExpectedFacts { get; set; }
        public V2GoldenCaseSemanticExpectation Semantic { get; set; }
        public V2GoldenCaseExecutionAssertionV2Schema[] ExpectedExecutions { get; set; }
        public V2GoldenCaseAnalysisAssertionV2Schema ExpectedAnalysis { get; set; }
    }

    internal sealed class V2GoldenCaseExecutionAssertionV2Schema
    {
        public string CheckerName { get; set; }
        public string FactType { get; set; }
        public string ExecutionStatus { get; set; }
        public int ExpectedResultCount { get; set; }
        public string ErrorInformation { get; set; }
        public string NoResultReason { get; set; }
        public string[] RequiredExecutionEvidence { get; set; }
        public string[] ForbiddenExecutionEvidence { get; set; }
        public V2GoldenCaseConsistencyResultAssertionV2Schema[] Results { get; set; }
    }

    internal sealed class V2GoldenCaseAnalysisAssertionV2Schema
    {
        public string StatusSummary { get; set; }
        public int ExpectedResultCount { get; set; }
    }

    internal sealed class V2GoldenCaseConsistencyResultAssertionV2Schema
    {
        public V2GoldenCaseResultIdentityAssertion Identity { get; set; }
        public string Status { get; set; }
        public string ReasonContains { get; set; }
        public string[] RequiredEvidence { get; set; }
        public string[] ForbiddenEvidence { get; set; }
    }
}
