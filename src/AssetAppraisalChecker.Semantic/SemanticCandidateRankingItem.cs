using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateRankingItem
    {
        public SemanticCandidateRankingItem(
            SemanticCandidateIdentity candidateIdentity,
            string candidateValue,
            double retrievalScore,
            int retrievalRank,
            double? rerankerRawLogit,
            int? rerankerRank)
        {
            CandidateIdentity = candidateIdentity
                ?? throw new ArgumentNullException(nameof(candidateIdentity));
            SemanticSourceLocation.RequireText(candidateValue, nameof(candidateValue));
            if (double.IsNaN(retrievalScore) || double.IsInfinity(retrievalScore))
            {
                throw new ArgumentOutOfRangeException(nameof(retrievalScore));
            }

            if (retrievalRank <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retrievalRank));
            }

            if (rerankerRawLogit.HasValue
                && (double.IsNaN(rerankerRawLogit.Value)
                    || double.IsInfinity(rerankerRawLogit.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(rerankerRawLogit));
            }

            if (rerankerRawLogit.HasValue != rerankerRank.HasValue
                || (rerankerRank.HasValue && rerankerRank.Value <= 0))
            {
                throw new ArgumentException(
                    "Reranker raw logit 与 rank 必须同时存在且 rank 为正数。");
            }

            CandidateValue = candidateValue;
            RetrievalScore = retrievalScore;
            RetrievalRank = retrievalRank;
            RerankerRawLogit = rerankerRawLogit;
            RerankerRank = rerankerRank;
        }

        public SemanticCandidateIdentity CandidateIdentity { get; }

        public string CandidateValue { get; }

        public double RetrievalScore { get; }

        public int RetrievalRank { get; }

        public double? RerankerRawLogit { get; }

        public int? RerankerRank { get; }
    }
}
