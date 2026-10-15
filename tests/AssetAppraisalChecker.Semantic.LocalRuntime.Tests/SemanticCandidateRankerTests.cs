using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Semantic.LocalRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.LocalRuntime.Tests
{
    [TestClass]
    public sealed class SemanticCandidateRankerTests
    {
        private const string Hash =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [TestMethod]
        public void Rank_UsesStableTop2AndRerankerOrdering()
        {
            var embedding = new FakeEmbeddingModel(new Dictionary<string, float[]>
            {
                ["query"] = new[] { 1f, 0f },
                ["a"] = new[] { 1f, 0f },
                ["b"] = new[] { 0.8f, 0.6f },
                ["c"] = new[] { 0f, 1f }
            });
            var reranker = new FakeReranker(new[] { 1d, 2d });

            SemanticCandidateRankingResult result = new SemanticCandidateRanker(
                embedding,
                reranker).Rank(
                    Request("a", "b", "c"),
                    Context(SemanticCandidateResolution.Resolvable));

            Assert.AreEqual(SemanticCandidateRankingStatus.Ranked, result.Status);
            Assert.HasCount(3, result.Rankings);
            Assert.AreEqual("a", result.Rankings.Single(item => item.RetrievalRank == 1).CandidateValue);
            Assert.AreEqual("b", result.Rankings.Single(item => item.RerankerRank == 1).CandidateValue);
            Assert.AreEqual(2, reranker.LastCandidateCount);
        }

        [TestMethod]
        public void Rank_RetrievalTieUsesOrdinalCandidateIdentity()
        {
            var embedding = new FakeEmbeddingModel(new Dictionary<string, float[]>
            {
                ["query"] = new[] { 1f, 0f },
                ["a"] = new[] { 1f, 0f },
                ["b"] = new[] { 1f, 0f },
                ["c"] = new[] { 0f, 1f }
            });

            SemanticCandidateRankingResult result = new SemanticCandidateRanker(
                embedding,
                new FakeReranker(new[] { 0d, 0d })).Rank(
                    Request("c", "b", "a"),
                    Context(SemanticCandidateResolution.Resolvable));

            Assert.AreEqual("a", result.Rankings.Single(item => item.RetrievalRank == 1).CandidateValue);
            Assert.AreEqual("b", result.Rankings.Single(item => item.RetrievalRank == 2).CandidateValue);
            Assert.AreEqual("a", result.Rankings.Single(item => item.RerankerRank == 1).CandidateValue);
        }

        [TestMethod]
        public void Rank_SingleCandidateDoesNotInventSecondCandidate()
        {
            var reranker = new FakeReranker(new[] { 3d });
            SemanticCandidateRankingResult result = new SemanticCandidateRanker(
                new FakeEmbeddingModel(new Dictionary<string, float[]>
                {
                    ["query"] = new[] { 1f, 0f },
                    ["only"] = new[] { 1f, 0f }
                }),
                reranker).Rank(
                    Request("only"),
                    Context(SemanticCandidateResolution.Resolvable));

            Assert.HasCount(1, result.Rankings);
            Assert.AreEqual(1, reranker.LastCandidateCount);
        }

        [TestMethod]
        public void Rank_NoValidCandidateAbstainsWithoutCallingModels()
        {
            var embedding = new FakeEmbeddingModel(new Dictionary<string, float[]>());
            var reranker = new FakeReranker(new double[0]);

            SemanticCandidateRankingResult result = new SemanticCandidateRanker(
                embedding,
                reranker).Rank(
                    Request("a"),
                    Context(SemanticCandidateResolution.NoValidCandidate));

            Assert.AreEqual(SemanticCandidateRankingStatus.Abstained, result.Status);
            Assert.HasCount(0, result.Rankings);
            Assert.AreEqual(0, embedding.CallCount);
            Assert.AreEqual(0, reranker.CallCount);
            Assert.IsFalse(result.FactPromotionAllowed);
        }

        [TestMethod]
        public void Rank_AmbiguousCandidateSetNeverAllowsBusinessSelection()
        {
            SemanticCandidateRankingResult result = new SemanticCandidateRanker(
                new FakeEmbeddingModel(new Dictionary<string, float[]>
                {
                    ["query"] = new[] { 1f, 0f },
                    ["a"] = new[] { 1f, 0f },
                    ["b"] = new[] { 0f, 1f }
                }),
                new FakeReranker(new[] { 10d, -10d })).Rank(
                    Request("a", "b"),
                    Context(SemanticCandidateResolution.Ambiguous));

            Assert.AreEqual(SemanticCandidateRankingStatus.Abstained, result.Status);
            Assert.IsFalse(result.SelectionContext.BusinessSelectionAllowed);
            Assert.IsFalse(result.FactPromotionAllowed);
        }

        [TestMethod]
        public void RankingRawLogitIsNotModelConfidence()
        {
            SemanticCandidateRankingResult result = new SemanticCandidateRanker(
                new FakeEmbeddingModel(new Dictionary<string, float[]>
                {
                    ["query"] = new[] { 1f, 0f },
                    ["a"] = new[] { 1f, 0f }
                }),
                new FakeReranker(new[] { 7.25d })).Rank(
                    Request("a"),
                    Context(SemanticCandidateResolution.Resolvable));

            Assert.AreEqual(7.25d, result.Rankings[0].RerankerRawLogit.Value, 0d);
            Assert.IsNull(typeof(SemanticCandidateRankingItem).GetProperty("ModelConfidence"));
            Assert.IsFalse(result.FactPromotionAllowed);
        }

        [TestMethod]
        public void AssetVerifier_MissingAssetFailsClosed()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AssetAppraisalChecker-LocalRuntime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var missing = new LocalModelAssetDescriptor("missing.bin", 1, Hash);
                var options = new LocalSemanticRuntimeOptions(
                    root,
                    Path.Combine(root, "ortextensions.dll"),
                    missing,
                    missing,
                    missing,
                    missing,
                    missing,
                    missing,
                    true);

                Assert.ThrowsExactly<FileNotFoundException>(
                    () => new LocalModelAssetVerifier().Verify(options));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static SemanticCandidateRequest Request(params string[] candidates)
        {
            var location = new SemanticSourceLocation("synthetic.docx", "loc/v1:test", "synthetic");
            var evidence = new SemanticCandidateEvidence(
                location,
                "query",
                "SyntheticQuery",
                SemanticHash.ComputeSha256("query"));
            return new SemanticCandidateRequest(
                "request/v1:test",
                new SemanticCandidateSetIdentity(
                    SemanticCandidateType.EvaluationMethod,
                    "subject/v1:test",
                    location.SourceLocationKey,
                    SemanticCandidateRole.AdoptedMethod),
                new[] { evidence },
                candidates,
                "test-ontology/v1",
                "test-policy/v1",
                true);
        }

        private static SemanticCandidateSelectionContext Context(
            SemanticCandidateResolution resolution)
        {
            return new SemanticCandidateSelectionContext(resolution, true);
        }

        private sealed class FakeEmbeddingModel : ILocalEmbeddingModel
        {
            private readonly IReadOnlyDictionary<string, float[]> _values;

            public FakeEmbeddingModel(IReadOnlyDictionary<string, float[]> values)
            {
                _values = values;
            }

            public int CallCount { get; private set; }

            public string ModelId => "fake-embedding";
            public string ModelRevision => "revision";
            public string ModelHash => Hash;
            public int Dimension => 2;

            public float[] Embed(string text)
            {
                CallCount++;
                return _values[text];
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeReranker : IReranker
        {
            private readonly IReadOnlyList<double> _scores;

            public FakeReranker(IReadOnlyList<double> scores)
            {
                _scores = scores;
            }

            public int CallCount { get; private set; }
            public int LastCandidateCount { get; private set; }

            public string ModelId => "fake-reranker";
            public string ModelRevision => "revision";
            public string ModelHash => Hash;

            public IReadOnlyList<double> Score(
                string query,
                IReadOnlyList<string> candidates)
            {
                CallCount++;
                LastCandidateCount = candidates.Count;
                return _scores;
            }

            public void Dispose()
            {
            }
        }
    }
}
