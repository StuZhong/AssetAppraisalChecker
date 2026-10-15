using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateRankingResult
    {
        public SemanticCandidateRankingResult(
            string requestId,
            SemanticCandidateSetIdentity candidateSetIdentity,
            SemanticCandidateSelectionContext selectionContext,
            SemanticCandidateRankingStatus status,
            IEnumerable<SemanticCandidateRankingItem> rankings,
            string embeddingModelId,
            string embeddingModelRevision,
            string embeddingModelHash,
            string rerankerModelId,
            string rerankerModelRevision,
            string rerankerModelHash,
            string diagnosticCode)
        {
            SemanticSourceLocation.RequireKey(requestId, nameof(requestId));
            CandidateSetIdentity = candidateSetIdentity
                ?? throw new ArgumentNullException(nameof(candidateSetIdentity));
            SelectionContext = selectionContext
                ?? throw new ArgumentNullException(nameof(selectionContext));
            if (!Enum.IsDefined(typeof(SemanticCandidateRankingStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (rankings == null)
            {
                throw new ArgumentNullException(nameof(rankings));
            }

            List<SemanticCandidateRankingItem> items = rankings.ToList();
            if (items.Any(item => item == null
                || !item.CandidateIdentity.SetIdentity.Equals(candidateSetIdentity)))
            {
                throw new ArgumentException(
                    "Ranking item 必须属于当前 CandidateSet。",
                    nameof(rankings));
            }

            if (items.GroupBy(item => item.CandidateIdentity).Any(group => group.Count() > 1)
                || !items.Select(item => item.RetrievalRank)
                    .OrderBy(value => value)
                    .SequenceEqual(Enumerable.Range(1, items.Count)))
            {
                throw new ArgumentException(
                    "Ranking identity 必须唯一且 retrieval rank 必须连续。",
                    nameof(rankings));
            }

            SemanticCandidateRankingItem[] reranked = items
                .Where(item => item.RerankerRank.HasValue)
                .ToArray();
            if (reranked.Length > 2
                || !reranked.Select(item => item.RerankerRank.Value)
                    .OrderBy(value => value)
                    .SequenceEqual(Enumerable.Range(1, reranked.Length)))
            {
                throw new ArgumentException(
                    "Reranker 只能覆盖 stable Top2 且 rank 必须连续。",
                    nameof(rankings));
            }

            RequireModelIdentity(embeddingModelId, embeddingModelRevision, embeddingModelHash);
            RequireModelIdentity(rerankerModelId, rerankerModelRevision, rerankerModelHash);
            SemanticSourceLocation.RequireKey(diagnosticCode, nameof(diagnosticCode));
            if (status == SemanticCandidateRankingStatus.Ranked && items.Count == 0)
            {
                throw new ArgumentException("Ranked 结果必须包含候选。", nameof(rankings));
            }

            RequestId = requestId;
            Status = status;
            Rankings = new ReadOnlyCollection<SemanticCandidateRankingItem>(items);
            EmbeddingModelId = embeddingModelId;
            EmbeddingModelRevision = embeddingModelRevision;
            EmbeddingModelHash = embeddingModelHash.ToLowerInvariant();
            RerankerModelId = rerankerModelId;
            RerankerModelRevision = rerankerModelRevision;
            RerankerModelHash = rerankerModelHash.ToLowerInvariant();
            DiagnosticCode = diagnosticCode;
        }

        public string RequestId { get; }

        public SemanticCandidateSetIdentity CandidateSetIdentity { get; }

        public SemanticCandidateSelectionContext SelectionContext { get; }

        public SemanticCandidateRankingStatus Status { get; }

        public IReadOnlyList<SemanticCandidateRankingItem> Rankings { get; }

        public string EmbeddingModelId { get; }

        public string EmbeddingModelRevision { get; }

        public string EmbeddingModelHash { get; }

        public string RerankerModelId { get; }

        public string RerankerModelRevision { get; }

        public string RerankerModelHash { get; }

        public string DiagnosticCode { get; }

        public bool FactPromotionAllowed => false;

        public IReadOnlyList<string> ToSafeEvidence()
        {
            string evidencePrefix = SelectionContext.ShadowOnly
                ? "SemanticShadow"
                : "SemanticActive";
            var evidence = new List<string>
            {
                string.Concat(
                    evidencePrefix, " | RequestId=", RequestId,
                    " | Status=", Status,
                    " | Resolution=", SelectionContext.Resolution,
                    " | ShadowOnly=", SelectionContext.ShadowOnly,
                    " | CandidateCount=", Rankings.Count,
                    " | DiagnosticCode=", DiagnosticCode),
                string.Concat(
                    evidencePrefix, " | Embedding=", EmbeddingModelId,
                    "@", EmbeddingModelRevision,
                    " | Reranker=", RerankerModelId,
                    "@", RerankerModelRevision,
                    " | FactPromotionAllowed=false")
            };

            foreach (SemanticCandidateRankingItem item in Rankings
                .OrderBy(value => value.RetrievalRank))
            {
                evidence.Add(string.Concat(
                    evidencePrefix,
                    "Rank | CandidateIdentity=",
                    item.CandidateIdentity,
                    " | RetrievalRank=", item.RetrievalRank,
                    " | RetrievalScore=",
                    item.RetrievalScore.ToString("G17", CultureInfo.InvariantCulture),
                    " | RerankerRank=",
                    item.RerankerRank.HasValue
                        ? item.RerankerRank.Value.ToString(CultureInfo.InvariantCulture)
                        : "n/a",
                    " | RerankerRawLogit=",
                    item.RerankerRawLogit.HasValue
                        ? item.RerankerRawLogit.Value.ToString("G17", CultureInfo.InvariantCulture)
                        : "n/a"));
            }

            return new ReadOnlyCollection<string>(evidence);
        }

        private static void RequireModelIdentity(string id, string revision, string hash)
        {
            SemanticSourceLocation.RequireText(id, nameof(id));
            SemanticSourceLocation.RequireKey(revision, nameof(revision));
            if (!SemanticHash.IsSha256(hash))
            {
                throw new ArgumentException("模型 Hash 必须为 SHA-256。", nameof(hash));
            }
        }
    }
}
