using System;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class V2GoldenCaseSchema2Tests
    {
        [TestMethod]
        public void Loader_ParsesOneExecutionWithTwoCompositeIdentityResults()
        {
            V2GoldenCaseExpected expected =
                new V2GoldenCaseJsonLoader().ParseExpected(Schema2Json());

            Assert.AreEqual(
                V2GoldenCaseJsonLoader.ExpectedSchemaVersionV2,
                expected.SchemaVersion);
            Assert.HasCount(1, expected.ExpectedExecutions);
            Assert.AreEqual(2, expected.ExpectedExecutions[0].ExpectedResultCount);
            Assert.HasCount(2, expected.ExpectedExecutions[0].Results);
            CollectionAssert.AreEquivalent(
                new[] { "subject/v1:subject-a", "subject/v1:subject-b" },
                expected.ExpectedAnalysis.Results
                    .Select(item => item.Identity.SubjectKey)
                    .ToArray());
        }

        [TestMethod]
        public void Loader_RejectsUnknownFieldMissingIdentityAndDuplicateIdentity()
        {
            string valid = Schema2Json();
            string unknown = valid.Replace(
                "\"ResultRole\":\"SyntheticConsistency\",",
                "\"ResultRole\":\"SyntheticConsistency\",\"Unknown\":true,");
            string missing = valid.Replace(
                "\"Identity\":{\"FactType\":\"EvaluationMethod\",\"SubjectKey\":\"subject/v1:subject-a\",\"ResultRole\":\"SyntheticConsistency\",\"ScopeKey\":\"scope/v1:default\"},",
                string.Empty);
            string duplicate = valid.Replace(
                "subject/v1:subject-b",
                "subject/v1:subject-a");

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(unknown));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(missing));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(duplicate));
        }

        [TestMethod]
        public void Loader_RejectsNotApplicableWithoutReasonOrWithResult()
        {
            string results = "\"Results\":["
                + ResultJson("subject-a", "PASS", "subject-a")
                + "," + ResultJson("subject-b", "FAIL", "subject-b") + "]";
            string noReason = Schema2Json()
                .Replace("\"ExecutionStatus\":\"Succeeded\"", "\"ExecutionStatus\":\"NotApplicable\"")
                .Replace("\"ExpectedResultCount\":2", "\"ExpectedResultCount\":0")
                .Replace(results, "\"Results\":[]")
                .Replace(
                    "\"ExpectedAnalysis\":{\"StatusSummary\":\"FAIL\",\"ExpectedResultCount\":2}",
                    "\"ExpectedAnalysis\":{\"StatusSummary\":\"WARNING\",\"ExpectedResultCount\":0}");
            string withResult = Schema2Json()
                .Replace("\"ExecutionStatus\":\"Succeeded\"", "\"ExecutionStatus\":\"NotApplicable\"")
                .Replace("\"NoResultReason\":\"\"", "\"NoResultReason\":\"确定性不适用\"");

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(noReason));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(withResult));
        }

        [TestMethod]
        public void Assertor_MatchesMultiResultsByCompositeIdentityRegardlessOfOrder()
        {
            V2GoldenCaseExpected expected =
                new V2GoldenCaseJsonLoader().ParseExpected(Schema2Json());
            ConsistencyResult subjectA = Result("subject-a", ConsistencyStatus.PASS);
            ConsistencyResult subjectB = Result("subject-b", ConsistencyStatus.FAIL);
            FactCheckExecutionEnvelope envelope =
                FactCheckExecutionEnvelope.FromResults(
                    "SyntheticMultiResultChecker",
                    FactType.EvaluationMethod,
                    new[] { subjectB, subjectA });
            var actual = new V2AnalysisResult(
                new[] { subjectB, subjectA },
                new[] { envelope });

            expected.ExpectedExecutions[0].Results =
                expected.ExpectedExecutions[0].Results.Reverse().ToArray();
            expected.ExpectedAnalysis.Results =
                expected.ExpectedAnalysis.Results.Reverse().ToArray();

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                actual,
                Array.Empty<V2GoldenCaseSemanticCallRecord>());

            Assert.AreEqual(
                0,
                failures.Count,
                string.Join(Environment.NewLine, failures.Select(item => item.ToString())));
        }

        [TestMethod]
        public void Assertor_RejectsMissingAndExtraCompositeIdentity()
        {
            V2GoldenCaseExpected expected =
                new V2GoldenCaseJsonLoader().ParseExpected(Schema2Json());
            ConsistencyResult subjectA = Result("subject-a", ConsistencyStatus.PASS);
            ConsistencyResult subjectC = Result("subject-c", ConsistencyStatus.FAIL);
            FactCheckExecutionEnvelope envelope =
                FactCheckExecutionEnvelope.FromResults(
                    "SyntheticMultiResultChecker",
                    FactType.EvaluationMethod,
                    new[] { subjectA, subjectC });
            var actual = new V2AnalysisResult(
                new[] { subjectA, subjectC },
                new[] { envelope });

            var failures = new V2GoldenCaseResultAssertor().Assert(
                expected,
                actual,
                Array.Empty<V2GoldenCaseSemanticCallRecord>());

            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf("identity is missing", StringComparison.Ordinal) >= 0));
            Assert.IsTrue(failures.Any(item =>
                item.Message.IndexOf("Unexpected result identity", StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void Loader_AdaptsSchema1ResultToFrozenDefaultIdentity()
        {
            string expectedPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "GoldenCases",
                "V2",
                "Runner",
                "V2-GT-101",
                "Expected.json");
            V2GoldenCaseExpected expected =
                new V2GoldenCaseJsonLoader().LoadExpected(expectedPath);
            V2GoldenCaseResultIdentityAssertion identity =
                expected.ExpectedAnalysis.Results.Single().Identity;

            Assert.AreEqual("AssessmentObject", identity.FactType);
            Assert.AreEqual(
                ConsistencyResultIdentity.DefaultSubjectKey,
                identity.SubjectKey);
            Assert.AreEqual(
                ConsistencyResultIdentity.DefaultResultRole,
                identity.ResultRole);
            Assert.AreEqual(
                ConsistencyResultIdentity.DefaultScopeKey,
                identity.ScopeKey);
        }

        private static ConsistencyResult Result(
            string subject,
            ConsistencyStatus status)
        {
            return new ConsistencyResult(
                new ConsistencyResultIdentity(
                    FactType.EvaluationMethod,
                    "subject/v1:" + subject,
                    "SyntheticConsistency",
                    ConsistencyResultIdentity.DefaultScopeKey),
                status,
                "Synthetic " + subject + " result.",
                new[] { "Evidence=" + subject });
        }

        private static string Schema2Json()
        {
            return "{"
                + "\"SchemaVersion\":\"v2-golden-case/2.0\","
                + "\"CaseId\":\"SYNTHETIC-MULTI\","
                + "\"ExpectedSemanticCallCount\":0,"
                + "\"ExpectedFacts\":[{"
                + "\"DocumentId\":\"report\",\"FactType\":\"EvaluationMethod\","
                + "\"ExpectedCount\":0,\"AllowAdditionalFacts\":false,\"Facts\":[]}],"
                + "\"Semantic\":{\"Mode\":\"Forbidden\",\"ExpectedCallCount\":0,"
                + "\"ExpectedCalls\":[],\"Responses\":[],\"FailureMessage\":\"\"},"
                + "\"ExpectedExecutions\":[{"
                + "\"CheckerName\":\"SyntheticMultiResultChecker\","
                + "\"FactType\":\"EvaluationMethod\","
                + "\"ExecutionStatus\":\"Succeeded\","
                + "\"ExpectedResultCount\":2,"
                + "\"ErrorInformation\":\"\",\"NoResultReason\":\"\","
                + "\"RequiredExecutionEvidence\":[],"
                + "\"ForbiddenExecutionEvidence\":[],"
                + "\"Results\":["
                + ResultJson("subject-a", "PASS", "subject-a")
                + ","
                + ResultJson("subject-b", "FAIL", "subject-b")
                + "]}],"
                + "\"ExpectedAnalysis\":{\"StatusSummary\":\"FAIL\",\"ExpectedResultCount\":2}"
                + "}";
        }

        private static string ResultJson(
            string subject,
            string status,
            string evidence)
        {
            string other = subject == "subject-a" ? "subject-b" : "subject-a";
            return "{"
                + "\"Identity\":{\"FactType\":\"EvaluationMethod\","
                + "\"SubjectKey\":\"subject/v1:" + subject + "\","
                + "\"ResultRole\":\"SyntheticConsistency\","
                + "\"ScopeKey\":\"scope/v1:default\"},"
                + "\"Status\":\"" + status + "\","
                + "\"ReasonContains\":\"Synthetic\","
                + "\"RequiredEvidence\":[\"Evidence=" + evidence + "\"],"
                + "\"ForbiddenEvidence\":[\"Evidence=" + other + "\"]"
                + "}";
        }
    }
}
