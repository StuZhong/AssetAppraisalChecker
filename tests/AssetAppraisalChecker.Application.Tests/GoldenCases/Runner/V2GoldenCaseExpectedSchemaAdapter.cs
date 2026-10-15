using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    internal static class V2GoldenCaseExpectedSchemaAdapter
    {
        public static V2GoldenCaseExpected FromV1(
            V2GoldenCaseExpectedV1Schema source)
        {
            V2GoldenCaseConsistencyResultAssertion[] results =
                source.ExpectedAnalysis.Results
                    .Select(ToCanonical)
                    .ToArray();

            return new V2GoldenCaseExpected
            {
                SchemaVersion = source.SchemaVersion,
                CaseId = source.CaseId,
                ExpectedSemanticCallCount = source.ExpectedSemanticCallCount,
                ExpectedFacts = source.ExpectedFacts,
                Semantic = source.Semantic,
                ExpectedExecutions = source.ExpectedExecutions
                    .Select(execution => ToCanonical(execution, results))
                    .ToArray(),
                ExpectedAnalysis = new V2GoldenCaseAnalysisAssertion
                {
                    StatusSummary = source.ExpectedAnalysis.StatusSummary,
                    ExpectedResultCount =
                        source.ExpectedAnalysis.ExpectedResultCount,
                    Results = results
                }
            };
        }

        public static V2GoldenCaseExpected FromV2(
            V2GoldenCaseExpectedV2Schema source)
        {
            var executions = new List<V2GoldenCaseExecutionAssertion>();
            var allResults =
                new List<V2GoldenCaseConsistencyResultAssertion>();
            int resultOrder = 1;

            for (int executionIndex = 0;
                executionIndex < source.ExpectedExecutions.Length;
                executionIndex++)
            {
                V2GoldenCaseExecutionAssertionV2Schema sourceExecution =
                    source.ExpectedExecutions[executionIndex];
                V2GoldenCaseConsistencyResultAssertion[] results =
                    sourceExecution.Results
                        .Select(result => ToCanonical(result, resultOrder++))
                        .ToArray();
                allResults.AddRange(results);

                executions.Add(new V2GoldenCaseExecutionAssertion
                {
                    Order = executionIndex + 1,
                    CheckerName = sourceExecution.CheckerName,
                    FactType = sourceExecution.FactType,
                    ExecutionStatus = sourceExecution.ExecutionStatus,
                    HasConsistencyResult = results.Length == 1,
                    ExpectedResultCount =
                        sourceExecution.ExpectedResultCount,
                    ErrorInformation = sourceExecution.ErrorInformation,
                    NoResultReason = sourceExecution.NoResultReason,
                    RequiredEvidence = Array.Empty<string>(),
                    ForbiddenEvidence = Array.Empty<string>(),
                    RequiredExecutionEvidence =
                        sourceExecution.RequiredExecutionEvidence,
                    ForbiddenExecutionEvidence =
                        sourceExecution.ForbiddenExecutionEvidence,
                    Results = results
                });
            }

            return new V2GoldenCaseExpected
            {
                SchemaVersion = source.SchemaVersion,
                CaseId = source.CaseId,
                ExpectedSemanticCallCount = source.ExpectedSemanticCallCount,
                ExpectedFacts = source.ExpectedFacts,
                Semantic = source.Semantic,
                ExpectedExecutions = executions.ToArray(),
                ExpectedAnalysis = new V2GoldenCaseAnalysisAssertion
                {
                    StatusSummary = source.ExpectedAnalysis.StatusSummary,
                    ExpectedResultCount =
                        source.ExpectedAnalysis.ExpectedResultCount,
                    Results = allResults.ToArray()
                }
            };
        }

        private static V2GoldenCaseExecutionAssertion ToCanonical(
            V2GoldenCaseExecutionAssertionV1Schema source,
            IEnumerable<V2GoldenCaseConsistencyResultAssertion> allResults)
        {
            V2GoldenCaseConsistencyResultAssertion[] results =
                source.HasConsistencyResult
                    ? allResults
                        .Where(result => string.Equals(
                            result.FactType,
                            source.FactType,
                            StringComparison.Ordinal))
                        .ToArray()
                    : new V2GoldenCaseConsistencyResultAssertion[0];

            return new V2GoldenCaseExecutionAssertion
            {
                Order = source.Order,
                CheckerName = source.CheckerName,
                FactType = source.FactType,
                ExecutionStatus = source.ExecutionStatus,
                HasConsistencyResult = source.HasConsistencyResult,
                ExpectedResultCount = source.HasConsistencyResult ? 1 : 0,
                ErrorInformation = source.ErrorInformation,
                RequiredEvidence =
                    source.RequiredEvidence ?? Array.Empty<string>(),
                ForbiddenEvidence =
                    source.ForbiddenEvidence ?? Array.Empty<string>(),
                RequiredExecutionEvidence = Array.Empty<string>(),
                ForbiddenExecutionEvidence = Array.Empty<string>(),
                Results = results
            };
        }

        private static V2GoldenCaseConsistencyResultAssertion ToCanonical(
            V2GoldenCaseConsistencyResultAssertionV1Schema source)
        {
            FactType factType = ParseFactType(source.FactType);
            return new V2GoldenCaseConsistencyResultAssertion
            {
                Order = source.Order,
                FactType = source.FactType,
                Identity = ToAssertion(
                    ConsistencyResultIdentity.CreateDefault(factType)),
                Status = source.Status,
                ReasonContains = source.ReasonContains,
                RequiredEvidence =
                    source.RequiredEvidence ?? Array.Empty<string>(),
                ForbiddenEvidence =
                    source.ForbiddenEvidence ?? Array.Empty<string>()
            };
        }

        private static V2GoldenCaseConsistencyResultAssertion ToCanonical(
            V2GoldenCaseConsistencyResultAssertionV2Schema source,
            int order)
        {
            return new V2GoldenCaseConsistencyResultAssertion
            {
                Order = order,
                FactType = source.Identity == null
                    ? string.Empty
                    : source.Identity.FactType,
                Identity = source.Identity,
                Status = source.Status,
                ReasonContains = source.ReasonContains,
                RequiredEvidence = source.RequiredEvidence,
                ForbiddenEvidence = source.ForbiddenEvidence
            };
        }

        private static V2GoldenCaseResultIdentityAssertion ToAssertion(
            ConsistencyResultIdentity identity)
        {
            return new V2GoldenCaseResultIdentityAssertion
            {
                FactType = identity.FactType.ToString(),
                SubjectKey = identity.SubjectKey,
                ResultRole = identity.ResultRole,
                ScopeKey = identity.ScopeKey
            };
        }

        private static FactType ParseFactType(string value)
        {
            FactType parsed;
            return Enum.TryParse(value, false, out parsed)
                ? parsed
                : FactType.Unknown;
        }
    }
}
