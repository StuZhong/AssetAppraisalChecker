using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests
{
    [TestClass]
    public sealed class SemanticCandidateTests
    {
        [TestMethod]
        public void Constructor_PreservesFrozenContractFields()
        {
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate();

            Assert.AreEqual(SemanticCandidateType.EvaluationMethod, candidate.CandidateType);
            Assert.AreEqual("成本法", candidate.CandidateValue);
            Assert.AreEqual("method/v1:cost-approach", candidate.NormalizedCandidate);
            Assert.AreEqual(SemanticCandidateTestData.SubjectKey, candidate.SubjectKey);
            Assert.AreEqual(0.93m, candidate.SimilarityScore);
            Assert.AreEqual(0.95m, candidate.ModelConfidence);
            Assert.AreEqual(SemanticCandidateTestData.ModelId, candidate.ModelId);
            Assert.AreEqual(SemanticCandidateTestData.ModelVersion, candidate.ModelVersion);
            Assert.AreEqual(SemanticCandidateTestData.AnalyzerVersion, candidate.AnalyzerVersion);
            Assert.AreEqual(SemanticCandidateTestData.ModelHash, candidate.ModelHash);
            Assert.AreEqual(SemanticCandidateCreatedBy.DeterministicMock, candidate.CreatedBy);
            Assert.HasCount(1, candidate.OriginalEvidence);
        }

        [TestMethod]
        public void CandidateAndEvidence_AreImmutableSnapshots()
        {
            var evidenceItems = new List<SemanticCandidateEvidence>
            {
                SemanticCandidateTestData.Evidence()
            };
            SemanticCandidateSetIdentity setIdentity = SemanticCandidateTestData.SetIdentity();
            var candidate = new SemanticCandidate(
                new SemanticCandidateIdentity(setIdentity, "method/v1:cost-approach"),
                "成本法",
                "method/v1:cost-approach",
                0.93m,
                0.95m,
                SemanticCandidateTestData.Location(),
                evidenceItems,
                SemanticCandidateTestData.ModelId,
                SemanticCandidateTestData.ModelVersion,
                SemanticCandidateTestData.AnalyzerVersion,
                SemanticCandidateTestData.ModelHash,
                "受控候选",
                SemanticCandidateCreatedBy.DeterministicMock);

            evidenceItems.Clear();

            Assert.HasCount(1, candidate.OriginalEvidence);
            Assert.IsFalse(candidate.OriginalEvidence is IList<SemanticCandidateEvidence> list
                && !list.IsReadOnly);
        }

        [TestMethod]
        public void Scores_AcceptBoundariesAndRejectOutOfRange()
        {
            Assert.AreEqual(0m, SemanticCandidateTestData.Candidate(
                modelConfidence: 0m,
                similarityScore: 0m).ModelConfidence);
            Assert.AreEqual(1m, SemanticCandidateTestData.Candidate(
                modelConfidence: 1m,
                similarityScore: 1m).SimilarityScore);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                SemanticCandidateTestData.Candidate(modelConfidence: -0.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                SemanticCandidateTestData.Candidate(modelConfidence: 1.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                SemanticCandidateTestData.Candidate(similarityScore: -0.01m));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                SemanticCandidateTestData.Candidate(similarityScore: 1.01m));
        }

        [TestMethod]
        public void Scores_UseDecimalSoNaNAndInfinityAreNotRepresentable()
        {
            Assert.AreEqual(typeof(decimal),
                typeof(SemanticCandidate).GetProperty(nameof(SemanticCandidate.ModelConfidence)).PropertyType);
            Assert.AreEqual(typeof(decimal?),
                typeof(SemanticCandidate).GetProperty(nameof(SemanticCandidate.SimilarityScore)).PropertyType);
            Assert.AreEqual(typeof(decimal),
                typeof(SystemAcceptanceThreshold).GetProperty(
                    nameof(SystemAcceptanceThreshold.MinimumModelConfidence)).PropertyType);
        }

        [TestMethod]
        public void Constructor_RejectsMissingMetadataAndInvalidHashes()
        {
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate();
            Assert.ThrowsExactly<ArgumentException>(() => new SemanticCandidate(
                candidate.Identity,
                candidate.CandidateValue,
                candidate.NormalizedCandidate,
                candidate.SimilarityScore,
                candidate.ModelConfidence,
                candidate.SourceLocation,
                candidate.OriginalEvidence,
                " ",
                candidate.ModelVersion,
                candidate.AnalyzerVersion,
                candidate.ModelHash,
                candidate.CandidateReason,
                candidate.CreatedBy));
            Assert.ThrowsExactly<ArgumentException>(() => new SemanticCandidate(
                candidate.Identity,
                candidate.CandidateValue,
                candidate.NormalizedCandidate,
                candidate.SimilarityScore,
                candidate.ModelConfidence,
                candidate.SourceLocation,
                candidate.OriginalEvidence,
                candidate.ModelId,
                " ",
                candidate.AnalyzerVersion,
                "not-a-sha256",
                candidate.CandidateReason,
                candidate.CreatedBy));
        }

        [TestMethod]
        public void Evidence_RejectsInvalidOrMismatchedHash()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new SemanticCandidateEvidence(
                    SemanticCandidateTestData.Location(),
                    "敏感原文",
                    "CandidatePhrase",
                    new string('a', 64)));
            Assert.ThrowsExactly<ArgumentException>(() =>
                new SemanticCandidateEvidence(
                    SemanticCandidateTestData.Location(),
                    "敏感原文",
                    "CandidatePhrase",
                    "invalid"));
        }

        [TestMethod]
        public void SafeDiagnostics_DoNotExposeOriginalEvidenceOrCandidateReason()
        {
            const string sensitive = "张三身份证110101199001011234不采用收益法";
            SemanticCandidate candidate = SemanticCandidateTestData.Candidate(
                originalText: sensitive);

            string diagnostic = candidate.ToString();

            Assert.IsFalse(diagnostic.Contains(sensitive));
            Assert.IsFalse(diagnostic.Contains(candidate.CandidateReason));
            StringAssert.Contains(diagnostic, candidate.OriginalEvidence[0].EvidenceHash);
            StringAssert.Contains(diagnostic, candidate.SubjectKey);
        }

        [TestMethod]
        public void SemanticCandidate_IsNotAnyBusinessFactType()
        {
            Type candidateType = typeof(SemanticCandidate);
            Assert.IsFalse(typeof(AssessmentObjectFact).IsAssignableFrom(candidateType));
            Assert.IsFalse(typeof(EvaluationMethodFact).IsAssignableFrom(candidateType));
            Assert.IsFalse(typeof(AssessmentValueFact).IsAssignableFrom(candidateType));
            Assert.IsFalse(typeof(BenchmarkDateFact).IsAssignableFrom(candidateType));
            Assert.IsFalse(typeof(ReportNumberFact).IsAssignableFrom(candidateType));
        }
    }
}
