using System;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    public sealed class V2GoldenCaseExecutionAssertion
    {
        public V2GoldenCaseExecutionAssertion()
        {
            CheckerName = string.Empty;
            FactType = string.Empty;
            ExecutionStatus = string.Empty;
            ErrorInformation = string.Empty;
            NoResultReason = string.Empty;
            RequiredEvidence = Array.Empty<string>();
            ForbiddenEvidence = Array.Empty<string>();
            RequiredExecutionEvidence = Array.Empty<string>();
            ForbiddenExecutionEvidence = Array.Empty<string>();
            Results = Array.Empty<V2GoldenCaseConsistencyResultAssertion>();
        }

        public int Order { get; set; }

        public string CheckerName { get; set; }

        public string FactType { get; set; }

        public string ExecutionStatus { get; set; }

        public bool HasConsistencyResult { get; set; }

        public int ExpectedResultCount { get; set; }

        public string ErrorInformation { get; set; }

        public string NoResultReason { get; set; }

        public string[] RequiredEvidence { get; set; }

        public string[] ForbiddenEvidence { get; set; }

        public string[] RequiredExecutionEvidence { get; set; }

        public string[] ForbiddenExecutionEvidence { get; set; }

        public V2GoldenCaseConsistencyResultAssertion[] Results { get; set; }
    }

    public sealed class V2GoldenCaseAnalysisAssertion
    {
        public V2GoldenCaseAnalysisAssertion()
        {
            StatusSummary = string.Empty;
            Results = Array.Empty<V2GoldenCaseConsistencyResultAssertion>();
        }

        public string StatusSummary { get; set; }

        public int ExpectedResultCount { get; set; }

        public V2GoldenCaseConsistencyResultAssertion[] Results { get; set; }
    }

    public sealed class V2GoldenCaseConsistencyResultAssertion
    {
        public V2GoldenCaseConsistencyResultAssertion()
        {
            FactType = string.Empty;
            Status = string.Empty;
            ReasonContains = string.Empty;
            RequiredEvidence = Array.Empty<string>();
            ForbiddenEvidence = Array.Empty<string>();
            Identity = new V2GoldenCaseResultIdentityAssertion();
        }

        public int Order { get; set; }

        public V2GoldenCaseResultIdentityAssertion Identity { get; set; }

        public string FactType { get; set; }

        public string Status { get; set; }

        public string ReasonContains { get; set; }

        public string[] RequiredEvidence { get; set; }

        public string[] ForbiddenEvidence { get; set; }
    }

    public sealed class V2GoldenCaseResultIdentityAssertion
    {
        public V2GoldenCaseResultIdentityAssertion()
        {
            FactType = string.Empty;
            SubjectKey = string.Empty;
            ResultRole = string.Empty;
            ScopeKey = string.Empty;
        }

        public string FactType { get; set; }

        public string SubjectKey { get; set; }

        public string ResultRole { get; set; }

        public string ScopeKey { get; set; }
    }
}
