using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class SemanticCandidateAnalyzerAndValidatorTests
    {
        [TestMethod]
        public void Analyzer_SameRequestReturnsSameDeterministicAnalysis()
        {
            const string requestId = "request/evaluation-method/high";
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateAnalysis expected = SemanticCandidateTestData.Analysis(
                requestId,
                identity,
                SemanticCandidateTestData.Candidate(setIdentity: identity));
            var analyzer = Analyzer(requestId, expected);
            SemanticCandidateRequest request = SemanticCandidateTestData.Request(
                requestId,
                identity,
                new[] { "method/v1:cost-approach" });

            SemanticCandidateAnalysis first = analyzer.Analyze(request);
            SemanticCandidateAnalysis second = analyzer.Analyze(request);

            Assert.AreSame(expected, first);
            Assert.AreSame(first, second);
            Assert.AreEqual(2, analyzer.CallCount);
        }

        [TestMethod]
        public void Analyzer_SupportsHighLowMultipleAndZeroCandidates()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate high = SemanticCandidateTestData.Candidate(
                modelConfidence: 0.95m,
                setIdentity: identity);
            SemanticCandidate low = SemanticCandidateTestData.Candidate(
                modelConfidence: 0.40m,
                setIdentity: identity);
            SemanticCandidate market = SemanticCandidateTestData.Candidate(
                "method/v1:market-approach",
                "市场法",
                0.82m,
                0.80m,
                identity);

            Assert.HasCount(1, Analyze("high", identity, high).CandidateSet.Candidates);
            Assert.AreEqual(0.40m,
                Analyze("low", identity, low).CandidateSet.Candidates[0].ModelConfidence);
            Assert.HasCount(2,
                Analyze("multiple", identity, high, market).CandidateSet.Candidates);
            Assert.HasCount(0, Analyze("zero", identity).CandidateSet.Candidates);
        }

        [TestMethod]
        public void Analyzer_UnconfiguredRequestAbstainsAsUnsupportedText()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            var analyzer = new DeterministicMockSemanticCandidateAnalyzer(
                new DeterministicMockSemanticCandidateScenario[0]);

            SemanticCandidateAnalysis analysis = analyzer.Analyze(
                SemanticCandidateTestData.Request(
                    "request/unsupported",
                    identity,
                    new[] { "method/v1:cost-approach" }));

            Assert.AreEqual(SemanticCandidateAnalyzerStatus.Abstained, analysis.Status);
            Assert.AreEqual(
                SemanticCandidateFailureCode.UnsupportedText,
                analysis.Failure.FailureCode);
            Assert.HasCount(0, analysis.CandidateSet.Candidates);
        }

        [TestMethod]
        [DataRow(SemanticCandidateFailureCode.Timeout)]
        [DataRow(SemanticCandidateFailureCode.ModelLoadFailure)]
        [DataRow(SemanticCandidateFailureCode.InvalidOutput)]
        public void Analyzer_SupportsControlledTechnicalFailures(
            SemanticCandidateFailureCode code)
        {
            const string requestId = "request/failure";
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateAnalysis failed = SemanticCandidateAnalysis.Failed(
                requestId,
                new SemanticCandidateSet(identity, new SemanticCandidate[0]),
                code,
                "mock.controlled-failure");

            SemanticCandidateAnalysis actual = Analyzer(requestId, failed).Analyze(
                SemanticCandidateTestData.Request(
                    requestId,
                    identity,
                    new[] { "method/v1:cost-approach" }));

            Assert.AreEqual(SemanticCandidateAnalyzerStatus.Failed, actual.Status);
            Assert.AreEqual(code, actual.Failure.FailureCode);
            Assert.HasCount(0, actual.CandidateSet.Candidates);
        }

        [TestMethod]
        [DataRow(SemanticCandidateFailureCode.LowConfidence)]
        [DataRow(SemanticCandidateFailureCode.MultipleCandidates)]
        [DataRow(SemanticCandidateFailureCode.UnsupportedText)]
        public void Analyzer_SupportsControlledAbstentionReasons(
            SemanticCandidateFailureCode code)
        {
            SemanticCandidateAnalysis analysis = SemanticCandidateAnalysis.Abstained(
                "request/abstain",
                new SemanticCandidateSet(
                    SemanticCandidateTestData.SetIdentity(),
                    new SemanticCandidate[0]),
                code,
                "mock.controlled-abstention");

            Assert.AreEqual(SemanticCandidateAnalyzerStatus.Abstained, analysis.Status);
            Assert.AreEqual(code, analysis.Failure.FailureCode);
        }

        [TestMethod]
        public void Analyzer_RejectsCandidateOutsideRequestAllowListAsInvalidOutput()
        {
            const string requestId = "request/not-allowed";
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate market = SemanticCandidateTestData.Candidate(
                "method/v1:market-approach",
                "市场法",
                setIdentity: identity);
            DeterministicMockSemanticCandidateAnalyzer analyzer = Analyzer(
                requestId,
                SemanticCandidateTestData.Analysis(requestId, identity, market));

            SemanticCandidateAnalysis actual = analyzer.Analyze(
                SemanticCandidateTestData.Request(
                    requestId,
                    identity,
                    new[] { "method/v1:cost-approach" }));

            Assert.AreEqual(SemanticCandidateAnalyzerStatus.Failed, actual.Status);
            Assert.AreEqual(
                SemanticCandidateFailureCode.InvalidOutput,
                actual.Failure.FailureCode);
            Assert.HasCount(0, actual.CandidateSet.Candidates);
        }

        [TestMethod]
        public void Validator_AcceptsCompleteContractAndRejectsInvalidOutput()
        {
            var validator = new SemanticCandidateValidator();
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateAnalysis valid = SemanticCandidateTestData.Analysis(
                "request/valid",
                identity,
                SemanticCandidateTestData.Candidate(setIdentity: identity));
            SemanticCandidateAnalysis invalid = SemanticCandidateAnalysis.Failed(
                "request/invalid",
                new SemanticCandidateSet(identity, new SemanticCandidate[0]),
                SemanticCandidateFailureCode.InvalidOutput,
                "mock.invalid-output");

            Assert.IsTrue(validator.Validate(valid).IsValid);
            SemanticCandidateContractValidationResult rejected = validator.Validate(invalid);
            Assert.IsFalse(rejected.IsValid);
            CollectionAssert.Contains(
                new System.Collections.Generic.List<SemanticCandidateContractValidationReasonCode>(
                    rejected.ReasonCodes),
                SemanticCandidateContractValidationReasonCode.InvalidOutput);
        }

        [TestMethod]
        public void FailureAndRequestDiagnostics_DoNotExposeOriginalEvidence()
        {
            const string sensitive = "完整客户敏感信息13800138000";
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            var request = new SemanticCandidateRequest(
                "request/privacy",
                identity,
                new[] { SemanticCandidateTestData.Evidence(sensitive) },
                new[] { "method/v1:cost-approach" },
                "evaluation-method/v1",
                "candidate-mock-policy/v1",
                true);
            var failure = new SemanticCandidateFailure(
                SemanticCandidateFailureCode.Timeout,
                "mock.timeout");

            Assert.IsFalse(request.ToString().Contains(sensitive));
            Assert.IsFalse(failure.ToString().Contains(sensitive));
            StringAssert.Contains(request.ToString(), request.Evidence[0].EvidenceHash);
        }

        private static SemanticCandidateAnalysis Analyze(
            string requestId,
            SemanticCandidateSetIdentity identity,
            params SemanticCandidate[] candidates)
        {
            SemanticCandidateAnalysis expected = SemanticCandidateTestData.Analysis(
                requestId,
                identity,
                candidates);
            return Analyzer(requestId, expected).Analyze(
                SemanticCandidateTestData.Request(
                    requestId,
                    identity,
                    new[]
                    {
                        "method/v1:cost-approach",
                        "method/v1:market-approach"
                    }));
        }

        private static DeterministicMockSemanticCandidateAnalyzer Analyzer(
            string requestId,
            SemanticCandidateAnalysis analysis)
        {
            return new DeterministicMockSemanticCandidateAnalyzer(
                new[]
                {
                    new DeterministicMockSemanticCandidateScenario(requestId, analysis)
                });
        }
    }
}
