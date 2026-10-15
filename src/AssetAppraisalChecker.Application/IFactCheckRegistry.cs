using System.Collections.Generic;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public interface IFactCheckRegistry
    {
        IReadOnlyList<IFactChecker> Checkers { get; }

        bool TryGet(FactType factType, out IFactChecker checker);
    }
}
