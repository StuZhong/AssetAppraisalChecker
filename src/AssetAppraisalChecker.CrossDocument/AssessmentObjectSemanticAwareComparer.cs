using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class AssessmentObjectSemanticAwareComparer
    {
        public const decimal DefaultMinimumSimilarityScore = 0.80m;
        public const decimal DefaultMinimumConfidence = 0.80m;

        private readonly AssessmentObjectSemanticEnhancer _semanticEnhancer;
        private readonly decimal _minimumSimilarityScore;
        private readonly decimal _minimumConfidence;

        public AssessmentObjectSemanticAwareComparer(ISemanticAnalyzer semanticAnalyzer)
            : this(
                semanticAnalyzer,
                DefaultMinimumSimilarityScore,
                DefaultMinimumConfidence)
        {
        }

        public AssessmentObjectSemanticAwareComparer(
            ISemanticAnalyzer semanticAnalyzer,
            decimal minimumSimilarityScore,
            decimal minimumConfidence)
        {
            if (semanticAnalyzer == null)
            {
                throw new ArgumentNullException(nameof(semanticAnalyzer));
            }

            ValidateUnitInterval(minimumSimilarityScore, nameof(minimumSimilarityScore));
            ValidateUnitInterval(minimumConfidence, nameof(minimumConfidence));

            _semanticEnhancer = new AssessmentObjectSemanticEnhancer(semanticAnalyzer);
            _minimumSimilarityScore = minimumSimilarityScore;
            _minimumConfidence = minimumConfidence;
        }

        public ConsistencyResult Compare(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact)
        {
            ValidateFacts(originalFact, candidateFact);

            ConsistencyResult hardRuleResult = CompareHardRuleFields(
                originalFact,
                candidateFact);
            if (hardRuleResult != null)
            {
                return hardRuleResult;
            }

            IReadOnlyList<FieldPair> semanticDifferences = GetSemanticDifferences(
                originalFact,
                candidateFact);
            if (semanticDifferences.Count == 0)
            {
                return CreateResult(
                    ConsistencyStatus.PASS,
                    "评估对象的硬规则字段和 Semantic 字段均完全一致。",
                    BuildFactEvidence(originalFact, candidateFact));
            }

            try
            {
                IReadOnlyList<SemanticFieldComparisonResult> semanticResults =
                    _semanticEnhancer.Enhance(originalFact, candidateFact);

                return EvaluateSemanticResults(
                    originalFact,
                    candidateFact,
                    semanticDifferences,
                    semanticResults);
            }
            catch (InvalidOperationException exception)
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    string.Concat(
                        "Semantic 分析失败，无法可靠判断允许字段是否一致：",
                        exception.Message),
                    BuildFactEvidence(originalFact, candidateFact));
            }
        }

        public ConsistencyResult CompareWithSemanticResults(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact,
            IEnumerable<SemanticFieldComparisonResult> semanticResults)
        {
            ValidateFacts(originalFact, candidateFact);

            ConsistencyResult hardRuleResult = CompareHardRuleFields(
                originalFact,
                candidateFact);
            if (hardRuleResult != null)
            {
                return hardRuleResult;
            }

            IReadOnlyList<FieldPair> semanticDifferences = GetSemanticDifferences(
                originalFact,
                candidateFact);
            if (semanticDifferences.Count == 0)
            {
                if (semanticResults != null && semanticResults.Any())
                {
                    return CreateResult(
                        ConsistencyStatus.WARNING,
                        "不存在需要 Semantic 判断的差异字段，但收到了额外 Semantic 结果。",
                        BuildFactEvidence(originalFact, candidateFact));
                }

                return CreateResult(
                    ConsistencyStatus.PASS,
                    "评估对象的硬规则字段和 Semantic 字段均完全一致。",
                    BuildFactEvidence(originalFact, candidateFact));
            }

            if (semanticResults == null)
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    "未提供 Semantic 结果，无法可靠判断允许字段是否一致。",
                    BuildFactEvidence(originalFact, candidateFact));
            }

            return EvaluateSemanticResults(
                originalFact,
                candidateFact,
                semanticDifferences,
                semanticResults.ToList());
        }

        private ConsistencyResult EvaluateSemanticResults(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact,
            IReadOnlyList<FieldPair> semanticDifferences,
            IReadOnlyList<SemanticFieldComparisonResult> semanticResults)
        {
            List<string> evidence = BuildFactEvidence(originalFact, candidateFact);

            if (semanticResults == null ||
                semanticResults.Any(result => result == null))
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    "Semantic 结果缺失或包含空结果，无法可靠判断一致性。",
                    evidence);
            }

            ILookup<string, SemanticFieldComparisonResult> resultsByField =
                semanticResults.ToLookup(result => result.FieldName, StringComparer.Ordinal);

            if (semanticResults.Count != semanticDifferences.Count ||
                resultsByField.Any(group => group.Count() != 1))
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    "Semantic 结果存在缺失、额外字段或多候选，无法可靠选择唯一结果。",
                    evidence);
            }

            foreach (SemanticFieldComparisonResult semanticResult in semanticResults)
            {
                FieldPair expectedPair = semanticDifferences.SingleOrDefault(
                    pair => string.Equals(
                        pair.FieldName,
                        semanticResult.FieldName,
                        StringComparison.Ordinal));

                if (expectedPair == null ||
                    !string.Equals(
                        expectedPair.OriginalValue,
                        semanticResult.OriginalValue,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expectedPair.CandidateValue,
                        semanticResult.CandidateValue,
                        StringComparison.Ordinal))
                {
                    return CreateResult(
                        ConsistencyStatus.WARNING,
                        "Semantic 结果与原始 Fact 字段不匹配，已拒绝该结果。",
                        evidence);
                }

                evidence.Add(string.Concat(
                    "Semantic | ",
                    semanticResult.FieldName,
                    " | SimilarityScore=",
                    semanticResult.SimilarityScore,
                    " | Confidence=",
                    semanticResult.Confidence));
                evidence.AddRange(semanticResult.Evidence);

                if (semanticResult.SimilarityScore < _minimumSimilarityScore ||
                    semanticResult.Confidence < _minimumConfidence)
                {
                    return CreateResult(
                        ConsistencyStatus.WARNING,
                        string.Concat(
                            "字段 ",
                            semanticResult.FieldName,
                            " 的 Semantic 分数或置信度不足，无法确认一致。"),
                        evidence);
                }
            }

            return CreateResult(
                ConsistencyStatus.PASS,
                "硬规则字段一致，所有差异字段均通过 Semantic 一致性门槛。",
                evidence);
        }

        private static ConsistencyResult CompareHardRuleFields(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact)
        {
            if (originalFact.IsEquity || candidateFact.IsEquity)
            {
                if (!originalFact.IsEquity || !candidateFact.IsEquity)
                    return CreateResult(ConsistencyStatus.WARNING,
                        "股权类对象与具体资产对象口径不同，未推断其等价或冲突。",
                        BuildFactEvidence(originalFact, candidateFact));
                ConsistencyResult comparison = new BasicFactComparer().Compare(
                    new BusinessFact[] { originalFact }, new BusinessFact[] { candidateFact }).Single();
                return CreateResult(comparison.Status,
                    comparison.Status == ConsistencyStatus.PASS
                        ? "明确评估对象的主体、权益类型与股权比例一致。"
                        : "明确评估对象的主体、权益类型或股权比例存在冲突；未将被评估单位或评估范围作为评估对象。",
                    BuildFactEvidence(originalFact, candidateFact));
            }
            List<string> evidence = BuildFactEvidence(originalFact, candidateFact);
            List<string> missingFields = new List<string>();

            AddMissingField(missingFields, "Owner", originalFact.Owner, candidateFact.Owner);
            if (!originalFact.Quantity.HasValue || !candidateFact.Quantity.HasValue)
            {
                missingFields.Add("Quantity");
            }

            AddMissingField(
                missingFields,
                "AssetType",
                originalFact.AssetType,
                candidateFact.AssetType);
            AddMissingField(
                missingFields,
                "EconomicAction",
                originalFact.EconomicAction,
                candidateFact.EconomicAction);
            AddMissingField(missingFields, "Scope", originalFact.Scope, candidateFact.Scope);

            List<string> conflictingFields = new List<string>();
            if (!string.IsNullOrWhiteSpace(originalFact.Owner) &&
                !string.IsNullOrWhiteSpace(candidateFact.Owner) &&
                !string.Equals(
                    originalFact.Owner,
                    candidateFact.Owner,
                    StringComparison.Ordinal))
            {
                conflictingFields.Add("Owner");
            }

            if (originalFact.Quantity.HasValue &&
                candidateFact.Quantity.HasValue &&
                originalFact.Quantity.Value != candidateFact.Quantity.Value)
            {
                conflictingFields.Add("Quantity");
            }

            if (conflictingFields.Count > 0)
            {
                return CreateResult(
                    ConsistencyStatus.FAIL,
                    string.Concat(
                        "评估对象硬规则字段明确冲突：",
                        string.Join("、", conflictingFields)),
                    evidence);
            }

            if (missingFields.Count > 0)
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    string.Concat(
                        "评估对象字段缺失，无法可靠判断一致性：",
                        string.Join("、", missingFields)),
                    evidence);
            }

            return null;
        }

        private static IReadOnlyList<FieldPair> GetSemanticDifferences(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact)
        {
            List<FieldPair> differences = new List<FieldPair>();
            AddDifference(
                differences,
                "AssetType",
                originalFact.AssetType,
                candidateFact.AssetType);
            AddDifference(
                differences,
                "EconomicAction",
                originalFact.EconomicAction,
                candidateFact.EconomicAction);
            AddDifference(
                differences,
                "Scope",
                originalFact.Scope,
                candidateFact.Scope);

            return differences;
        }

        private static void AddDifference(
            ICollection<FieldPair> differences,
            string fieldName,
            string originalValue,
            string candidateValue)
        {
            if (!string.Equals(originalValue, candidateValue, StringComparison.Ordinal))
            {
                differences.Add(new FieldPair(
                    fieldName,
                    originalValue,
                    candidateValue));
            }
        }

        private static void AddMissingField(
            ICollection<string> missingFields,
            string fieldName,
            string originalValue,
            string candidateValue)
        {
            if (string.IsNullOrWhiteSpace(originalValue) ||
                string.IsNullOrWhiteSpace(candidateValue))
            {
                missingFields.Add(fieldName);
            }
        }

        private static List<string> BuildFactEvidence(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact)
        {
            return new List<string>
            {
                string.Concat(
                    originalFact.SourceDocument,
                    " | ",
                    originalFact.OriginalText),
                string.Concat(
                    candidateFact.SourceDocument,
                    " | ",
                    candidateFact.OriginalText)
            };
        }

        private static ConsistencyResult CreateResult(
            ConsistencyStatus status,
            string reason,
            IEnumerable<string> evidence)
        {
            return new ConsistencyResult(
                FactType.AssessmentObject,
                status,
                reason,
                evidence);
        }

        private static void ValidateFacts(
            AssessmentObjectFact originalFact,
            AssessmentObjectFact candidateFact)
        {
            if (originalFact == null)
            {
                throw new ArgumentNullException(nameof(originalFact));
            }

            if (candidateFact == null)
            {
                throw new ArgumentNullException(nameof(candidateFact));
            }
        }

        private static void ValidateUnitInterval(decimal value, string parameterName)
        {
            if (value < 0m || value > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "数值必须在 0 到 1 之间。");
            }
        }

        private sealed class FieldPair
        {
            public FieldPair(
                string fieldName,
                string originalValue,
                string candidateValue)
            {
                FieldName = fieldName;
                OriginalValue = originalValue;
                CandidateValue = candidateValue;
            }

            public string FieldName { get; }

            public string OriginalValue { get; }

            public string CandidateValue { get; }
        }
    }
}
