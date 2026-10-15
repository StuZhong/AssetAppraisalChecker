using System;
using System.Collections.Generic;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public sealed class AssessmentValueChecker : IFactChecker
    {
        public const string Name = "AssessmentValueChecker";

        private readonly AssessmentValueFactExtractor _factExtractor;
        private readonly AssessmentValueComparer _comparer;

        public AssessmentValueChecker(
            AssessmentValueFactExtractor factExtractor,
            AssessmentValueComparer comparer)
        {
            _factExtractor = factExtractor ??
                throw new ArgumentNullException(nameof(factExtractor));
            _comparer = comparer ??
                throw new ArgumentNullException(nameof(comparer));
        }

        public string CheckerName => Name;

        public FactType FactType => FactType.AssessmentValue;

        public FactCheckExecutionEnvelope Execute(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            ValidateDocuments(firstDocument, secondDocument);

            IReadOnlyList<AssessmentValueFact> firstFacts;
            IReadOnlyList<AssessmentValueFact> secondFacts;
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
                        "评估价值 Fact 提取失败，已停止比较：",
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

            if (firstFacts.Count > 1 || secondFacts.Count > 1)
            {
                return FactCheckExecutionEnvelope.FromResult(
                    CheckerName,
                    CreateWarningResult(
                        string.Concat(
                            "第一阶段无法为两个文档各确定唯一 AssessmentValueFact。",
                            "第一个文档数量=",
                            firstFacts.Count,
                            "，第二个文档数量=",
                            secondFacts.Count,
                            "。"),
                        firstDocument,
                        secondDocument,
                        firstFacts.Count == 1 ? firstFacts[0] : null,
                        secondFacts.Count == 1 ? secondFacts[0] : null));
            }

            AssessmentValueFact firstFact =
                firstFacts.Count == 1 ? firstFacts[0] : null;
            AssessmentValueFact secondFact =
                secondFacts.Count == 1 ? secondFacts[0] : null;

            try
            {
                return FactCheckExecutionEnvelope.FromResult(
                    CheckerName,
                    _comparer.Compare(firstFact, secondFact));
            }
            catch (Exception exception)
            {
                ConsistencyResult warning = CreateWarningResult(
                    string.Concat(
                        "评估价值比较失败，未生成猜测性结论：",
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
        }

        private static ConsistencyResult CreateWarningResult(
            string reason,
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument,
            AssessmentValueFact firstFact,
            AssessmentValueFact secondFact)
        {
            var evidence = new List<string>();
            AddEvidence(evidence, firstDocument, firstFact);
            AddEvidence(evidence, secondDocument, secondFact);

            return new ConsistencyResult(
                FactType.AssessmentValue,
                ConsistencyStatus.WARNING,
                reason,
                evidence);
        }

        private static void AddEvidence(
            ICollection<string> evidence,
            V2DocumentAnalysisInput document,
            AssessmentValueFact fact)
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
                " | AssessmentValueFactCount=0"));
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
