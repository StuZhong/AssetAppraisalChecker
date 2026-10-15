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
    public sealed class V2GoldenCaseRunnerTests
    {
        [TestMethod]
        public void ManifestLoader_ReadsAllRegisteredShadowCases()
        {
            V2GoldenCaseManifest manifest = new V2GoldenCaseJsonLoader()
                .LoadManifest(GetManifestPath());

            Assert.AreEqual(V2GoldenCaseJsonLoader.ManifestSchemaVersion, manifest.SchemaVersion);
            CollectionAssert.AreEqual(
                new[]
                {
                    "V2-GT-101", "V2-GT-102", "V2-GT-103", "V2-GT-104", "V2-GT-105",
                    "V2-GT-201", "V2-GT-202", "V2-GT-203", "V2-GT-204",
                    "V2-GT-301", "V2-GT-302", "V2-GT-303",
                    "V2-GT-401", "V2-GT-402", "V2-GT-403",
                    "V2-GT-501", "V2-GT-502", "V2-GT-503", "V2-GT-504",
                    "V2-GT-505", "V2-GT-506", "V2-GT-507", "V2-GT-508",
                    "V2-GT-509", "V2-GT-510", "V2-GT-511", "V2-GT-512", "V2-GT-513",
                    "V2-GT-901", "V2-GT-902", "V2-GT-903"
                },
                manifest.Cases.OrderBy(item => item.Order).Select(item => item.CaseId).ToArray());
            Assert.IsTrue(manifest.Cases.All(item => item.Inputs.Length == 2));
            Assert.AreEqual(15, manifest.Cases.Count(item =>
                item.LegacySchemaVersion == V2GoldenLegacyExpectedAdapter.SchemaVersion
                && !string.IsNullOrWhiteSpace(item.LegacyExpectedFile)));
            Assert.AreEqual(16, manifest.Cases.Count(item =>
                string.IsNullOrWhiteSpace(item.LegacySchemaVersion)
                && string.IsNullOrWhiteSpace(item.LegacyExpectedFile)));
            foreach (V2GoldenCaseManifestEntry manifestCase in manifest.Cases)
            {
                CollectionAssert.AreEquivalent(
                    new[] { "Report", "Explanation" },
                    manifestCase.Inputs.Select(input => input.Role).ToArray());
            }
        }

        [TestMethod]
        public void ManifestLoader_RejectsUnsupportedSchemaVersion()
        {
            const string json =
                "{\"SchemaVersion\":\"v2-golden-manifest/2.0\",\"Cases\":[]}";

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseManifest(json));
        }

        [TestMethod]
        public void ManifestLoader_RejectsUnknownField()
        {
            string json = File.ReadAllText(GetManifestPath()).Replace(
                "\"SchemaVersion\": \"v2-golden-manifest/1.0\",",
                "\"SchemaVersion\": \"v2-golden-manifest/1.0\",\n  \"UnexpectedField\": true,");

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseManifest(json));
        }

        [TestMethod]
        public void ManifestLoader_AcceptsLegacyConfigurationWhenPairIsAbsent()
        {
            const string json =
                "{\"SchemaVersion\":\"v2-golden-manifest/1.0\",\"Cases\":["
                + "{\"Order\":1,\"CaseId\":\"SYNTHETIC\",\"ExpectedFile\":\"Expected.json\","
                + "\"Inputs\":["
                + "{\"DocumentId\":\"report\",\"Role\":\"Report\",\"File\":\"Report.docx\",\"Sha256\":\""
                + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"},"
                + "{\"DocumentId\":\"explanation\",\"Role\":\"Explanation\",\"File\":\"Explanation.docx\",\"Sha256\":\""
                + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}]}]}";

            V2GoldenCaseManifest manifest = new V2GoldenCaseJsonLoader().ParseManifest(json);

            Assert.AreEqual(string.Empty, manifest.Cases.Single().LegacyExpectedFile);
            Assert.AreEqual(string.Empty, manifest.Cases.Single().LegacySchemaVersion);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ManifestLoader_RejectsOneSidedLegacyConfiguration(bool includeFile)
        {
            string legacy = includeFile
                ? ",\"LegacyExpectedFile\":\"Legacy.json\""
                : ",\"LegacySchemaVersion\":\"legacy-v2-golden-case/1.0\"";
            string json =
                "{\"SchemaVersion\":\"v2-golden-manifest/1.0\",\"Cases\":["
                + "{\"Order\":1,\"CaseId\":\"SYNTHETIC\",\"ExpectedFile\":\"Expected.json\""
                + legacy
                + ",\"Inputs\":["
                + "{\"DocumentId\":\"report\",\"Role\":\"Report\",\"File\":\"Report.docx\",\"Sha256\":\""
                + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"},"
                + "{\"DocumentId\":\"explanation\",\"Role\":\"Explanation\",\"File\":\"Explanation.docx\",\"Sha256\":\""
                + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}]}]}";

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseManifest(json));
        }

        [TestMethod]
        public void Sha256Verifier_AcceptsEveryManifestHashAndRejectsDifferentHash()
        {
            V2GoldenCaseManifest manifest = new V2GoldenCaseJsonLoader()
                .LoadManifest(GetManifestPath());

            foreach (V2GoldenCaseInputDefinition input in manifest.Cases.SelectMany(item => item.Inputs))
            {
                string inputPath = Resolve(input.File);
                Assert.IsTrue(V2GoldenCaseSha256.Verify(inputPath, input.Sha256));
                Assert.IsFalse(V2GoldenCaseSha256.Verify(inputPath, new string('0', 64)));
            }
        }

        [TestMethod]
        [DataRow("V2-GT-101", "PASS", 0, "Forbidden")]
        [DataRow("V2-GT-102", "PASS", 1, "DeterministicMock")]
        [DataRow("V2-GT-103", "FAIL", 0, "Forbidden")]
        [DataRow("V2-GT-104", "FAIL", 0, "Forbidden")]
        [DataRow("V2-GT-105", "WARNING", 1, "ControlledFailure")]
        [DataRow("V2-GT-201", "PASS", 0, "Forbidden")]
        [DataRow("V2-GT-202", "PASS", 0, "Forbidden")]
        [DataRow("V2-GT-203", "FAIL", 0, "Forbidden")]
        [DataRow("V2-GT-204", "WARNING", 0, "Forbidden")]
        [DataRow("V2-GT-301", "PASS", 0, "Forbidden")]
        [DataRow("V2-GT-302", "FAIL", 0, "Forbidden")]
        [DataRow("V2-GT-303", "WARNING", 0, "Forbidden")]
        [DataRow("V2-GT-401", "PASS", 0, "Forbidden")]
        [DataRow("V2-GT-402", "FAIL", 0, "Forbidden")]
        [DataRow("V2-GT-403", "WARNING", 0, "Forbidden")]
        public void ExpectedLoader_ReadsUnifiedSchema(
            string caseId,
            string status,
            int expectedSemanticCallCount,
            string semanticMode)
        {
            V2GoldenCaseManifestEntry manifestCase = GetManifestCase(caseId);
            var loader = new V2GoldenCaseJsonLoader();

            V2GoldenCaseExpected expected = loader.LoadExpected(
                Resolve(manifestCase.ExpectedFile));

            Assert.AreEqual(V2GoldenCaseJsonLoader.ExpectedSchemaVersion, expected.SchemaVersion);
            Assert.AreEqual(caseId, expected.CaseId);
            Assert.AreEqual(5, expected.ExpectedExecutions.Length);
            Assert.AreEqual(status, expected.ExpectedAnalysis.StatusSummary);
            Assert.AreEqual(1, expected.ExpectedAnalysis.ExpectedResultCount);
            Assert.AreEqual(expectedSemanticCallCount, expected.Semantic.ExpectedCallCount);
            Assert.AreEqual(semanticMode, expected.Semantic.Mode);
            Assert.IsTrue(expected.ExpectedFacts.Length > 0);
        }

        [TestMethod]
        public void ExpectedLoader_RejectsUnsupportedSchemaVersion()
        {
            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(
                    "{\"SchemaVersion\":\"v2-golden-case/3.0\",\"CaseId\":\"V2-GT-401\"}"));
        }

        [TestMethod]
        public void ExpectedLoader_RejectsUnknownField()
        {
            V2GoldenCaseManifestEntry manifestCase = GetManifestCase("V2-GT-101");
            string json = File.ReadAllText(Resolve(manifestCase.ExpectedFile)).Replace(
                "\"SchemaVersion\": \"v2-golden-case/1.0\",",
                "\"SchemaVersion\": \"v2-golden-case/1.0\",\n  \"UnexpectedField\": true,");

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(json));
        }

        [TestMethod]
        public void ExpectedLoader_RejectsMissingRequiredSections()
        {
            const string json =
                "{\"SchemaVersion\":\"v2-golden-case/1.0\",\"CaseId\":\"V2-GT-101\"}";

            Assert.ThrowsExactly<InvalidDataException>(() =>
                new V2GoldenCaseJsonLoader().ParseExpected(json));
        }

        [TestMethod]
        [DataRow("V2-GT-101", "PASS", "AssessmentObject", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-102", "PASS", "AssessmentObject", "Succeeded", 2, 1, "Scope")]
        [DataRow("V2-GT-103", "FAIL", "AssessmentObject", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-104", "FAIL", "AssessmentObject", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-105", "WARNING", "AssessmentObject", "Failed", 2, 1, "Scope")]
        [DataRow("V2-GT-201", "PASS", "AssessmentValue", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-202", "PASS", "AssessmentValue", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-203", "FAIL", "AssessmentValue", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-204", "WARNING", "AssessmentValue", "CompletedWithWarning", 1, 0, "")]
        [DataRow("V2-GT-301", "PASS", "BenchmarkDate", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-302", "FAIL", "BenchmarkDate", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-303", "WARNING", "BenchmarkDate", "CompletedWithWarning", 1, 0, "")]
        [DataRow("V2-GT-401", "PASS", "ReportNumber", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-402", "FAIL", "ReportNumber", "Succeeded", 2, 0, "")]
        [DataRow("V2-GT-403", "WARNING", "ReportNumber", "CompletedWithWarning", 1, 0, "")]
        public void Runner_ExecutesUnifiedShadowCase(
            string caseId,
            string overallStatus,
            string factTypeText,
            string executionStatus,
            int factCount,
            int semanticCallCount,
            string semanticField)
        {
            V2GoldenCaseExecutionResult execution = new V2GoldenCaseRunner().Run(
                GetManifestPath(),
                caseId);

            AssertSuccessful(execution);
            Assert.AreEqual(overallStatus, execution.AnalysisResult.StatusSummary.ToString());
            FactType factType = (FactType)Enum.Parse(typeof(FactType), factTypeText);
            Assert.AreEqual(factType, execution.AnalysisResult.Results.Single().FactType);
            Assert.AreEqual(overallStatus, execution.AnalysisResult.Results.Single().Status.ToString());
            Assert.AreEqual(
                executionStatus,
                execution.AnalysisResult.Executions.Single(
                    item => item.FactType == factType).ExecutionStatus.ToString());
            Assert.AreEqual(factCount, execution.Facts.Count);
            Assert.AreEqual(semanticCallCount, execution.SemanticCallCount);
            if (semanticCallCount > 0)
            {
                Assert.AreEqual(semanticField, execution.SemanticCalls.Single().FieldName);
            }

            Assert.AreEqual(2, execution.OpenXmlPreflight.Count);
            Assert.IsTrue(execution.OpenXmlPreflight.Values.All(item => item));
            Assert.AreEqual(2, execution.PrivacyPreflight.Count);
            Assert.IsTrue(execution.PrivacyPreflight.Values.All(item => item));
            Assert.AreEqual(2, execution.HashesBefore.Count);
            Assert.AreEqual(2, execution.HashesAfter.Count);
            foreach (string documentId in execution.HashesBefore.Keys)
            {
                Assert.AreEqual(execution.HashesBefore[documentId], execution.HashesAfter[documentId]);
            }
        }

        [TestMethod]
        [DataRow("V2-GT-901", "PASS", "PASS", "Succeeded", 8)]
        [DataRow("V2-GT-902", "FAIL", "FAIL", "Succeeded", 8)]
        [DataRow("V2-GT-903", "WARNING", "WARNING", "CompletedWithWarning", 7)]
        public void Runner_ExecutesIntegratedMultiCheckerCase(
            string caseId,
            string overallStatus,
            string assessmentValueStatus,
            string assessmentValueExecutionStatus,
            int expectedFactCount)
        {
            V2GoldenCaseExecutionResult execution = new V2GoldenCaseRunner().Run(
                GetManifestPath(),
                caseId);

            AssertSuccessful(execution);
            Assert.AreEqual(overallStatus, execution.AnalysisResult.StatusSummary.ToString());
            Assert.AreEqual(5, execution.AnalysisResult.Executions.Count);
            Assert.AreEqual(4, execution.AnalysisResult.Results.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    FactType.AssessmentObject,
                    FactType.AssessmentValue,
                    FactType.BenchmarkDate,
                    FactType.ReportNumber
                },
                execution.AnalysisResult.Results.Select(item => item.FactType).ToArray());
            Assert.AreEqual(
                assessmentValueStatus,
                execution.AnalysisResult.Results.Single(
                    item => item.FactType == FactType.AssessmentValue).Status.ToString());
            Assert.AreEqual(
                assessmentValueExecutionStatus,
                execution.AnalysisResult.Executions.Single(
                    item => item.FactType == FactType.AssessmentValue).ExecutionStatus.ToString());
            Assert.AreEqual(expectedFactCount, execution.Facts.Count);
            Assert.AreEqual(0, execution.SemanticCallCount);
            Assert.AreEqual(2, execution.OpenXmlPreflight.Count);
            Assert.IsTrue(execution.OpenXmlPreflight.Values.All(item => item));
            Assert.AreEqual(2, execution.PrivacyPreflight.Count);
            Assert.IsTrue(execution.PrivacyPreflight.Values.All(item => item));
            Assert.IsTrue(execution.HashesBefore.Keys.All(documentId =>
                execution.HashesBefore[documentId] == execution.HashesAfter[documentId]));
        }

        [TestMethod]
        [DataRow("V2-GT-501", "PASS", "Succeeded", 1, 2, 0)]
        [DataRow("V2-GT-502", "WARNING", "CompletedWithWarning", 1, 2, 0)]
        [DataRow("V2-GT-503", "FAIL", "Succeeded", 1, 2, 0)]
        [DataRow("V2-GT-504", "WARNING", "CompletedWithWarning", 1, 1, 0)]
        [DataRow("V2-GT-505", "PASS", "Succeeded", 1, 4, 0)]
        [DataRow("V2-GT-506", "WARNING", "CompletedWithWarning", 1, 3, 0)]
        [DataRow("V2-GT-507", "PASS", "Succeeded", 2, 4, 0)]
        [DataRow("V2-GT-508", "FAIL", "Succeeded", 2, 4, 0)]
        [DataRow("V2-GT-509", "PASS", "Succeeded", 1, 2, 0)]
        [DataRow("V2-GT-510", "WARNING", "CompletedWithWarning", 1, 1, 1)]
        [DataRow("V2-GT-511", "WARNING", "Failed", 1, 1, 1)]
        [DataRow("V2-GT-512", "WARNING", "CompletedWithWarning", 2, 2, 0)]
        [DataRow("V2-GT-513", "FAIL", "Succeeded", 1, 1, 0)]
        public void Runner_ExecutesEvaluationMethodCase(
            string caseId,
            string overallStatus,
            string executionStatus,
            int resultCount,
            int factCount,
            int semanticCallCount)
        {
            V2GoldenCaseExecutionResult execution = new V2GoldenCaseRunner().Run(
                GetManifestPath(),
                caseId);

            AssertSuccessful(execution);
            Assert.AreEqual(overallStatus, execution.AnalysisResult.StatusSummary.ToString());
            FactCheckExecutionEnvelope methodExecution = execution.AnalysisResult.Executions.Single(
                item => item.FactType == FactType.EvaluationMethod);
            Assert.AreEqual(executionStatus, methodExecution.ExecutionStatus.ToString());
            Assert.AreEqual(resultCount, methodExecution.Results.Count);
            Assert.AreEqual(resultCount, execution.AnalysisResult.Results.Count);
            Assert.IsTrue(methodExecution.Results.All(item =>
                item.Identity.ResultRole == EvaluationMethodComparer.ResultRole
                && item.Identity.ScopeKey == EvaluationMethodComparer.ScopeKey));
            Assert.AreEqual(factCount, execution.Facts.Count);
            Assert.AreEqual(semanticCallCount, execution.SemanticCallCount);
            Assert.AreEqual(5, execution.AnalysisResult.Executions.Count);
            Assert.IsTrue(execution.OpenXmlPreflight.Values.All(item => item));
            Assert.IsTrue(execution.PrivacyPreflight.Values.All(item => item));
            Assert.IsTrue(execution.HashesBefore.Keys.All(documentId =>
                execution.HashesBefore[documentId] == execution.HashesAfter[documentId]));
        }

        [TestMethod]
        [DataRow("V2-GT-101")]
        [DataRow("V2-GT-102")]
        [DataRow("V2-GT-103")]
        [DataRow("V2-GT-104")]
        [DataRow("V2-GT-105")]
        [DataRow("V2-GT-201")]
        [DataRow("V2-GT-202")]
        [DataRow("V2-GT-203")]
        [DataRow("V2-GT-204")]
        [DataRow("V2-GT-301")]
        [DataRow("V2-GT-302")]
        [DataRow("V2-GT-303")]
        [DataRow("V2-GT-401")]
        [DataRow("V2-GT-402")]
        [DataRow("V2-GT-403")]
        public void ShadowResult_MatchesLegacyExpected(string caseId)
        {
            V2GoldenCaseManifestEntry manifestCase = GetManifestCase(caseId);
            V2GoldenCaseExecutionResult execution = new V2GoldenCaseRunner().Run(
                GetManifestPath(),
                caseId);
            AssertSuccessful(execution);

            var adapter = new V2GoldenLegacyExpectedAdapter();
            var failures = adapter.AssertShadow(
                Resolve(manifestCase.LegacyExpectedFile),
                execution);

            Assert.AreEqual(
                0,
                failures.Count,
                string.Join(Environment.NewLine, failures.Select(item => item.ToString())));
        }

        [TestMethod]
        public void OpenXmlPreflight_AcceptsEveryRegisteredInput()
        {
            var preflight = new V2GoldenCaseOpenXmlPreflight();
            V2GoldenCaseManifest manifest = new V2GoldenCaseJsonLoader()
                .LoadManifest(GetManifestPath());

            foreach (V2GoldenCaseInputDefinition input in manifest.Cases.SelectMany(item => item.Inputs))
            {
                var failures = preflight.Inspect(input.DocumentId, Resolve(input.File));
                Assert.AreEqual(
                    0,
                    failures.Count,
                    string.Join(Environment.NewLine, failures.Select(item => item.ToString())));
            }
        }

        [TestMethod]
        public void SemanticFactory_RecordsDeterministicMockCall()
        {
            var expectation = new V2GoldenCaseSemanticExpectation
            {
                Mode = "DeterministicMock",
                ExpectedCallCount = 1,
                ExpectedCalls = new[]
                {
                    new V2GoldenCaseSemanticCallAssertion
                    {
                        Order = 1,
                        FieldName = "Scope",
                        OriginalValue = "全部供配电资产",
                        CandidateValue = "供配电资产全部范围"
                    }
                },
                Responses = new[]
                {
                    new V2GoldenCaseSemanticResponseDefinition
                    {
                        FieldName = "Scope",
                        SimilarityScore = "0.95",
                        Confidence = "0.95",
                        Evidence = new[] { "offline-mock" }
                    }
                }
            };
            V2GoldenSemanticAnalyzerSession session =
                V2GoldenSemanticAnalyzerFactory.Create(expectation);

            var result = session.Analyze(
                "FieldName=Scope\nOriginalValue=全部供配电资产\nCandidateValue=供配电资产全部范围");

            Assert.AreEqual(1, session.CallCount);
            Assert.AreEqual("Scope", session.Calls.Single().FieldName);
            Assert.AreEqual(0.95m, result.SimilarityScore);
            Assert.AreEqual("offline-mock", result.Evidence.Single());
        }

        [TestMethod]
        public void Runner_ReturnsFailClosedFailureForUnknownCase()
        {
            V2GoldenCaseExecutionResult execution = new V2GoldenCaseRunner().Run(
                GetManifestPath(),
                "V2-GT-999");

            Assert.IsFalse(execution.IsSuccess);
            Assert.IsNull(execution.AnalysisResult);
            Assert.IsTrue(execution.Failures.Any(item => item.Stage == "Runner"));
        }

        private static void AssertSuccessful(V2GoldenCaseExecutionResult execution)
        {
            Assert.IsTrue(
                execution.IsSuccess,
                string.Join(Environment.NewLine, execution.Failures.Select(item => item.ToString())));
            Assert.IsNotNull(execution.AnalysisResult);
        }

        private static V2GoldenCaseManifestEntry GetManifestCase(string caseId)
        {
            return new V2GoldenCaseJsonLoader()
                .LoadManifest(GetManifestPath())
                .Cases.Single(item => item.CaseId == caseId);
        }

        private static string Resolve(string relativePath)
        {
            return Path.Combine(
                GetGoldenCaseRoot(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
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
