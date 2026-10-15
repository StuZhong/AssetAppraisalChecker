using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class EvaluationMethodCandidateNegationTests
    {
        [TestMethod]
        [DataRow("房屋建筑物按现行重建成本测算。")]
        [DataRow("房屋建筑物尚未取得其他资料，按现行重建成本测算。")]
        public void AffirmativeCandidate_UnrelatedNegativeWordsDoNotContaminateScope(string text)
        {
            var ranker = new RecordingRanker();
            var execution = Execute(text, ranker);
            AssertGate(execution, "AcceptedEvidence", "AcceptedForCandidatePriority", 1);
            Assert.AreEqual(1, ranker.Requests.Count);
        }

        [TestMethod]
        [DataRow("房屋建筑物不采用现行重建成本测算。")]
        [DataRow("房屋建筑物未采用重建成本测算。")]
        [DataRow("房屋建筑物不适用重置成本途径。")]
        [DataRow("房屋建筑物不采用以现行重建成本为基础的测算。")]
        [DataRow("房屋建筑物并非按现行重建成本测算。")]
        [DataRow("房屋建筑物未基于现行重建成本测算。")]
        public void ExplicitLocalNegation_IsRejectedEvenWithHighModelScores(string text)
        {
            var ranker = new RecordingRanker();
            var execution = Execute(text, ranker);
            AssertGate(execution, "Rejected", "NegationDetected", 1);
            Assert.AreEqual(1, ranker.Requests.Count);
            Assert.IsFalse(execution.ExecutionEvidence.Any(e => e.Contains("Decision=AcceptedEvidence")));
        }

        [TestMethod]
        [DataRow("房屋建筑物不采用现行重建成本测算；本次按重置成本途径测算。", true)]
        [DataRow("房屋建筑物按现行重建成本测算；本次不采用重置成本途径。", false)]
        public void SameParagraphMixedPolarity_UsesSeparateEvidenceAndStableIdentities(string text, bool negativeFirst)
        {
            var ranker = new RecordingRanker();
            var execution = Execute(text, ranker);
            AssertGate(execution, "Rejected", "NegationDetected", 1);
            AssertGate(execution, "AcceptedEvidence", "AcceptedForCandidatePriority", 1);
            Assert.AreEqual(2, ranker.Requests.Count);
            Assert.AreEqual(2, ranker.Requests.Select(r => r.CandidateSetIdentity.CanonicalValue).Distinct().Count());
            Assert.AreEqual(negativeFirst, ranker.Requests[0].Evidence[0].OriginalText.Contains("不采用"));
            Assert.AreEqual(!negativeFirst, ranker.Requests[1].Evidence[0].OriginalText.Contains("不采用"));
            Assert.IsTrue(ranker.Requests.All(r => !r.Evidence[0].OriginalText.Contains("；本次")));

            var repeated = new RecordingRanker();
            Execute(text, repeated);
            CollectionAssert.AreEqual(ranker.Requests.Select(r => r.RequestId).ToArray(),
                repeated.Requests.Select(r => r.RequestId).ToArray());
            var read = Input("DocumentB", text);
            var extracted = new EvaluationMethodFactExtractor().ExtractWithDiagnostics(read.DocumentReadResult, read.SourceDocument);
            Assert.AreEqual(0, extracted.Facts.Count);
            Assert.AreEqual(1, extracted.Statements.Count, "AI scoping must not broaden business extraction.");
        }

        [TestMethod]
        [DataRow("房屋建筑物采用现行重建成本或重置成本途径测算。")]
        [DataRow("房屋建筑物不得不采用现行重建成本测算。")]
        [DataRow("房屋建筑物尚未确定是否采用现行重建成本测算。")]
        [DataRow("房屋建筑物采用“现行重建成本测算”的引用表述。")]
        public void UncertainScope_DoesNotGuessAffirmativeOrNegative(string text)
        {
            var ranker = new RecordingRanker();
            var execution = Execute(text, ranker);
            AssertGate(execution, "Abstained", "Ambiguous", 1);
            Assert.IsTrue(ranker.Contexts.All(c => c.Resolution == SemanticCandidateResolution.Ambiguous));
            Assert.IsFalse(execution.ExecutionEvidence.Any(e => e.Contains("Decision=AcceptedEvidence")));
        }

        [TestMethod]
        public void CompetingSubjects_StillUsesSubjectUnresolvedAbstention()
        {
            var execution = Execute("房屋建筑物及机器设备采用重置成本法。", new RecordingRanker());
            AssertGate(execution, "Abstained", "SubjectUnresolved", 1);
        }

        [TestMethod]
        public void RulesOnlyBusinessResultAndCandidateExtraction_AreUnchanged()
        {
            const string text = "房屋建筑物不采用现行重建成本测算；本次按重置成本途径测算。";
            var a = Input("DocumentA", "房屋建筑物采用重置成本法。");
            var b = Input("DocumentB", text);
            var before = new EvaluationMethodChecker(new EvaluationMethodFactExtractor(), new EvaluationMethodComparer()).Execute(a, b);
            var active = Execute(text, new RecordingRanker());
            CollectionAssert.AreEqual(before.Results.Select(r => r.Status).ToArray(), active.Results.Select(r => r.Status).ToArray());
            CollectionAssert.AreEqual(before.Results.Select(r => r.Identity.CanonicalValue).ToArray(), active.Results.Select(r => r.Identity.CanonicalValue).ToArray());
            Assert.IsFalse(before.ExecutionEvidence.Any(e => e.Contains("SemanticActive")));
        }

        [TestMethod]
        public void ExistingHardConflict_PreventsRanking()
        {
            var ranker = new RecordingRanker();
            var checker = Checker(ranker);
            var execution = checker.Execute(Input("DocumentA", "房屋建筑物采用市场法。"),
                Input("DocumentB", "房屋建筑物采用收益法。", "房屋建筑物不采用现行重建成本测算。"));
            Assert.AreEqual(0, ranker.Requests.Count);
            Assert.IsTrue(execution.ExecutionEvidence.Any(e => e.Contains("Reason=HardRuleConflict")));
        }

        private static FactCheckExecutionEnvelope Execute(string text, RecordingRanker ranker)
        {
            var execution = Checker(ranker).Execute(Input("DocumentA", "房屋建筑物采用重置成本法。"), Input("DocumentB", text));
            Assert.AreNotEqual(FactCheckExecutionStatus.Failed, execution.ExecutionStatus);
            Assert.IsTrue(execution.Results.All(r => r.Status == ConsistencyStatus.WARNING));
            Assert.IsTrue(execution.ExecutionEvidence.Any(e => e.Contains("FactPromotionAllowed=false")));
            return execution;
        }
        private static EvaluationMethodChecker Checker(RecordingRanker ranker) => new EvaluationMethodChecker(
            new EvaluationMethodFactExtractor(), new EvaluationMethodComparer(), null, ranker, SemanticCandidateProcessingOptions.Active);
        private static void AssertGate(FactCheckExecutionEnvelope execution, string decision, string reason, int count) =>
            Assert.AreEqual(count, execution.ExecutionEvidence.Count(e => e.StartsWith("SemanticRankingGate |") &&
                e.Contains("Decision=" + decision + " | Reason=" + reason + " |")));
        private static V2DocumentAnalysisInput Input(string source, params string[] texts) => new V2DocumentAnalysisInput(
            DocumentReadResult.Succeeded(texts.Select((text, i) => new DocumentElement(DocumentElementType.Paragraph,
                text, new DocumentLocation(DocumentPart.MainBody, i, null, null, null, new string[0]), i, "",
                new DocumentNearbyContext("", text, ""))), texts.Length, 0), source);

        private sealed class RecordingRanker : ISemanticCandidateRanker
        {
            private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            public List<SemanticCandidateRequest> Requests { get; } = new List<SemanticCandidateRequest>();
            public List<SemanticCandidateSelectionContext> Contexts { get; } = new List<SemanticCandidateSelectionContext>();
            public SemanticCandidateRankingResult Rank(SemanticCandidateRequest request, SemanticCandidateSelectionContext context)
            {
                Requests.Add(request); Contexts.Add(context);
                return new SemanticCandidateRankingResult(request.RequestId, request.CandidateSetIdentity, context,
                    SemanticCandidateRankingStatus.Ranked, new[] { new SemanticCandidateRankingItem(
                        new SemanticCandidateIdentity(request.CandidateSetIdentity, "重置成本法"), "重置成本法", 0.999, 1, 100, 1) },
                    "test-embedding", "test-revision", Hash, "test-reranker", "test-revision", Hash, "SafetyWiringTestOnly");
            }
        }
    }
}
