using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class SemanticCandidateAcceptanceGateTests
    {
        [TestMethod]
        public void HighConfidenceCandidate_IsAcceptedButNeverPromotedToFact()
        {
            SemanticCandidateAcceptanceDecision decision = Evaluate(
                SemanticCandidateTestData.Candidate());

            Assert.AreEqual(
                SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate,
                decision.Decision);
            Assert.AreEqual(SemanticCandidateAcceptanceReasonCode.Accepted, decision.ReasonCode);
            Assert.IsFalse(decision.FactPromotionAllowed);
            Assert.IsNotNull(decision.CandidateIdentity);
        }

        [TestMethod]
        public void LowConfidenceCandidate_FailsClosedAsWarning()
        {
            SemanticCandidateAcceptanceDecision decision = Evaluate(
                SemanticCandidateTestData.Candidate(modelConfidence: 0.60m));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Warning, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.LowModelConfidence,
                decision.ReasonCode);
        }

        [TestMethod]
        public void CloseCompetingCandidates_DoNotSelectTopOne()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate cost = SemanticCandidateTestData.Candidate(
                modelConfidence: 0.84m,
                similarityScore: 0.92m,
                setIdentity: identity);
            SemanticCandidate market = SemanticCandidateTestData.Candidate(
                "method/v1:market-approach",
                "市场法",
                0.82m,
                0.91m,
                identity);
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis("request/ambiguous", identity, cost, market),
                SemanticCandidateTestData.Context(
                    threshold: SemanticCandidateTestData.Threshold(
                        confidence: 0.80m,
                        similarity: 0.85m,
                        margin: 0.10m)));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Warning, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.MultipleCandidates,
                decision.ReasonCode);
            Assert.AreEqual(0.02m, decision.CandidateMargin);
            Assert.IsFalse(decision.FactPromotionAllowed);
        }

        [TestMethod]
        public void ZeroCandidate_FailsClosedAsWarning()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis("request/zero", identity),
                SemanticCandidateTestData.Context());

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Warning, decision.Decision);
            Assert.AreEqual(SemanticCandidateAcceptanceReasonCode.NoCandidate, decision.ReasonCode);
        }

        [TestMethod]
        public void SubjectMissing_CannotBeAccepted()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity(
                subjectKey: SemanticCandidateAcceptanceGate.UnresolvedSubjectKey);
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis(
                    "request/subject-missing",
                    identity,
                    SemanticCandidateTestData.Candidate(setIdentity: identity)),
                SemanticCandidateTestData.Context());

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Warning, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.SubjectUnresolved,
                decision.ReasonCode);
        }

        [TestMethod]
        public void SubjectConflict_CannotBeRepairedBySemantic()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity(
                subjectKey: "subject/v1:machinery-equipment");
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis(
                    "request/subject-conflict",
                    identity,
                    SemanticCandidateTestData.Candidate(setIdentity: identity)),
                SemanticCandidateTestData.Context());

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.SubjectConflict,
                decision.ReasonCode);
        }

        [TestMethod]
        public void Negation_BlocksEvenVeryHighConfidenceCandidate()
        {
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                Analysis(SemanticCandidateTestData.Candidate(
                    "method/v1:income-approach",
                    "收益法",
                    0.99m,
                    0.99m,
                    SemanticCandidateTestData.SetIdentity())),
                SemanticCandidateTestData.Context(hasNegation: true));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.NegationDetected,
                decision.ReasonCode);
        }

        [TestMethod]
        public void HardRuleConflict_BlocksEvenVeryHighConfidenceCandidate()
        {
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                Analysis(SemanticCandidateTestData.Candidate(
                    "method/v1:income-approach",
                    "收益法",
                    0.99m,
                    0.99m,
                    SemanticCandidateTestData.SetIdentity())),
                SemanticCandidateTestData.Context(hasHardRuleConflict: true));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.HardRuleConflict,
                decision.ReasonCode);
        }

        [TestMethod]
        [DataRow(SemanticCandidateType.AssessmentValue, SemanticCandidateRole.AssessmentValue)]
        [DataRow(SemanticCandidateType.BenchmarkDate, SemanticCandidateRole.BenchmarkDate)]
        [DataRow(SemanticCandidateType.ReportNumber, SemanticCandidateRole.ReportNumber)]
        [DataRow(SemanticCandidateType.Quantity, SemanticCandidateRole.Quantity)]
        [DataRow(SemanticCandidateType.Owner, SemanticCandidateRole.Owner)]
        public void HardRuleProtectedCandidateTypes_AreAlwaysRejected(
            SemanticCandidateType type,
            SemanticCandidateRole role)
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity(
                type,
                SemanticCandidateTestData.SubjectKey,
                SemanticCandidateTestData.LocationKey,
                role);
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                "value/v1:test",
                "候选值",
                0.99m,
                0.99m,
                identity);

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis("request/protected", identity, candidate),
                SemanticCandidateTestData.Context());

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.HardRuleProtectedType,
                decision.ReasonCode);
        }

        [TestMethod]
        public void SimilarityScore_IsIndependentFromConfidenceThreshold()
        {
            SemanticCandidate noSimilarity = SemanticCandidateTestData.Candidate(
                modelConfidence: 0.99m,
                similarityScore: null);
            SemanticCandidateAcceptanceDecision missing = Evaluate(noSimilarity);
            SemanticCandidate lowSimilarity = SemanticCandidateTestData.Candidate(
                modelConfidence: 0.99m,
                similarityScore: 0.20m);
            SemanticCandidateAcceptanceDecision low = Evaluate(lowSimilarity);

            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.SimilarityScoreMissing,
                missing.ReasonCode);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.SimilarityThresholdNotMet,
                low.ReasonCode);
        }

        [TestMethod]
        public void TechnicalFailure_ReturnsWarningWithoutCandidateOrFact()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateAnalysis failed = SemanticCandidateAnalysis.Failed(
                "request/model-failure",
                new SemanticCandidateSet(identity, new SemanticCandidate[0]),
                SemanticCandidateFailureCode.ModelLoadFailure,
                "model.load-failed");

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                failed,
                SemanticCandidateTestData.Context());

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Warning, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.AnalyzerFailure,
                decision.ReasonCode);
            Assert.IsNull(decision.CandidateIdentity);
            Assert.IsFalse(decision.FactPromotionAllowed);
        }

        [TestMethod]
        public void InvalidOutput_IsRejectedByContractBeforeScoring()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateAnalysis invalid = SemanticCandidateAnalysis.Failed(
                "request/invalid-output",
                new SemanticCandidateSet(identity, new SemanticCandidate[0]),
                SemanticCandidateFailureCode.InvalidOutput,
                "model.invalid-output");

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                invalid,
                SemanticCandidateTestData.Context());

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.ContractInvalid,
                decision.ReasonCode);
        }

        [TestMethod]
        public void EvidenceInsufficient_PreventsAcceptance()
        {
            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                Analysis(SemanticCandidateTestData.Candidate()),
                SemanticCandidateTestData.Context(evidenceSufficient: false));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.EvidenceInsufficient,
                decision.ReasonCode);
        }

        private static SemanticCandidateAcceptanceDecision Evaluate(
            SemanticCandidate candidate)
        {
            return Gate().Evaluate(
                Analysis(candidate),
                SemanticCandidateTestData.Context());
        }

        private static SemanticCandidateAnalysis Analysis(SemanticCandidate candidate)
        {
            return SemanticCandidateTestData.Analysis(
                "request/gate",
                candidate.Identity.SetIdentity,
                candidate);
        }

        private static SemanticCandidateAcceptanceGate Gate()
        {
            return new SemanticCandidateAcceptanceGate();
        }
    }
}
