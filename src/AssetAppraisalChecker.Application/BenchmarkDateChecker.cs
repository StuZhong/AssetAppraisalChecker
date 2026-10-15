using System;
using System.Collections.Generic;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public sealed class BenchmarkDateChecker : IFactChecker
    {
        public const string Name = "BenchmarkDateChecker";

        private readonly BenchmarkDateFactExtractor _factExtractor;
        private readonly BenchmarkDateComparer _comparer;

        public BenchmarkDateChecker(
            BenchmarkDateFactExtractor factExtractor,
            BenchmarkDateComparer comparer)
        {
            _factExtractor = factExtractor ??
                throw new ArgumentNullException(nameof(factExtractor));
            _comparer = comparer ??
                throw new ArgumentNullException(nameof(comparer));
        }

        public string CheckerName => Name;

        public FactType FactType => FactType.BenchmarkDate;

        public FactCheckExecutionEnvelope Execute(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            ValidateDocuments(firstDocument, secondDocument);

            IReadOnlyList<BenchmarkDateFact> firstFacts;
            IReadOnlyList<BenchmarkDateFact> secondFacts;
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
                return CreateFailedEnvelope(
                    "评估基准日 Fact 提取失败，已停止比较：",
                    exception,
                    firstDocument,
                    secondDocument);
            }

            if (firstFacts.Count == 0 && secondFacts.Count == 0)
            {
                return FactCheckExecutionEnvelope.NotApplicable(
                    CheckerName,
                    FactType);
            }

            try
            {
                return FactCheckExecutionEnvelope.FromResult(
                    CheckerName,
                    _comparer.Compare(firstFacts, secondFacts));
            }
            catch (Exception exception)
            {
                return CreateFailedEnvelope(
                    "评估基准日比较失败，未生成猜测性结论：",
                    exception,
                    firstDocument,
                    secondDocument);
            }
        }

        private FactCheckExecutionEnvelope CreateFailedEnvelope(
            string reasonPrefix,
            Exception exception,
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            var warning = new ConsistencyResult(
                FactType.BenchmarkDate,
                ConsistencyStatus.WARNING,
                string.Concat(reasonPrefix, exception.Message),
                new[]
                {
                    string.Concat(
                        firstDocument.SourceDocument,
                        " | DocumentStatus=",
                        firstDocument.DocumentReadResult.Status),
                    string.Concat(
                        secondDocument.SourceDocument,
                        " | DocumentStatus=",
                        secondDocument.DocumentReadResult.Status)
                });

            return FactCheckExecutionEnvelope.Failed(
                CheckerName,
                warning,
                exception.Message);
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
