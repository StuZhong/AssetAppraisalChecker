using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class V2GoldenCaseResultAssertorTests
    {
        [TestMethod]
        public void Assert_MatchesExecutionsAndResultsByIdentityRegardlessOfExpectedOrder()
        {
            V2GoldenCaseExecutionResult execution = Run("V2-GT-901");
            V2GoldenCaseExpected expected = LoadExpected("V2-GT-901");
            expected.ExpectedExecutions = expected.ExpectedExecutions.Reverse().ToArray();
            expected.ExpectedAnalysis.Results = expected.ExpectedAnalysis.Results.Reverse().ToArray();

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                execution.AnalysisResult,
                execution.SemanticCalls);

            Assert.AreEqual(
                0,
                failures.Count,
                string.Join(Environment.NewLine, failures.Select(item => item.ToString())));
        }

        [TestMethod]
        public void Assert_RejectsMissingExecutionAndResultIdentity()
        {
            V2GoldenCaseExecutionResult execution = Run("V2-GT-901");
            V2GoldenCaseExpected expected = LoadExpected("V2-GT-901");
            var incomplete = new V2AnalysisResult(
                execution.AnalysisResult.Results.Skip(1),
                execution.AnalysisResult.Executions.Skip(1));

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                incomplete,
                execution.SemanticCalls);

            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf("identity is missing", StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void Assert_RejectsDuplicateExecutionAndResultIdentity()
        {
            V2GoldenCaseExecutionResult execution = Run("V2-GT-901");
            V2GoldenCaseExpected expected = LoadExpected("V2-GT-901");
            var duplicated = new V2AnalysisResult(
                execution.AnalysisResult.Results.Concat(
                    new[] { execution.AnalysisResult.Results[0] }),
                execution.AnalysisResult.Executions.Concat(
                    new[] { execution.AnalysisResult.Executions[0] }));

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                duplicated,
                execution.SemanticCalls);

            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf("Duplicate", StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void Assert_RejectsUnexpectedExecutionAndResult()
        {
            V2GoldenCaseExecutionResult execution = Run("V2-GT-101");
            V2GoldenCaseExpected expected = LoadExpected("V2-GT-101");
            var extraResult = new ConsistencyResult(
                FactType.AssessmentValue,
                ConsistencyStatus.PASS,
                "synthetic extra result",
                new[] { "synthetic evidence" });
            var extraExecution = FactCheckExecutionEnvelope.FromResult(
                "SyntheticAssessmentValueChecker",
                extraResult);
            var unexpected = new V2AnalysisResult(
                execution.AnalysisResult.Results.Concat(new[] { extraResult }),
                execution.AnalysisResult.Executions.Concat(new[] { extraExecution }));

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                unexpected,
                execution.SemanticCalls);

            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf("Unexpected execution identity", StringComparison.Ordinal) >= 0));
            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf("Unexpected result identity", StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void Assert_RejectsEvidenceFromAnotherFactDomain()
        {
            V2GoldenCaseExecutionResult execution = Run("V2-GT-901");
            V2GoldenCaseExpected expected = LoadExpected("V2-GT-901");
            ConsistencyResult original = execution.AnalysisResult.Results.Single(
                item => item.FactType == FactType.BenchmarkDate);
            var polluted = new ConsistencyResult(
                original.FactType,
                original.Status,
                original.Reason,
                original.Evidence.Concat(new[] { "NormalizedAmountYuan=9982200.00" }));
            ConsistencyResult[] results = execution.AnalysisResult.Results
                .Select(item => item.FactType == FactType.BenchmarkDate ? polluted : item)
                .ToArray();
            FactCheckExecutionEnvelope[] executions = execution.AnalysisResult.Executions
                .Select(item => item.FactType == FactType.BenchmarkDate
                    ? FactCheckExecutionEnvelope.FromResult(item.CheckerName, polluted)
                    : item)
                .ToArray();

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                new V2AnalysisResult(results, executions),
                execution.SemanticCalls);

            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf(
                    "forbidden token 'NormalizedAmountYuan='",
                    StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void ExpectedLoader_RejectsDuplicateExecutionAndResultIdentities()
        {
            var serializer = new JavaScriptSerializer();
            V2GoldenCaseExpected duplicateExecution = LoadExpected("V2-GT-901");
            duplicateExecution.ExpectedExecutions[1].CheckerName =
                duplicateExecution.ExpectedExecutions[0].CheckerName;

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(
                    serializer.Serialize(duplicateExecution)));

            V2GoldenCaseExpected duplicateResult = LoadExpected("V2-GT-901");
            duplicateResult.ExpectedAnalysis.Results[1].FactType =
                duplicateResult.ExpectedAnalysis.Results[0].FactType;

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(
                    serializer.Serialize(duplicateResult)));
        }

        private static V2GoldenCaseExecutionResult Run(string caseId)
        {
            V2GoldenCaseExecutionResult execution = new V2GoldenCaseRunner().Run(
                GetManifestPath(),
                caseId);
            Assert.IsTrue(
                execution.IsSuccess,
                string.Join(Environment.NewLine, execution.Failures.Select(item => item.ToString())));
            return execution;
        }

        private static V2GoldenCaseExpected LoadExpected(string caseId)
        {
            V2GoldenCaseManifestEntry manifestCase = new V2GoldenCaseJsonLoader()
                .LoadManifest(GetManifestPath())
                .Cases.Single(item => item.CaseId == caseId);
            return new V2GoldenCaseJsonLoader().LoadExpected(
                Path.Combine(
                    GetGoldenCaseRoot(),
                    manifestCase.ExpectedFile.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string GetManifestPath()
        {
            return Path.Combine(GetGoldenCaseRoot(), "Manifest.json");
        }

        private static string GetGoldenCaseRoot()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "GoldenCases",
                "V2");
        }
    }
}
