using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class SemanticCandidateRanker : ISemanticCandidateRanker
    {
        private const int RetrievalLimit = 2;

        private readonly ILocalEmbeddingModel _embeddingModel;
        private readonly IReranker _reranker;

        public SemanticCandidateRanker(
            ILocalEmbeddingModel embeddingModel,
            IReranker reranker)
        {
            _embeddingModel = embeddingModel
                ?? throw new ArgumentNullException(nameof(embeddingModel));
            _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));
        }

        public SemanticCandidateRankingResult Rank(
            SemanticCandidateRequest request,
            SemanticCandidateSelectionContext selectionContext)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (selectionContext == null)
            {
                throw new ArgumentNullException(nameof(selectionContext));
            }

            if (selectionContext.Resolution
                == SemanticCandidateResolution.NoValidCandidate)
            {
                return CreateResult(
                    request,
                    selectionContext,
                    SemanticCandidateRankingStatus.Abstained,
                    new SemanticCandidateRankingItem[0],
                    "NoValidCandidate");
            }

            string query = string.Join(
                "\n",
                request.Evidence
                    .OrderBy(item => item.SourceLocation.SourceLocationKey, StringComparer.Ordinal)
                    .ThenBy(item => item.EvidenceHash, StringComparer.Ordinal)
                    .Select(item => item.OriginalText));
            float[] queryEmbedding = RequireEmbedding(_embeddingModel.Embed(query));

            List<RetrievalCandidate> retrieval = request.AllowedNormalizedCandidates
                .Select(value => new RetrievalCandidate(
                    new SemanticCandidateIdentity(request.CandidateSetIdentity, value),
                    value,
                    Cosine(queryEmbedding, RequireEmbedding(_embeddingModel.Embed(value)))))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Identity.CanonicalValue, StringComparer.Ordinal)
                .ToList();

            for (int index = 0; index < retrieval.Count; index++)
            {
                retrieval[index].RetrievalRank = index + 1;
            }

            RetrievalCandidate[] top = retrieval.Take(RetrievalLimit).ToArray();
            IReadOnlyList<double> logits = _reranker.Score(
                query,
                top.Select(item => item.Value).ToArray());
            if (logits == null || logits.Count != top.Length
                || logits.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
            {
                throw new InvalidOperationException(
                    "Reranker 输出数量或数值不符合契约。");
            }

            List<RetrievalCandidate> reranked = top
                .Select((item, index) =>
                {
                    item.RawLogit = logits[index];
                    return item;
                })
                .OrderByDescending(item => item.RawLogit.Value)
                .ThenBy(item => item.Identity.CanonicalValue, StringComparer.Ordinal)
                .ToList();
            for (int index = 0; index < reranked.Count; index++)
            {
                reranked[index].RerankerRank = index + 1;
            }

            SemanticCandidateRankingItem[] items = retrieval
                .Select(item => new SemanticCandidateRankingItem(
                    item.Identity,
                    item.Value,
                    item.Score,
                    item.RetrievalRank,
                    item.RawLogit,
                    item.RerankerRank))
                .ToArray();

            SemanticCandidateRankingStatus status = selectionContext.Resolution
                == SemanticCandidateResolution.Ambiguous
                ? SemanticCandidateRankingStatus.Abstained
                : SemanticCandidateRankingStatus.Ranked;
            return CreateResult(
                request,
                selectionContext,
                status,
                items,
                status == SemanticCandidateRankingStatus.Ranked
                    ? (selectionContext.ShadowOnly
                        ? "ShadowRankingCompleted"
                        : "ActiveRankingCompleted")
                    : "AmbiguousCandidateSet");
        }

        private SemanticCandidateRankingResult CreateResult(
            SemanticCandidateRequest request,
            SemanticCandidateSelectionContext selectionContext,
            SemanticCandidateRankingStatus status,
            IEnumerable<SemanticCandidateRankingItem> items,
            string diagnosticCode)
        {
            return new SemanticCandidateRankingResult(
                request.RequestId,
                request.CandidateSetIdentity,
                selectionContext,
                status,
                items,
                _embeddingModel.ModelId,
                _embeddingModel.ModelRevision,
                _embeddingModel.ModelHash,
                _reranker.ModelId,
                _reranker.ModelRevision,
                _reranker.ModelHash,
                diagnosticCode);
        }

        private float[] RequireEmbedding(float[] embedding)
        {
            if (embedding == null || embedding.Length != _embeddingModel.Dimension
                || embedding.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            {
                throw new InvalidOperationException(
                    "Embedding 输出维度或数值不符合契约。");
            }

            return embedding;
        }

        private static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
        {
            if (left.Count != right.Count || left.Count == 0)
            {
                throw new InvalidOperationException("Embedding 维度不一致。");
            }

            double dot = 0d;
            double leftNorm = 0d;
            double rightNorm = 0d;
            for (int index = 0; index < left.Count; index++)
            {
                dot += left[index] * right[index];
                leftNorm += left[index] * left[index];
                rightNorm += right[index] * right[index];
            }

            if (leftNorm <= 0d || rightNorm <= 0d)
            {
                throw new InvalidOperationException("Embedding norm 必须大于零。");
            }

            return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
        }

        private sealed class RetrievalCandidate
        {
            public RetrievalCandidate(
                SemanticCandidateIdentity identity,
                string value,
                double score)
            {
                Identity = identity;
                Value = value;
                Score = score;
            }

            public SemanticCandidateIdentity Identity { get; }

            public string Value { get; }

            public double Score { get; }

            public int RetrievalRank { get; set; }

            public double? RawLogit { get; set; }

            public int? RerankerRank { get; set; }
        }
    }
}
