using System;
using System.Collections.Generic;

namespace AssetAppraisalChecker.Semantic
{
    public interface IReranker : IDisposable
    {
        string ModelId { get; }

        string ModelRevision { get; }

        string ModelHash { get; }

        IReadOnlyList<double> Score(string query, IReadOnlyList<string> candidates);
    }
}
