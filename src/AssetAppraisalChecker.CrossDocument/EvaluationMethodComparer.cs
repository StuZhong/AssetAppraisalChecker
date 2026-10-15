using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class EvaluationMethodComparer
    {
        public const string ResultRole = "AdoptedMethodSet";
        public const string FinalResultRole = "FinalAdoptedMethodSet";
        public const string ScopeKey = "scope/v1:default";

        public IReadOnlyList<ConsistencyResult> Compare(
            IEnumerable<EvaluationMethodFact> firstFacts,
            IEnumerable<EvaluationMethodFact> secondFacts,
            IEnumerable<EvaluationMethodStatement> firstStatements,
            IEnumerable<EvaluationMethodStatement> secondStatements)
        {
            if (firstFacts == null)
            {
                throw new ArgumentNullException(nameof(firstFacts));
            }

            if (secondFacts == null)
            {
                throw new ArgumentNullException(nameof(secondFacts));
            }

            if (firstStatements == null)
            {
                throw new ArgumentNullException(nameof(firstStatements));
            }

            if (secondStatements == null)
            {
                throw new ArgumentNullException(nameof(secondStatements));
            }

            EvaluationMethodFact[] leftFacts = firstFacts.ToArray();
            EvaluationMethodFact[] rightFacts = secondFacts.ToArray();
            EvaluationMethodStatement[] leftStatements = firstStatements.ToArray();
            EvaluationMethodStatement[] rightStatements = secondStatements.ToArray();
            ValidateItems(leftFacts, rightFacts, leftStatements, rightStatements);

            string[] subjectKeys = leftFacts.Select(item => item.NormalizedSubjectKey)
                .Concat(rightFacts.Select(item => item.NormalizedSubjectKey))
                .Concat(leftStatements.Select(item => item.NormalizedSubjectKey))
                .Concat(rightStatements.Select(item => item.NormalizedSubjectKey))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

            var results = new List<ConsistencyResult>();
            foreach (string subjectKey in subjectKeys)
            {
                results.Add(CompareSubject(
                    subjectKey,
                    leftFacts.Where(item => item.NormalizedSubjectKey == subjectKey).ToArray(),
                    rightFacts.Where(item => item.NormalizedSubjectKey == subjectKey).ToArray(),
                    leftStatements.Where(item => item.NormalizedSubjectKey == subjectKey).ToArray(),
                    rightStatements.Where(item => item.NormalizedSubjectKey == subjectKey).ToArray()));
            }

            return new ReadOnlyCollection<ConsistencyResult>(results);
        }

        private static ConsistencyResult CompareSubject(
            string subjectKey,
            IReadOnlyList<EvaluationMethodFact> firstFacts,
            IReadOnlyList<EvaluationMethodFact> secondFacts,
            IReadOnlyList<EvaluationMethodStatement> firstStatements,
            IReadOnlyList<EvaluationMethodStatement> secondStatements)
        {
            if (firstFacts.Concat(secondFacts).Any(item => item.MethodRole == EvaluationMethodRole.FinalAdopted))
            {
                return CompareFinalSubject(subjectKey, firstFacts, secondFacts, firstStatements, secondStatements);
            }

            // Independent method calculations cannot stand in for an adopted-method set.
            firstFacts = firstFacts.Where(item => item.MethodRole != EvaluationMethodRole.MethodCalculation).ToArray();
            secondFacts = secondFacts.Where(item => item.MethodRole != EvaluationMethodRole.MethodCalculation).ToArray();
            var identity = new ConsistencyResultIdentity(
                FactType.EvaluationMethod,
                subjectKey,
                ResultRole,
                ScopeKey);
            List<string> evidence = CreateEvidence(
                subjectKey,
                firstFacts,
                secondFacts,
                firstStatements,
                secondStatements);

            if (firstFacts.Count == 0 || secondFacts.Count == 0)
            {
                if (HasExplicitAdoptionConflict(firstFacts, secondStatements)
                    || HasExplicitAdoptionConflict(secondFacts, firstStatements))
                {
                    return new ConsistencyResult(
                        identity,
                        ConsistencyStatus.FAIL,
                        "同一 Subject 的明确采用方法与另一文档的明确排除或不适用声明冲突。",
                        evidence);
                }

                return new ConsistencyResult(
                    identity,
                    ConsistencyStatus.WARNING,
                    "至少一侧未形成该 Subject 的明确 Adopted 方法集合，已 Fail Closed。",
                    evidence);
            }

            EvaluationMethodCode[] firstSet = firstFacts.Select(item => item.MethodCode)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();
            EvaluationMethodCode[] secondSet = secondFacts.Select(item => item.MethodCode)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();

            if (firstSet.SequenceEqual(secondSet))
            {
                return new ConsistencyResult(
                    identity,
                    ConsistencyStatus.PASS,
                    "同一 Subject 的明确 Adopted 方法集合完全一致。",
                    evidence);
            }

            if (IsCostFamilySpecificityDifference(firstSet, secondSet))
            {
                return new ConsistencyResult(
                    identity,
                    ConsistencyStatus.WARNING,
                    "成本法与重置成本法属于上位方法与具体方法关系，不能自动判定为等价。",
                    evidence);
            }

            bool bothComplete = firstFacts.All(item => item.SetCompleteness == EvaluationMethodSetCompleteness.Complete)
                && secondFacts.All(item => item.SetCompleteness == EvaluationMethodSetCompleteness.Complete);
            bool explicitSingleConflict = firstSet.Length == 1
                && secondSet.Length == 1
                && MethodFamily(firstSet[0]) != MethodFamily(secondSet[0]);
            if (bothComplete || explicitSingleConflict)
            {
                return new ConsistencyResult(
                    identity,
                    ConsistencyStatus.FAIL,
                    "同一 Subject 的明确 Adopted 方法集合存在确定性差异。",
                    evidence);
            }

            return new ConsistencyResult(
                identity,
                ConsistencyStatus.WARNING,
                "方法集合存在差异，但至少一侧集合完整性未知，不能确定为硬冲突。",
                evidence);
        }

        private static ConsistencyResult CompareFinalSubject(
            string subjectKey,
            IEnumerable<EvaluationMethodFact> firstFacts,
            IEnumerable<EvaluationMethodFact> secondFacts,
            IReadOnlyList<EvaluationMethodStatement> firstStatements,
            IReadOnlyList<EvaluationMethodStatement> secondStatements)
        {
            EvaluationMethodFact[] first = firstFacts.Where(item => item.MethodRole == EvaluationMethodRole.FinalAdopted).ToArray();
            EvaluationMethodFact[] second = secondFacts.Where(item => item.MethodRole == EvaluationMethodRole.FinalAdopted).ToArray();
            var identity = new ConsistencyResultIdentity(FactType.EvaluationMethod, subjectKey, FinalResultRole, ScopeKey);
            List<string> evidence = CreateEvidence(subjectKey, first, second,
                new EvaluationMethodStatement[0], new EvaluationMethodStatement[0]);
            // Explicit exclusion remains a hard rule; introducing a final-method role
            // must not weaken the existing Adopted-versus-Rejected/NotApplicable contract.
            if (HasExplicitAdoptionConflict(first, secondStatements) || HasExplicitAdoptionConflict(second, firstStatements))
                return new ConsistencyResult(identity, ConsistencyStatus.FAIL,
                    "同一 Subject 的明确最终采用方法与另一文档的明确排除或不适用声明冲突。",
                    CreateEvidence(subjectKey, first, second,
                        firstStatements.Where(item => item.Applicability == EvaluationMethodApplicability.Rejected || item.Applicability == EvaluationMethodApplicability.NotApplicable),
                        secondStatements.Where(item => item.Applicability == EvaluationMethodApplicability.Rejected || item.Applicability == EvaluationMethodApplicability.NotApplicable)));
            EvaluationMethodCode[][] sets = first.Concat(second)
                .GroupBy(item => new { item.SourceDocument, item.MethodSetId })
                .Select(group => group.Select(item => item.MethodCode).Distinct().OrderBy(item => item).ToArray())
                .ToArray();
            bool specificityOnly = false;
            for (int left = 0; left < sets.Length; left++)
            {
                for (int right = left + 1; right < sets.Length; right++)
                {
                    if (sets[left].SequenceEqual(sets[right])) continue;
                    if (IsCostFamilySpecificityDifference(sets[left], sets[right])) specificityOnly = true;
                    else return new ConsistencyResult(identity, ConsistencyStatus.FAIL,
                        "同一 Subject 的明确最终采用方法存在矛盾（不包含参与方法或独立测算结果）。", evidence);
                }
            }

            if (first.Length == 0 || second.Length == 0 || specificityOnly)
                return new ConsistencyResult(identity, ConsistencyStatus.WARNING,
                    "最终采用方法至少一侧未明确或方法层级不同，不能提升普通参与方法作为最终结论。", evidence);
            return new ConsistencyResult(identity, ConsistencyStatus.PASS,
                "同一 Subject 的明确最终采用方法一致；其他参与或测算方法不构成冲突。", evidence);
        }

        private static bool HasExplicitAdoptionConflict(
            IReadOnlyList<EvaluationMethodFact> adoptedFacts,
            IReadOnlyList<EvaluationMethodStatement> otherStatements)
        {
            return adoptedFacts.Any(fact => otherStatements.Any(statement =>
                statement.MethodCode == fact.MethodCode
                && (statement.Applicability == EvaluationMethodApplicability.Rejected
                    || statement.Applicability == EvaluationMethodApplicability.NotApplicable)));
        }

        private static bool IsCostFamilySpecificityDifference(
            IReadOnlyList<EvaluationMethodCode> first,
            IReadOnlyList<EvaluationMethodCode> second)
        {
            if (first.Count != 1 || second.Count != 1)
            {
                return false;
            }

            return (first[0] == EvaluationMethodCode.CostApproach
                    && second[0] == EvaluationMethodCode.ReplacementCostMethod)
                || (first[0] == EvaluationMethodCode.ReplacementCostMethod
                    && second[0] == EvaluationMethodCode.CostApproach);
        }

        private static EvaluationMethodFamily MethodFamily(EvaluationMethodCode method)
        {
            if (method == EvaluationMethodCode.CostApproach
                || method == EvaluationMethodCode.ReplacementCostMethod)
            {
                return EvaluationMethodFamily.Cost;
            }

            if (method == EvaluationMethodCode.MarketApproach)
            {
                return EvaluationMethodFamily.Market;
            }

            if (method == EvaluationMethodCode.IncomeApproach)
            {
                return EvaluationMethodFamily.Income;
            }

            if (method == EvaluationMethodCode.AssetBasedApproach) return EvaluationMethodFamily.AssetBased;

            return EvaluationMethodFamily.Unknown;
        }

        private static List<string> CreateEvidence(
            string subjectKey,
            IEnumerable<EvaluationMethodFact> firstFacts,
            IEnumerable<EvaluationMethodFact> secondFacts,
            IEnumerable<EvaluationMethodStatement> firstStatements,
            IEnumerable<EvaluationMethodStatement> secondStatements)
        {
            var result = new List<string>();
            foreach (EvaluationMethodFact fact in firstFacts.Concat(secondFacts))
            {
                result.Add(string.Concat(
                    fact.SourceDocument,
                    " | SubjectKey=",
                    subjectKey,
                    " | Method=",
                    fact.Method,
                    " | ",
                    fact.OriginalText));
            }

            foreach (EvaluationMethodStatement statement in firstStatements.Concat(secondStatements)
                .Where(item => item.Applicability != EvaluationMethodApplicability.Adopted))
            {
                result.Add(string.Concat(
                    statement.SourceDocument,
                    " | SubjectKey=",
                    subjectKey,
                    " | Applicability=",
                    statement.Applicability,
                    " | ",
                    statement.OriginalText));
            }

            if (result.Count == 0)
            {
                result.Add("SubjectKey=" + subjectKey + " | AdoptedMethodFactCount=0");
            }

            return result.Distinct(StringComparer.Ordinal).ToList();
        }

        private static void ValidateItems(
            IEnumerable<EvaluationMethodFact> firstFacts,
            IEnumerable<EvaluationMethodFact> secondFacts,
            IEnumerable<EvaluationMethodStatement> firstStatements,
            IEnumerable<EvaluationMethodStatement> secondStatements)
        {
            if (firstFacts.Concat(secondFacts).Any(item => item == null || !item.IsStructured))
            {
                throw new ArgumentException("EvaluationMethodComparer 只接受结构化 Adopted 方法事实。");
            }

            if (firstStatements.Concat(secondStatements).Any(item => item == null))
            {
                throw new ArgumentException("EvaluationMethodStatement 集合不能包含 null。");
            }
        }
    }
}
