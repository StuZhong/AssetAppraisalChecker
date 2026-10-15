using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.Application
{
    public sealed class AssessmentObjectChecker : IFactChecker
    {
        public const string Name = "AssessmentObjectChecker";

        private readonly AssessmentObjectFactExtractor _factExtractor;
        private readonly AssessmentObjectSemanticEnhancer _semanticEnhancer;
        private readonly AssessmentObjectSemanticAwareComparer _comparer;

        public AssessmentObjectChecker(
            AssessmentObjectFactExtractor factExtractor,
            AssessmentObjectSemanticEnhancer semanticEnhancer,
            AssessmentObjectSemanticAwareComparer comparer)
        {
            _factExtractor = factExtractor ??
                throw new ArgumentNullException(nameof(factExtractor));
            _semanticEnhancer = semanticEnhancer ??
                throw new ArgumentNullException(nameof(semanticEnhancer));
            _comparer = comparer ??
                throw new ArgumentNullException(nameof(comparer));
        }

        public string CheckerName => Name;

        public FactType FactType => FactType.AssessmentObject;

        public FactCheckExecutionEnvelope Execute(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            ValidateDocuments(firstDocument, secondDocument);
            FactCheckExecutionEnvelope objectExecution = ExecuteObject(firstDocument, secondDocument);
            var extractor = new AppraisedEntityFactExtractor();
            IReadOnlyList<AppraisedEntityFact> first = extractor.Extract(
                firstDocument.DocumentReadResult, firstDocument.SourceDocument);
            IReadOnlyList<AppraisedEntityFact> second = extractor.Extract(
                secondDocument.DocumentReadResult, secondDocument.SourceDocument);
            if (first.Count == 0 && second.Count == 0) return objectExecution;

            ConsistencyResult entityResult = CompareAppraisedEntity(first, second, firstDocument, secondDocument);
            ConsistencyResult[] results = objectExecution.Results.Concat(new[] { entityResult }).ToArray();
            return objectExecution.ExecutionStatus == FactCheckExecutionStatus.Failed
                ? FactCheckExecutionEnvelope.Failed(CheckerName, FactType, results,
                    objectExecution.ErrorInformation, objectExecution.ExecutionEvidence)
                : FactCheckExecutionEnvelope.FromResults(CheckerName, FactType, results,
                    objectExecution.ExecutionEvidence);
        }

        private static ConsistencyResult CompareAppraisedEntity(
            IReadOnlyList<AppraisedEntityFact> first, IReadOnlyList<AppraisedEntityFact> second,
            V2DocumentAnalysisInput firstDocument, V2DocumentAnalysisInput secondDocument)
        {
            var identity = new ConsistencyResultIdentity(FactType.AssessmentObject,
                ConsistencyResultIdentity.DefaultSubjectKey, AppraisedEntityFact.ResultRole,
                ConsistencyResultIdentity.DefaultScopeKey);
            // Multiple different role assertions are not a licence to pair by index
            // or select a majority. Compare only unique canonical company identities.
            bool unique = first.Select(item => item.CompanyName).Distinct(StringComparer.Ordinal).Count() == 1
                && second.Select(item => item.CompanyName).Distinct(StringComparer.Ordinal).Count() == 1;
            if (!unique)
                return new ConsistencyResult(identity, ConsistencyStatus.WARNING,
                    "被评估单位字段缺失、简称关系不确定或存在竞争表述，未推断主体身份。",
                    new[] { firstDocument.SourceDocument + " | 无法唯一确定被评估单位",
                        secondDocument.SourceDocument + " | 无法唯一确定被评估单位" });

            ConsistencyResult comparison = new BasicFactComparer().Compare(
                new BusinessFact[] { first[0] }, new BusinessFact[] { second[0] }).Single();
            return new ConsistencyResult(identity, comparison.Status,
                comparison.Status == ConsistencyStatus.PASS
                    ? "两份文档明确陈述的被评估单位一致。"
                    : "两份文档明确陈述的被评估单位名称不一致；未将委托人、权利人或评估对象混作被评估单位。",
                first.Concat(second).Select(item => item.SourceDocument + " | " + item.OriginalText));
        }

        private FactCheckExecutionEnvelope ExecuteObject(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            ValidateDocuments(firstDocument, secondDocument);

            IReadOnlyList<AssessmentObjectFact> firstFacts;
            IReadOnlyList<AssessmentObjectFact> secondFacts;
            try
            {
                firstFacts = _factExtractor.Extract(
                    firstDocument.DocumentReadResult,
                    firstDocument.SourceDocument);
                secondFacts = _factExtractor.Extract(
                    secondDocument.DocumentReadResult,
                    secondDocument.SourceDocument);
            }
            catch (Exception exception)
            {
                ConsistencyResult warning = CreateWarningResult(
                    string.Concat(
                        "评估对象 Fact 提取失败，已停止比较：",
                        exception.Message),
                    firstDocument,
                    secondDocument,
                    null,
                    null);
                return FactCheckExecutionEnvelope.Failed(
                    CheckerName,
                    warning,
                    exception.Message);
            }

            if (firstFacts.Count == 0 && secondFacts.Count == 0)
            {
                return FactCheckExecutionEnvelope.NotApplicable(
                    CheckerName,
                    FactType);
            }

            if (firstFacts.Concat(secondFacts).Any(fact => fact.IsEquity))
                return CompareEquityObjects(firstFacts, secondFacts);

            if (firstFacts.Count != 1 || secondFacts.Count != 1)
            {
                return FactCheckExecutionEnvelope.FromResult(
                    CheckerName,
                    CreateWarningResult(
                        string.Concat(
                            "无法为两个文档各确定唯一 AssessmentObjectFact。",
                            "第一个文档数量=",
                            firstFacts.Count,
                            "，第二个文档数量=",
                            secondFacts.Count,
                            "。"),
                        firstDocument,
                        secondDocument,
                        null,
                        null));
            }

            AssessmentObjectFact firstFact = firstFacts[0];
            AssessmentObjectFact secondFact = secondFacts[0];

            ConsistencyResult rulePrecheck;
            try
            {
                rulePrecheck = _comparer.CompareWithSemanticResults(
                    firstFact,
                    secondFact,
                    null);
            }
            catch (Exception exception)
            {
                ConsistencyResult warning = CreateWarningResult(
                    string.Concat(
                        "评估对象规则预判失败，已停止比较：",
                        exception.Message),
                    firstDocument,
                    secondDocument,
                    firstFact,
                    secondFact);
                return FactCheckExecutionEnvelope.Failed(
                    CheckerName,
                    warning,
                    exception.Message);
            }

            if (rulePrecheck.Status != ConsistencyStatus.WARNING)
            {
                return FactCheckExecutionEnvelope.FromResult(
                    CheckerName,
                    rulePrecheck);
            }

            IReadOnlyList<SemanticFieldComparisonResult> semanticResults;
            try
            {
                semanticResults = _semanticEnhancer.Enhance(
                    firstFact,
                    secondFact);
            }
            catch (InvalidOperationException exception)
            {
                try
                {
                    ConsistencyResult warning =
                        _comparer.CompareWithSemanticResults(
                            firstFact,
                            secondFact,
                            null);
                    return FactCheckExecutionEnvelope.Failed(
                        CheckerName,
                        warning,
                        exception.Message);
                }
                catch (Exception comparisonException)
                {
                    return CreateFailedComparisonEnvelope(
                        comparisonException,
                        firstDocument,
                        secondDocument,
                        firstFact,
                        secondFact);
                }
            }

            try
            {
                return FactCheckExecutionEnvelope.FromResult(
                    CheckerName,
                    _comparer.CompareWithSemanticResults(
                        firstFact,
                        secondFact,
                        semanticResults));
            }
            catch (Exception exception)
            {
                return CreateFailedComparisonEnvelope(
                    exception,
                    firstDocument,
                    secondDocument,
                    firstFact,
                    secondFact);
            }
        }

        private FactCheckExecutionEnvelope CompareEquityObjects(
            IReadOnlyList<AssessmentObjectFact> first, IReadOnlyList<AssessmentObjectFact> second)
        {
            AssessmentObjectFact[] all = first.Concat(second).ToArray();
            // Repeated equivalent assertions keep all provenance, but never get
            // paired by occurrence index or selected using a majority vote.
            bool unique = first.Count > 0 && second.Count > 0 && all.All(fact => fact.IsEquity)
                && first.Select(fact => fact.Value).Distinct(StringComparer.Ordinal).Count() == 1
                && second.Select(fact => fact.Value).Distinct(StringComparer.Ordinal).Count() == 1;
            string[] evidence = all.Select(fact => fact.SourceDocument + " | " + fact.OriginalText).ToArray();
            ConsistencyResult comparison = unique
                ? _comparer.CompareWithSemanticResults(first[0], second[0], null)
                : new ConsistencyResult(FactType, ConsistencyStatus.WARNING,
                    "评估对象缺失、口径不同或存在多个对象表述，未猜测主体、权益类型或股权比例。",
                    evidence);
            return FactCheckExecutionEnvelope.FromResult(CheckerName, new ConsistencyResult(
                FactType, comparison.Status, comparison.Reason,
                evidence));
        }

        private FactCheckExecutionEnvelope CreateFailedComparisonEnvelope(
            Exception exception,
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument,
            AssessmentObjectFact firstFact,
            AssessmentObjectFact secondFact)
        {
            ConsistencyResult warning = CreateWarningResult(
                string.Concat(
                    "评估对象最终比较失败，未生成猜测性结论：",
                    exception.Message),
                firstDocument,
                secondDocument,
                firstFact,
                secondFact);
            return FactCheckExecutionEnvelope.Failed(
                CheckerName,
                warning,
                exception.Message);
        }

        private static ConsistencyResult CreateWarningResult(
            string reason,
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument,
            AssessmentObjectFact firstFact,
            AssessmentObjectFact secondFact)
        {
            var evidence = new List<string>();
            AddEvidence(evidence, firstDocument, firstFact);
            AddEvidence(evidence, secondDocument, secondFact);

            return new ConsistencyResult(
                FactType.AssessmentObject,
                ConsistencyStatus.WARNING,
                reason,
                evidence);
        }

        private static void AddEvidence(
            ICollection<string> evidence,
            V2DocumentAnalysisInput document,
            AssessmentObjectFact fact)
        {
            if (fact != null)
            {
                evidence.Add(string.Concat(
                    fact.SourceDocument,
                    " | ",
                    fact.OriginalText));
                return;
            }

            evidence.Add(string.Concat(
                document.SourceDocument,
                " | DocumentStatus=",
                document.DocumentReadResult.Status));
        }

        private static void ValidateDocuments(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            if (firstDocument == null)
            {
                throw new ArgumentNullException(nameof(firstDocument));
            }

            if (secondDocument == null)
            {
                throw new ArgumentNullException(nameof(secondDocument));
            }
        }
    }
}
