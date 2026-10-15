using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public sealed class FactCheckRegistry : IFactCheckRegistry
    {
        private readonly IReadOnlyDictionary<FactType, IFactChecker> _byFactType;

        public FactCheckRegistry(IEnumerable<IFactChecker> checkers)
        {
            if (checkers == null)
            {
                throw new ArgumentNullException(nameof(checkers));
            }

            List<IFactChecker> checkerItems = checkers.ToList();
            if (checkerItems.Any(checker => checker == null))
            {
                throw new ArgumentException(
                    "FactCheck 注册集合不能包含 null。",
                    nameof(checkers));
            }

            if (checkerItems.Any(
                checker => string.IsNullOrWhiteSpace(checker.CheckerName)))
            {
                throw new ArgumentException(
                    "所有 FactCheck 必须提供 Checker 名称。",
                    nameof(checkers));
            }

            if (checkerItems.Any(
                checker => checker.FactType == FactType.Unknown ||
                    !Enum.IsDefined(typeof(FactType), checker.FactType)))
            {
                throw new ArgumentException(
                    "所有 FactCheck 必须声明有效 FactType。",
                    nameof(checkers));
            }

            if (checkerItems
                .GroupBy(checker => checker.CheckerName, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Checker 名称不能重复。",
                    nameof(checkers));
            }

            if (checkerItems
                .GroupBy(checker => checker.FactType)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "同一 FactType 只能显式注册一个 Checker。",
                    nameof(checkers));
            }

            Checkers = new ReadOnlyCollection<IFactChecker>(checkerItems);
            _byFactType = new ReadOnlyDictionary<FactType, IFactChecker>(
                checkerItems.ToDictionary(
                    checker => checker.FactType,
                    checker => checker));
        }

        public IReadOnlyList<IFactChecker> Checkers { get; }

        public bool TryGet(FactType factType, out IFactChecker checker)
        {
            return _byFactType.TryGetValue(factType, out checker);
        }
    }
}
