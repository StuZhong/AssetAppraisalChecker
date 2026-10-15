using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class SemanticCandidateIdentityAndSetTests
    {
        [TestMethod]
        public void Identities_UseOrdinalValueEqualityAndStableHash()
        {
            SemanticCandidateSetIdentity firstSet = SemanticCandidateTestData.SetIdentity();
            SemanticCandidateSetIdentity secondSet = SemanticCandidateTestData.SetIdentity();
            var first = new SemanticCandidateIdentity(firstSet, "method/v1:cost-approach");
            var second = new SemanticCandidateIdentity(secondSet, "method/v1:cost-approach");

            Assert.AreEqual(firstSet, secondSet);
            Assert.AreEqual(firstSet.GetHashCode(), secondSet.GetHashCode());
            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
            Assert.AreEqual(first.CanonicalValue, second.CanonicalValue);
        }

        [TestMethod]
        public void Identity_DoesNotContainEvidenceOrRandomGuid()
        {
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                originalText: "完整敏感业务原文不得进入身份");

            Assert.IsFalse(candidate.Identity.CanonicalValue.Contains("完整敏感业务原文"));
            Assert.IsFalse(Guid.TryParse(candidate.Identity.CanonicalValue, out _));
            StringAssert.Contains(candidate.Identity.CanonicalValue, SemanticCandidateTestData.SubjectKey);
            StringAssert.Contains(candidate.Identity.CanonicalValue, SemanticCandidateTestData.LocationKey);
        }

        [TestMethod]
        public void CandidateSet_IsOrderIndependentAndDeterministicallySorted()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate market = SemanticCandidateTestData.Candidate(
                "method/v1:market-approach",
                "市场法",
                0.82m,
                0.80m,
                identity);
            SemanticCandidate cost = SemanticCandidateTestData.Candidate(
                setIdentity: identity);

            var first = new SemanticCandidateSet(identity, new[] { market, cost });
            var second = new SemanticCandidateSet(identity, new[] { cost, market });

            CollectionAssert.AreEqual(
                first.Candidates.Select(item => item.Identity.CanonicalValue).ToArray(),
                second.Candidates.Select(item => item.Identity.CanonicalValue).ToArray());
        }

        [TestMethod]
        public void CandidateSet_SupportsZeroToManyCandidatesAndIsReadOnly()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            var empty = new SemanticCandidateSet(identity, new SemanticCandidate[0]);
            var many = new SemanticCandidateSet(identity, new[]
            {
                SemanticCandidateTestData.Candidate(setIdentity: identity),
                SemanticCandidateTestData.Candidate(
                    "method/v1:market-approach",
                    "市场法",
                    0.82m,
                    0.80m,
                    identity)
            });

            Assert.HasCount(0, empty.Candidates);
            Assert.HasCount(2, many.Candidates);
            Assert.IsFalse(many.Candidates is IList<SemanticCandidate> list && !list.IsReadOnly);
        }

        [TestMethod]
        public void CandidateSet_RejectsDuplicateCandidateIdentity()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate first = SemanticCandidateTestData.Candidate(setIdentity: identity);
            SemanticCandidate duplicate = SemanticCandidateTestData.Candidate(setIdentity: identity);

            Assert.ThrowsExactly<ArgumentException>(() =>
                new SemanticCandidateSet(identity, new[] { first, duplicate }));
        }

        [TestMethod]
        public void CandidateSet_RejectsCandidateFromDifferentSet()
        {
            SemanticCandidateSetIdentity identity = SemanticCandidateTestData.SetIdentity();
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                setIdentity: SemanticCandidateTestData.SetIdentity(
                    subjectKey: "subject/v1:machinery-equipment"));

            Assert.ThrowsExactly<ArgumentException>(() =>
                new SemanticCandidateSet(identity, new[] { candidate }));
        }

        [TestMethod]
        public void CandidateSetIdentity_RejectsUnknownTypeRoleAndMissingKeys()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new SemanticCandidateSetIdentity(
                    SemanticCandidateType.Unknown,
                    SemanticCandidateTestData.SubjectKey,
                    SemanticCandidateTestData.LocationKey,
                    SemanticCandidateRole.AdoptedMethod));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new SemanticCandidateSetIdentity(
                    SemanticCandidateType.EvaluationMethod,
                    SemanticCandidateTestData.SubjectKey,
                    SemanticCandidateTestData.LocationKey,
                    SemanticCandidateRole.Unknown));
            Assert.ThrowsExactly<ArgumentException>(() =>
                new SemanticCandidateSetIdentity(
                    SemanticCandidateType.EvaluationMethod,
                    " ",
                    SemanticCandidateTestData.LocationKey,
                    SemanticCandidateRole.AdoptedMethod));
        }
    }
}
