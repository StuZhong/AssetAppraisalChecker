using System;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class EvaluationMethodSemanticCandidateContractTests
    {
        [TestMethod]
        [DataRow("按现行重建成本测算", "重置成本法", "method/v1:replacement-cost-method")]
        [DataRow("参照近期市场成交案例", "市场法", "method/v1:market-approach")]
        [DataRow("根据未来收益折现测算", "收益法", "method/v1:income-approach")]
        public void NonStandardExpression_MockProducesCandidateOnly(
            string originalText,
            string candidateValue,
            string normalizedCandidate)
        {
            string requestId = "request/" + normalizedCandidate;
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                normalizedCandidate,
                candidateValue,
                0.95m,
                0.93m,
                identity,
                originalText);
            SemanticCandidateAnalysis expected = SemanticCandidateTestData.Analysis(
                requestId,
                identity,
                candidate);
            var analyzer = new DeterministicMockSemanticCandidateAnalyzer(
                new[]
                {
                    new DeterministicMockSemanticCandidateScenario(requestId, expected)
                });

            SemanticCandidateAnalysis actual = analyzer.Analyze(
                SemanticCandidateTestData.Request(
                    requestId,
                    identity,
                    new[] { normalizedCandidate }));

            Assert.HasCount(1, actual.CandidateSet.Candidates);
            Assert.AreEqual(normalizedCandidate,
                actual.CandidateSet.Candidates[0].NormalizedCandidate);
            Assert.IsFalse(typeof(EvaluationMethodFact).IsAssignableFrom(
                actual.CandidateSet.Candidates[0].GetType()));
        }

        [TestMethod]
        public void CorrectCandidate_WithUnresolvedSubject_IsNotAccepted()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity(
                subjectKey: SemanticCandidateAcceptanceGate.UnresolvedSubjectKey);
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                setIdentity: identity);

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis(
                    "request/method-subject-unresolved",
                    identity,
                    candidate),
                SemanticCandidateTestData.Context());

            Assert.AreNotEqual(
                SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate,
                decision.Decision);
            Assert.IsFalse(decision.FactPromotionAllowed);
        }

        [TestMethod]
        public void NegatedIncomeMethod_IsRejectedRegardlessOfMockConfidence()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                "method/v1:income-approach",
                "收益法",
                0.99m,
                0.99m,
                identity,
                "不采用收益法");

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis("request/negated-income", identity, candidate),
                SemanticCandidateTestData.Context(hasNegation: true));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.NegationDetected,
                decision.ReasonCode);
        }

        [TestMethod]
        public void MarketVersusIncomeHardConflict_CannotBeChangedByCandidate()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                "method/v1:income-approach",
                "收益法",
                0.99m,
                0.99m,
                identity);

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis(
                    "request/hard-method-conflict",
                    identity,
                    candidate),
                SemanticCandidateTestData.Context(hasHardRuleConflict: true));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Rejected, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.HardRuleConflict,
                decision.ReasonCode);
            Assert.IsFalse(decision.FactPromotionAllowed);
        }

        [TestMethod]
        public void CompetingMethodsWithInsufficientMargin_RemainUnknown()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate cost = SemanticCandidateTestData.Candidate(
                modelConfidence: 0.84m,
                setIdentity: identity);
            SemanticCandidate market = SemanticCandidateTestData.Candidate(
                "method/v1:market-approach",
                "市场法",
                0.82m,
                0.92m,
                identity);

            SemanticCandidateAcceptanceDecision decision = Gate().Evaluate(
                SemanticCandidateTestData.Analysis(
                    "request/competing-methods",
                    identity,
                    cost,
                    market),
                SemanticCandidateTestData.Context(
                    threshold: SemanticCandidateTestData.Threshold(
                        confidence: 0.80m,
                        margin: 0.10m)));

            Assert.AreEqual(SemanticCandidateAcceptanceDecisionKind.Warning, decision.Decision);
            Assert.AreEqual(
                SemanticCandidateAcceptanceReasonCode.MultipleCandidates,
                decision.ReasonCode);
            Assert.AreEqual(0.02m, decision.CandidateMargin);
        }

        [TestMethod]
        public void ProductionAssemblyContainsNoCandidateToBusinessFactPromoter()
        {
            Type[] exportedTypes = typeof(SemanticCandidate).Assembly.GetExportedTypes();
            foreach (Type type in exportedTypes)
            {
                foreach (System.Reflection.MethodInfo method in type.GetMethods())
                {
                    Assert.IsFalse(
                        typeof(BusinessFact).IsAssignableFrom(method.ReturnType),
                        "Semantic API must not return BusinessFact: "
                            + type.FullName + "." + method.Name);
                }
            }
        }

        private static SemanticCandidateAcceptanceGate Gate()
        {
            return new SemanticCandidateAcceptanceGate();
        }
    }
}
