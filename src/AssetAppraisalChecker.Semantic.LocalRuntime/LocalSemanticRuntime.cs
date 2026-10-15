using System;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class LocalSemanticRuntime : ISemanticCandidateRanker, IDisposable
    {
        private readonly ILocalEmbeddingModel _embeddingModel;
        private readonly IReranker _reranker;
        private readonly SemanticCandidateRanker _ranker;
        private bool _disposed;

        private LocalSemanticRuntime(
            ILocalEmbeddingModel embeddingModel,
            IReranker reranker)
        {
            _embeddingModel = embeddingModel;
            _reranker = reranker;
            _ranker = new SemanticCandidateRanker(embeddingModel, reranker);
        }

        public static LocalSemanticRuntime Create(LocalSemanticRuntimeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            VerifiedLocalModelAssets assets = new LocalModelAssetVerifier().Verify(options);
            BgeLocalEmbeddingModel embedding = null;
            BgeReranker reranker = null;
            try
            {
                embedding = new BgeLocalEmbeddingModel(
                    assets,
                    options.BgeModel.ExpectedSha256);
                reranker = new BgeReranker(
                    assets,
                    options.RerankerModel.ExpectedSha256);
                return new LocalSemanticRuntime(embedding, reranker);
            }
            catch
            {
                reranker?.Dispose();
                embedding?.Dispose();
                throw;
            }
        }

        public SemanticCandidateRankingResult Rank(
            SemanticCandidateRequest request,
            SemanticCandidateSelectionContext selectionContext)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LocalSemanticRuntime));
            }

            return _ranker.Rank(request, selectionContext);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _reranker.Dispose();
            _embeddingModel.Dispose();
            _disposed = true;
        }
    }
}
