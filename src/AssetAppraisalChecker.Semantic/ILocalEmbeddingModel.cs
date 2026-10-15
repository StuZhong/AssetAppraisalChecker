using System;

namespace AssetAppraisalChecker.Semantic
{
    public interface ILocalEmbeddingModel : IDisposable
    {
        string ModelId { get; }

        string ModelRevision { get; }

        string ModelHash { get; }

        int Dimension { get; }

        float[] Embed(string text);
    }
}
