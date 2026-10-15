using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.CrossDocument;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public sealed class V2GoldenCaseResultAssertor
    {
        public IReadOnlyList<V2GoldenCaseAssertionFailure> Assert(
            V2GoldenCaseExpected expected,
            V2AnalysisResult actual,
            IReadOnlyList<V2GoldenCaseSemanticCallRecord> semanticCalls)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            if (semanticCalls == null)
            {
                throw new ArgumentNullException(nameof(semanticCalls));
            }

            var failures = new List<V2GoldenCaseAssertionFailure>();
            AssertSemantic(expected, semanticCalls, failures);
            AssertExecutions(expected, actual, failures);
            AssertAnalysis(expected, actual, failures);
            AssertStructuralInvariants(actual, failures);
            return failures.AsReadOnly();
        }

        private static void AssertSemantic(
            V2GoldenCaseExpected expected,
            IReadOnlyList<V2GoldenCaseSemanticCallRecord> semanticCalls,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            AddMismatch(
                failures,
                "Semantic",
                expected.ExpectedSemanticCallCount.ToString(),
                semanticCalls.Count.ToString(),
                "call count");

            V2GoldenCaseSemanticCallAssertion[] expectedCalls =
                expected.Semantic.ExpectedCalls
                    .OrderBy(item => item.Order)
                    .ToArray();
            int count = Math.Min(expectedCalls.Length, semanticCalls.Count);
            for (int index = 0; index < count; index++)
            {
                string stage = "Semantic[" + expectedCalls[index].Order + "]";
                AddMismatch(failures, stage, expectedCalls[index].FieldName, semanticCalls[index].FieldName, "FieldName");
                AddMismatch(failures, stage, expectedCalls[index].OriginalValue, semanticCalls[index].OriginalValue, "OriginalValue");
                AddMismatch(failures, stage, expectedCalls[index].CandidateValue, semanticCalls[index].CandidateValue, "CandidateValue");
            }
        }

        private static void AssertExecutions(
            V2GoldenCaseExpected expected,
            V2AnalysisResult actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            AddMismatch(
                failures,
                "Execution",
                expected.ExpectedExecutions.Length.ToString(),
                actual.Executions.Count.ToString(),
                "execution count");

            AddDuplicateFailures(
                actual.Executions,
                ExecutionIdentity,
                "Execution",
                "CheckerName/FactType",
                failures);

            foreach (V2GoldenCaseExecutionAssertion expectedExecution
                in expected.ExpectedExecutions)
            {
                string identity = ExpectedExecutionIdentity(expectedExecution);
                FactCheckExecutionEnvelope[] matches = actual.Executions
                    .Where(item => string.Equals(
                        ExecutionIdentity(item),
                        identity,
                        StringComparison.Ordinal))
                    .ToArray();
                string stage = "Execution[" + identity + "]";
                if (matches.Length == 0)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Expected execution identity is missing."));
                    continue;
                }

                if (matches.Length > 1)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Expected execution identity is duplicated."));
                    continue;
                }

                FactCheckExecutionEnvelope execution = matches[0];
                AddMismatch(failures, stage, expectedExecution.ExecutionStatus, execution.ExecutionStatus.ToString(), "ExecutionStatus");
                AddMismatch(failures, stage, expectedExecution.ExpectedResultCount.ToString(), execution.Results.Count.ToString(), "ExpectedResultCount");
                AddMismatch(failures, stage, expectedExecution.ErrorInformation, execution.ErrorInformation, "ErrorInformation");
                if (string.Equals(
                    expected.SchemaVersion,
                    V2GoldenCaseJsonLoader.ExpectedSchemaVersionV2,
                    StringComparison.Ordinal))
                {
                    AddMismatch(failures, stage, expectedExecution.NoResultReason, execution.NoResultReason, "NoResultReason");
                }

                AssertEvidence(
                    failures,
                    stage + "/CompatibilityEvidence",
                    execution.Evidence,
                    expectedExecution.RequiredEvidence,
                    expectedExecution.ForbiddenEvidence);
                AssertEvidence(
                    failures,
                    stage + "/ExecutionEvidence",
                    execution.ExecutionEvidence,
                    expectedExecution.RequiredExecutionEvidence,
                    expectedExecution.ForbiddenExecutionEvidence);
                AssertResults(
                    expectedExecution.Results,
                    execution.Results,
                    stage,
                    failures);
            }

            foreach (FactCheckExecutionEnvelope actualExecution
                in actual.Executions)
            {
                string identity = ExecutionIdentity(actualExecution);
                if (!expected.ExpectedExecutions.Any(item => string.Equals(
                    ExpectedExecutionIdentity(item),
                    identity,
                    StringComparison.Ordinal)))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "Execution",
                        "Unexpected execution identity '" + identity + "'."));
                }
            }
        }

        private static void AssertAnalysis(
            V2GoldenCaseExpected expected,
            V2AnalysisResult actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            AddMismatch(failures, "Analysis", expected.ExpectedAnalysis.StatusSummary, actual.StatusSummary.ToString(), "StatusSummary");
            AddMismatch(failures, "Analysis", expected.ExpectedAnalysis.ExpectedResultCount.ToString(), actual.Results.Count.ToString(), "result count");

            AssertResults(
                expected.ExpectedAnalysis.Results,
                actual.Results,
                "Analysis",
                failures);
        }

        private static void AssertResults(
            IEnumerable<V2GoldenCaseConsistencyResultAssertion> expectedResults,
            IEnumerable<ConsistencyResult> actualResults,
            string parentStage,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            V2GoldenCaseConsistencyResultAssertion[] expectedItems =
                expectedResults.ToArray();
            ConsistencyResult[] actualItems = actualResults.ToArray();

            AddDuplicateFailures(
                actualItems,
                item => item.Identity.CanonicalValue,
                parentStage + "/Result",
                "Result",
                failures);

            foreach (V2GoldenCaseConsistencyResultAssertion expected
                in expectedItems)
            {
                string identity = ExpectedResultIdentity(expected);
                ConsistencyResult[] matches = actualItems
                    .Where(item => string.Equals(
                        item.Identity.CanonicalValue,
                        identity,
                        StringComparison.Ordinal))
                    .ToArray();
                string stage = parentStage + "/Result[" + identity + "]";
                if (matches.Length == 0)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Expected result identity is missing."));
                    continue;
                }

                if (matches.Length > 1)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Expected result identity is duplicated."));
                    continue;
                }

                ConsistencyResult actual = matches[0];
                AddMismatch(failures, stage, expected.Status, actual.Status.ToString(), "Status");
                if (actual.Reason.IndexOf(
                    expected.ReasonContains,
                    StringComparison.Ordinal) < 0)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Reason does not contain '" + expected.ReasonContains + "'."));
                }

                AssertEvidence(
                    failures,
                    stage,
                    actual.Evidence,
                    expected.RequiredEvidence,
                    expected.ForbiddenEvidence);
            }

            foreach (ConsistencyResult actual in actualItems)
            {
                if (!expectedItems.Any(item => string.Equals(
                    ExpectedResultIdentity(item),
                    actual.Identity.CanonicalValue,
                    StringComparison.Ordinal)))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        parentStage + "/Result",
                        "Unexpected result identity '"
                            + actual.Identity.CanonicalValue + "'."));
                }
            }
        }

        private static void AssertStructuralInvariants(
            V2AnalysisResult actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            ConsistencyResult[] executionResults = actual.Executions
                .SelectMany(execution => execution.Results)
                .ToArray();
            AddDuplicateFailures(
                executionResults,
                result => result.Identity.CanonicalValue,
                "ExecutionResultUnion",
                "Result",
                failures);

            foreach (FactCheckExecutionEnvelope execution in actual.Executions)
            {
                string[] flattened = DeterministicEvidence(execution.Results);
                if (!execution.Evidence.SequenceEqual(flattened))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "Execution[" + ExecutionIdentity(execution) + "]",
                        "Compatibility Evidence is not the deterministic, de-duplicated Result Evidence projection."));
                }
            }

            AssertExactResultUnion(executionResults, actual.Results, failures);

            string expectedSummary =
                SummarizeStatus(actual.Results, actual.Executions).ToString();
            AddMismatch(failures, "Analysis", expectedSummary, actual.StatusSummary.ToString(), "independently aggregated StatusSummary");

            string[] analysisEvidence = DeterministicEvidence(actual.Results);
            if (!actual.Evidence.SequenceEqual(analysisEvidence))
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    "Analysis",
                    "Top-level Evidence is not the deterministic, de-duplicated Result Evidence projection."));
            }
        }

        private static void AssertExactResultUnion(
            IEnumerable<ConsistencyResult> executionResults,
            IEnumerable<ConsistencyResult> analysisResults,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            Dictionary<string, ConsistencyResult[]> executionByIdentity =
                executionResults
                    .GroupBy(item => item.Identity.CanonicalValue, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            Dictionary<string, ConsistencyResult[]> analysisByIdentity =
                analysisResults
                    .GroupBy(item => item.Identity.CanonicalValue, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

            foreach (string identity in executionByIdentity.Keys
                .Union(analysisByIdentity.Keys, StringComparer.Ordinal))
            {
                ConsistencyResult[] fromExecution;
                ConsistencyResult[] fromAnalysis;
                executionByIdentity.TryGetValue(identity, out fromExecution);
                analysisByIdentity.TryGetValue(identity, out fromAnalysis);
                if (fromExecution == null
                    || fromAnalysis == null
                    || fromExecution.Length != 1
                    || fromAnalysis.Length != 1)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "ExecutionResultUnion",
                        "Execution Results and Analysis Results differ for identity '"
                            + identity + "'."));
                    continue;
                }

                if (!SameResult(fromExecution[0], fromAnalysis[0]))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "ExecutionResultUnion",
                        "Execution Result content differs from Analysis Result for identity '"
                            + identity + "'."));
                }
            }
        }

        private static bool SameResult(
            ConsistencyResult first,
            ConsistencyResult second)
        {
            return first.Status == second.Status
                && string.Equals(first.Reason, second.Reason, StringComparison.Ordinal)
                && first.Evidence.SequenceEqual(second.Evidence);
        }

        private static ConsistencyStatus SummarizeStatus(
            IReadOnlyCollection<ConsistencyResult> results,
            IReadOnlyCollection<FactCheckExecutionEnvelope> executions)
        {
            if (results.Any(result => result.Status == ConsistencyStatus.FAIL))
            {
                return ConsistencyStatus.FAIL;
            }

            if (results.Any(result => result.Status == ConsistencyStatus.WARNING)
                || executions.Any(execution =>
                    execution.ExecutionStatus == FactCheckExecutionStatus.Failed
                    || execution.ExecutionStatus
                        == FactCheckExecutionStatus.CompletedWithWarning)
                || results.Count == 0)
            {
                return ConsistencyStatus.WARNING;
            }

            return ConsistencyStatus.PASS;
        }

        private static string[] DeterministicEvidence(
            IEnumerable<ConsistencyResult> results)
        {
            return results
                .OrderBy(
                    result => result.Identity.CanonicalValue,
                    StringComparer.Ordinal)
                .SelectMany(result => result.Evidence)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string ExecutionIdentity(
            FactCheckExecutionEnvelope execution)
        {
            return execution.CheckerName + "/" + execution.FactType;
        }

        private static string ExpectedExecutionIdentity(
            V2GoldenCaseExecutionAssertion execution)
        {
            return execution.CheckerName + "/" + execution.FactType;
        }

        private static string ExpectedResultIdentity(
            V2GoldenCaseConsistencyResultAssertion result)
        {
            if (result.Identity == null)
            {
                return string.Empty;
            }

            return string.Concat(
                result.Identity.FactType,
                "|",
                result.Identity.SubjectKey,
                "|",
                result.Identity.ResultRole,
                "|",
                result.Identity.ScopeKey);
        }

        private static void AssertEvidence(
            ICollection<V2GoldenCaseAssertionFailure> failures,
            string stage,
            IEnumerable<string> actualEvidence,
            IEnumerable<string> requiredEvidence,
            IEnumerable<string> forbiddenEvidence)
        {
            string[] evidence = actualEvidence.ToArray();
            foreach (string required in requiredEvidence)
            {
                if (!evidence.Any(item =>
                    item.IndexOf(required, StringComparison.Ordinal) >= 0))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Evidence does not contain '" + required + "'."));
                }
            }

            foreach (string forbidden in forbiddenEvidence)
            {
                if (evidence.Any(item =>
                    item.IndexOf(forbidden, StringComparison.Ordinal) >= 0))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Evidence unexpectedly contains forbidden token '"
                            + forbidden + "'."));
                }
            }
        }

        private static void AddDuplicateFailures<T>(
            IEnumerable<T> items,
            Func<T, string> identitySelector,
            string stage,
            string identityName,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            foreach (IGrouping<string, T> duplicate in items
                .GroupBy(identitySelector, StringComparer.Ordinal)
                .Where(group => group.Count() > 1))
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    "Duplicate " + identityName + " identity '"
                        + duplicate.Key + "'."));
            }
        }

        private static void AddMismatch(
            ICollection<V2GoldenCaseAssertionFailure> failures,
            string stage,
            string expected,
            string actual,
            string field)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    field + " expected '" + expected + "' but was '"
                        + actual + "'."));
            }
        }
    }
}
