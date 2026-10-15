using System.Linq;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.LocalRuntime.Tests
{
    [TestClass]
    public sealed class SemanticRankingAcceptanceGateTests
    {
        private const string Hash =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [TestMethod]
        public void EvaluateRanked_ActiveResolvableAcceptsPriorityEvidenceOnly()
        {
            SemanticCandidateRankingAcceptanceDecision decision = Gate().EvaluateRanked(
                Ranking(SemanticCandidateResolution.Resolvable, false),
                Context(false, false));

            Assert.AreEqual(
                SemanticCandidateRankingAcceptanceDecisionKind.AcceptedEvidence,
                decision.Decision);
            Assert.IsNotNull(decision.PriorityCandidateIdentity);
            Assert.IsFalse(decision.FactPromotionAllowed);
        }

        [TestMethod]
        public void EvaluateRanked_NoValidCandidateAbstains()
        {
            SemanticCandidateRankingAcceptanceDecision decision = Gate().EvaluateRanked(
                EmptyRanking(SemanticCandidateResolution.NoValidCandidate),
                Context(false, false));

            Assert.AreEqual(
                SemanticCandidateRankingAcceptanceDecisionKind.Abstained,
                decision.Decision);
            Assert.AreEqual(
                SemanticCandidateRankingAcceptanceReasonCode.NoValidCandidate,
                decision.ReasonCode);
        }

        [TestMethod]
        public void EvaluateRanked_HighRankingCannotOverrideNegationOrHardConflict()
        {
            SemanticCandidateAcceptanceGate gate = Gate();
            SemanticCandidateRankingResult ranking = Ranking(
                SemanticCandidateResolution.Resolvable,
                false);

            SemanticCandidateRankingAcceptanceDecision negation = gate.EvaluateRanked(
                ranking,
                Context(true, false));
            SemanticCandidateRankingAcceptanceDecision conflict = gate.EvaluateRanked(
                ranking,
                Context(false, true));

            Assert.AreEqual(
                SemanticCandidateRankingAcceptanceReasonCode.NegationDetected,
                negation.ReasonCode);
            Assert.AreEqual(
                SemanticCandidateRankingAcceptanceReasonCode.HardRuleConflict,
                conflict.ReasonCode);
            Assert.IsTrue(new[] { negation, conflict }.All(item =>
                item.Decision == SemanticCandidateRankingAcceptanceDecisionKind.Rejected
                && !item.FactPromotionAllowed));
        }

        private static SemanticCandidateAcceptanceGate Gate()
        {
            return new SemanticCandidateAcceptanceGate();
        }

        private static SemanticCandidateRankingAcceptanceContext Context(
            bool negation,
            bool conflict)
        {
            return new SemanticCandidateRankingAcceptanceContext(
                true,
                "subject/v1:test",
                negation,
                conflict,
                true);
        }

        private static SemanticCandidateRankingResult Ranking(
            SemanticCandidateResolution resolution,
            bool shadowOnly)
        {
            SemanticCandidateSetIdentity set = Identity();
            var item = new SemanticCandidateRankingItem(
                new SemanticCandidateIdentity(set, "candidate"),
                "candidate",
                0.99,
                1,
                99.0,
                1);
            return new SemanticCandidateRankingResult(
                "request/v1:test",
                set,
                new SemanticCandidateSelectionContext(resolution, shadowOnly),
                SemanticCandidateRankingStatus.Ranked,
                new[] { item },
                "embedding",
                "revision",
                Hash,
                "reranker",
                "revision",
                Hash,
                "test");
        }

        private static SemanticCandidateRankingResult EmptyRanking(
            SemanticCandidateResolution resolution)
        {
            return new SemanticCandidateRankingResult(
                "request/v1:test",
                Identity(),
                new SemanticCandidateSelectionContext(resolution, false),
                SemanticCandidateRankingStatus.Abstained,
                new SemanticCandidateRankingItem[0],
                "embedding",
                "revision",
                Hash,
                "reranker",
                "revision",
                Hash,
                "NoValidCandidate");
        }

        private static SemanticCandidateSetIdentity Identity()
        {
            return new SemanticCandidateSetIdentity(
                SemanticCandidateType.EvaluationMethod,
                "subject/v1:test",
                "loc/v1:test",
                SemanticCandidateRole.AdoptedMethod);
        }
    }
}
