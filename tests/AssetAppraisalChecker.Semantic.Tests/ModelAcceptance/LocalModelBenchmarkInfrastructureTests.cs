using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    [TestClass]
    public sealed class LocalModelBenchmarkInfrastructureTests
    {
        [TestMethod]
        public void FakeAdapter_IsDeterministicAndProducesMockCandidatesOnly()
        {
            LoadedLocalModelEvaluationManifest manifest = BgeManifest();
            LocalSemanticAcceptanceCase item = Dataset().Dataset.Cases.Single(
                candidate => candidate.CaseId == "SEM-EVM-001");
            var adapter = new DeterministicFakeLocalModelAdapter(
                LocalModelAdapterProfile.BgeSmallZhV15(),
                manifest.Manifest.IntendedRevision);

            LocalModelAdapterObservation first = adapter.Analyze(item);
            LocalModelAdapterObservation second = adapter.Analyze(item);

            Assert.AreEqual(
                first.Analysis.CandidateSet.Identity,
                second.Analysis.CandidateSet.Identity);
            Assert.AreEqual(
                first.Analysis.CandidateSet.Candidates[0].Identity,
                second.Analysis.CandidateSet.Candidates[0].Identity);
            Assert.AreEqual(
                SemanticCandidateCreatedBy.DeterministicMock,
                first.Analysis.CandidateSet.Candidates[0].CreatedBy);
            Assert.AreEqual(5m, first.LatencyMilliseconds);
        }

        [TestMethod]
        public void Runner_ExecutesAdapterValidatorGateMetricsAndReportPipeline()
        {
            ModelAcceptanceReport report = RunBge();

            Assert.AreEqual(23, report.CaseResults.Count);
            Assert.IsTrue(report.CaseResults.All(item => item.Validation.IsValid));
            Assert.IsTrue(report.CaseResults.All(item => !item.Decision.FactPromotionAllowed));
            Assert.AreEqual(ModelAcceptanceGateStatus.Passed, report.SafetyGate);
            Assert.AreEqual(ModelAcceptanceGateStatus.Passed, report.SemanticGate);
            Assert.AreEqual(ModelAcceptanceGateStatus.Passed, report.HardNegativeGate);
            Assert.AreEqual(ModelAcceptanceGateStatus.Passed, report.AbstentionGate);
            Assert.AreEqual(ModelAcceptanceGateStatus.NotEvaluated, report.PerformanceGate);
            Assert.AreEqual(ModelAcceptanceGateStatus.NotEvaluated, report.OfflineGate);
            Assert.AreEqual(ModelAcceptanceGateStatus.Passed, report.LicenseGate);
            Assert.AreEqual(ModelFinalEligibility.Pending, report.FinalEligibility);
        }

        [TestMethod]
        public void Runner_UsesAcceptanceGateForHardRulesNegationSubjectAndAmbiguity()
        {
            Dictionary<string, LocalModelBenchmarkCaseResult> results = RunBge()
                .CaseResults.ToDictionary(item => item.CaseId, StringComparer.Ordinal);

            AssertReason(results, "SEM-EVM-101", SemanticCandidateAcceptanceReasonCode.HardRuleConflict);
            AssertReason(results, "SEM-EVM-103", SemanticCandidateAcceptanceReasonCode.NegationDetected);
            AssertReason(results, "SEM-EVM-106", SemanticCandidateAcceptanceReasonCode.SubjectConflict);
            AssertReason(results, "SEM-EVM-201", SemanticCandidateAcceptanceReasonCode.MultipleCandidates);
            AssertReason(results, "SEM-EVM-202", SemanticCandidateAcceptanceReasonCode.NoCandidate);
            AssertReason(results, "SEM-EVM-203", SemanticCandidateAcceptanceReasonCode.SubjectUnresolved);
            AssertReason(results, "SEM-AOB-202", SemanticCandidateAcceptanceReasonCode.LowModelConfidence);
            AssertReason(results, "SEM-SAFE-001", SemanticCandidateAcceptanceReasonCode.HardRuleProtectedType);
        }

        [TestMethod]
        public void Metrics_ReportAccuracyPrecisionRecallFalseRatesAndAbstention()
        {
            LocalModelBenchmarkMetrics metrics = RunBge().Metrics;

            Assert.AreEqual(23, metrics.TotalCases);
            Assert.AreEqual(1m, metrics.CandidateAccuracy);
            Assert.AreEqual(1m, metrics.Precision);
            Assert.AreEqual(1m, metrics.Recall);
            Assert.AreEqual(0m, metrics.FalsePositiveRate);
            Assert.AreEqual(0m, metrics.FalseNegativeRate);
            Assert.AreEqual(5m / 23m, metrics.AbstentionRate);
            Assert.AreEqual(0m, metrics.HardNegativeAcceptanceRate);
            Assert.IsTrue(metrics.AverageTop1Top2Margin.HasValue);
            Assert.AreEqual(5m, metrics.AverageLatencyMilliseconds);
            Assert.AreEqual(5m, metrics.P50LatencyMilliseconds);
            Assert.AreEqual(5m, metrics.P95LatencyMilliseconds);
            Assert.IsNull(metrics.PeakMemoryBytes);
            Assert.IsNull(metrics.ModelLoadTimeMilliseconds);
        }

        [TestMethod]
        public void Metrics_CountFalsePositiveAndHardNegativeAcceptanceSeparately()
        {
            LocalSemanticAcceptanceCase positive = Case(
                "SEM-TST-001",
                "Positive",
                "method/v1:cost-approach",
                false);
            LocalSemanticAcceptanceCase hardNegative = Case(
                "SEM-TST-002",
                "HardNegative",
                null,
                true);
            LocalModelBenchmarkCaseResult positiveResult = Result(
                positive,
                "method/v1:cost-approach",
                SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate);
            LocalModelBenchmarkCaseResult unsafeResult = Result(
                hardNegative,
                "method/v1:market-approach",
                SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate);

            LocalModelBenchmarkMetrics metrics = new LocalModelBenchmarkMetricsCalculator()
                .Calculate(
                    new[] { positive, hardNegative },
                    new[] { positiveResult, unsafeResult });

            Assert.AreEqual(0.5m, metrics.Precision);
            Assert.AreEqual(1m, metrics.Recall);
            Assert.AreEqual(1m, metrics.FalsePositiveRate);
            Assert.AreEqual(0m, metrics.FalseNegativeRate);
            Assert.AreEqual(1m, metrics.HardNegativeAcceptanceRate);
        }

        [TestMethod]
        public void FinalEligibility_RejectsAnyMandatoryGateFailureAndRequiresAllPass()
        {
            Assert.AreEqual(
                ModelFinalEligibility.Rejected,
                ModelAcceptanceReport.AggregateEligibility(new[]
                {
                    ModelAcceptanceGateStatus.Passed,
                    ModelAcceptanceGateStatus.Failed,
                    ModelAcceptanceGateStatus.NotEvaluated
                }));
            Assert.AreEqual(
                ModelFinalEligibility.Pending,
                ModelAcceptanceReport.AggregateEligibility(new[]
                {
                    ModelAcceptanceGateStatus.Passed,
                    ModelAcceptanceGateStatus.NotEvaluated
                }));
            Assert.AreEqual(
                ModelFinalEligibility.EligibleForNextStage,
                ModelAcceptanceReport.AggregateEligibility(new[]
                {
                    ModelAcceptanceGateStatus.Passed,
                    ModelAcceptanceGateStatus.Passed
                }));
        }

        [TestMethod]
        public void BenchmarkReport_IsDeterministicForFixedInputsAndTimestamp()
        {
            DateTimeOffset timestamp = new DateTimeOffset(
                2026,
                9,
                14,
                0,
                0,
                0,
                TimeSpan.Zero);
            ModelAcceptanceReport first = RunBge(timestamp);
            ModelAcceptanceReport second = RunBge(timestamp);

            Assert.AreEqual(Fingerprint(first), Fingerprint(second));
            Assert.IsTrue(LocalModelAcceptanceHash.IsSha256(first.DatasetHash));
            Assert.IsTrue(LocalModelAcceptanceHash.IsSha256(first.ModelManifestHash));
            Assert.IsNull(first.ModelFileHash);
            StringAssert.Contains(first.RuntimeVersion, "deterministic-fake-runtime");
            StringAssert.Contains(first.RuntimeVersion, "no-onnx");
        }

        [TestMethod]
        public void BgeAndE5AdapterProfiles_KeepPrefixAndPoolingRulesIsolated()
        {
            LocalModelAdapterProfile bge = LocalModelAdapterProfile.BgeSmallZhV15();
            LocalModelAdapterProfile e5 = LocalModelAdapterProfile.MultilingualE5Small();

            StringAssert.StartsWith(
                bge.PrepareQuery("示例"),
                "为这个句子生成表示以用于检索相关文章：");
            Assert.AreEqual("示例", bge.PreparePassage("示例"));
            Assert.AreEqual("CLS+L2", bge.PoolingStrategy);
            Assert.AreEqual("query: 示例", e5.PrepareQuery("示例"));
            Assert.AreEqual("passage: 示例", e5.PreparePassage("示例"));
            Assert.AreEqual("MeanPooling+L2", e5.PoolingStrategy);
            Assert.AreNotEqual(bge.AdapterVersion, e5.AdapterVersion);
        }

        [TestMethod]
        public void E5Manifest_CanRunThroughSameModelIndependentFakeHarness()
        {
            LoadedLocalModelEvaluationManifest manifest =
                new LocalModelEvaluationManifestLoader().Load(
                    LocalModelAcceptanceTestPaths.E5Manifest);
            var runner = new LocalModelBenchmarkRunner(
                new DeterministicFakeLocalModelAdapter(
                    LocalModelAdapterProfile.MultilingualE5Small(),
                    manifest.Manifest.IntendedRevision));

            ModelAcceptanceReport report = runner.Run(
                manifest,
                Dataset(),
                FixedTimestamp(),
                "synthetic-test-machine/no-real-model");

            Assert.AreEqual("intfloat/multilingual-e5-small", report.ModelId);
            Assert.AreEqual(23, report.CaseResults.Count);
            Assert.AreEqual(ModelFinalEligibility.Pending, report.FinalEligibility);
        }

        [TestMethod]
        public void OfflineContract_RequiresControlledAbsolutePathAndDisablesOnlineBehavior()
        {
            string controlledRoot = Path.Combine(Path.GetTempPath(), "aac-models-controlled");
            var contract = new LocalModelOfflineVerificationContract(controlledRoot);

            contract.ValidateModelPath(Path.Combine(controlledRoot, "bge-small"));
            Assert.Throws<InvalidDataException>(() => contract.ValidateModelPath(
                Path.GetFullPath(Path.Combine(controlledRoot, "..", "outside-model"))));
            Assert.IsTrue(contract.RequireLocalAbsolutePath);
            Assert.IsFalse(contract.AllowNetwork);
            Assert.IsFalse(contract.AllowAutoDownload);
            Assert.IsFalse(contract.AllowTelemetry);
            Assert.IsFalse(contract.AllowOnlineUpdate);
            Assert.IsTrue(contract.RequireTokenizerInitialization);
            Assert.IsTrue(contract.RequireRepeatedInference);
            Assert.AreEqual(3, contract.RequiredRepeatCount);
            Assert.IsTrue(contract.RequireArtifactHashBeforeAndAfter);
        }

        [TestMethod]
        public void SemanticAssembly_HasNoCandidateToBusinessFactPromotionMapper()
        {
            Type candidateType = typeof(SemanticCandidate);
            Type analysisType = typeof(SemanticCandidateAnalysis);
            MethodInfo[] promotionMethods = candidateType.Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly))
                .Where(method => typeof(BusinessFact).IsAssignableFrom(method.ReturnType))
                .Where(method => method.GetParameters().Any(parameter =>
                    parameter.ParameterType == candidateType
                    || parameter.ParameterType == analysisType))
                .ToArray();

            Assert.HasCount(0, promotionMethods);
        }

        private static LoadedLocalModelEvaluationManifest BgeManifest()
        {
            return new LocalModelEvaluationManifestLoader().Load(
                LocalModelAcceptanceTestPaths.BgeManifest);
        }

        private static LoadedLocalSemanticAcceptanceDataset Dataset()
        {
            return new LocalSemanticAcceptanceDatasetLoader().Load(
                LocalModelAcceptanceTestPaths.AcceptanceDataset);
        }

        private static ModelAcceptanceReport RunBge()
        {
            return RunBge(FixedTimestamp());
        }

        private static ModelAcceptanceReport RunBge(DateTimeOffset timestamp)
        {
            LoadedLocalModelEvaluationManifest manifest = BgeManifest();
            var adapter = new DeterministicFakeLocalModelAdapter(
                LocalModelAdapterProfile.BgeSmallZhV15(),
                manifest.Manifest.IntendedRevision);
            return new LocalModelBenchmarkRunner(adapter).Run(
                manifest,
                Dataset(),
                timestamp,
                "synthetic-test-machine/no-real-model");
        }

        private static DateTimeOffset FixedTimestamp()
        {
            return new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
        }

        private static string Fingerprint(ModelAcceptanceReport report)
        {
            return string.Join("|", new[]
            {
                report.DatasetVersion,
                report.DatasetHash,
                report.ModelId,
                report.Revision,
                report.ModelManifestHash,
                report.AdapterVersion,
                report.RuntimeVersion,
                report.Quantization,
                report.MachineInfoSummary,
                report.RunTimestamp.ToString("O"),
                report.FinalEligibility.ToString(),
                string.Join(",", report.CaseResults.Select(item =>
                    item.CaseId + ":" + item.Decision.Decision + ":" + item.ActualCandidate))
            });
        }

        private static void AssertReason(
            IReadOnlyDictionary<string, LocalModelBenchmarkCaseResult> results,
            string caseId,
            SemanticCandidateAcceptanceReasonCode expected)
        {
            Assert.AreEqual(expected, results[caseId].Decision.ReasonCode, caseId);
        }

        private static LocalSemanticAcceptanceCase Case(
            string caseId,
            string category,
            string expectedCandidate,
            bool isHardNegative)
        {
            return new LocalSemanticAcceptanceCase
            {
                CaseId = caseId,
                Category = category,
                ExpectedCandidate = expectedCandidate,
                ExpectedDecisionClass = expectedCandidate == null
                    ? "Rejected"
                    : "AcceptedCandidate",
                IsHardNegative = isHardNegative
            };
        }

        private static LocalModelBenchmarkCaseResult Result(
            LocalSemanticAcceptanceCase item,
            string actualCandidate,
            SemanticCandidateAcceptanceDecisionKind decisionKind)
        {
            var setIdentity = new SemanticCandidateSetIdentity(
                SemanticCandidateType.EvaluationMethod,
                "subject/v1:test",
                "loc/v1:test:" + item.CaseId.ToLowerInvariant(),
                SemanticCandidateRole.AdoptedMethod);
            var candidateIdentity = new SemanticCandidateIdentity(
                setIdentity,
                actualCandidate);
            var decision = new SemanticCandidateAcceptanceDecision(
                decisionKind,
                SemanticCandidateAcceptanceReasonCode.Accepted,
                setIdentity,
                candidateIdentity,
                null,
                new[] { "synthetic-safe-evidence" });
            var validation = new SemanticCandidateContractValidationResult(
                true,
                new[] { SemanticCandidateContractValidationReasonCode.Valid });
            return new LocalModelBenchmarkCaseResult(
                item.CaseId,
                item.Category,
                item.ExpectedCandidate,
                actualCandidate,
                decision,
                validation,
                5m,
                null);
        }
    }
}
