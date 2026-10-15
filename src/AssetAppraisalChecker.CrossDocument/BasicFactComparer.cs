using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class BasicFactComparer : ICrossDocumentComparer
    {
        public IReadOnlyList<ConsistencyResult> Compare(
            IEnumerable<BusinessFact> firstDocumentFacts,
            IEnumerable<BusinessFact> secondDocumentFacts)
        {
            List<BusinessFact> firstFacts = MaterializeFacts(
                firstDocumentFacts,
                nameof(firstDocumentFacts));
            List<BusinessFact> secondFacts = MaterializeFacts(
                secondDocumentFacts,
                nameof(secondDocumentFacts));

            List<ConsistencyResult> results = new List<ConsistencyResult>();
            IEnumerable<FactType> factTypes = firstFacts
                .Concat(secondFacts)
                .Select(fact => fact.FactType)
                .Distinct()
                .OrderBy(factType => (int)factType);

            foreach (FactType factType in factTypes)
            {
                List<BusinessFact> firstByType = firstFacts
                    .Where(fact => fact.FactType == factType)
                    .ToList();
                List<BusinessFact> secondByType = secondFacts
                    .Where(fact => fact.FactType == factType)
                    .ToList();
                int pairCount = Math.Max(firstByType.Count, secondByType.Count);

                for (int index = 0; index < pairCount; index++)
                {
                    BusinessFact first = index < firstByType.Count ? firstByType[index] : null;
                    BusinessFact second = index < secondByType.Count ? secondByType[index] : null;
                    results.Add(ComparePair(factType, first, second));
                }
            }

            return new ReadOnlyCollection<ConsistencyResult>(results);
        }

        private static ConsistencyResult ComparePair(
            FactType factType,
            BusinessFact first,
            BusinessFact second)
        {
            if (first == null || second == null)
            {
                return new ConsistencyResult(
                    factType,
                    ConsistencyStatus.WARNING,
                    "缺少可配对的同类型事实，无法判断一致性。",
                    BuildEvidence(first, second));
            }

            if (string.IsNullOrWhiteSpace(first.Value) || string.IsNullOrWhiteSpace(second.Value))
            {
                return new ConsistencyResult(
                    factType,
                    ConsistencyStatus.WARNING,
                    "至少一项事实值缺失，无法判断一致性。",
                    BuildEvidence(first, second));
            }

            if (string.Equals(first.Value, second.Value, StringComparison.Ordinal))
            {
                return new ConsistencyResult(
                    factType,
                    ConsistencyStatus.PASS,
                    "两份文档中的事实值完全一致。",
                    BuildEvidence(first, second));
            }

            return new ConsistencyResult(
                factType,
                ConsistencyStatus.FAIL,
                "两份文档中的同类型事实值明确不同。",
                BuildEvidence(first, second));
        }

        private static IReadOnlyList<string> BuildEvidence(
            BusinessFact first,
            BusinessFact second)
        {
            List<string> evidence = new List<string>();
            if (first != null)
            {
                evidence.Add(FormatEvidence(first));
            }

            if (second != null)
            {
                evidence.Add(FormatEvidence(second));
            }

            return evidence;
        }

        private static string FormatEvidence(BusinessFact fact)
        {
            return string.Concat(
                fact.SourceDocument,
                " | ",
                fact.OriginalText);
        }

        private static List<BusinessFact> MaterializeFacts(
            IEnumerable<BusinessFact> facts,
            string parameterName)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<BusinessFact> factList = facts.ToList();
            if (factList.Any(fact => fact == null))
            {
                throw new ArgumentException("事实集合不能包含 null。", parameterName);
            }

            return factList;
        }
    }
}
