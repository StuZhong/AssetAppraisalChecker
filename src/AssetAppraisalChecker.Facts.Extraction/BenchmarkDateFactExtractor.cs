using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    public sealed class BenchmarkDateFactExtractor
    {
        private readonly BasicFieldExtractor basicFieldExtractor;

        public BenchmarkDateFactExtractor()
            : this(new BasicFieldExtractor())
        {
        }

        internal BenchmarkDateFactExtractor(BasicFieldExtractor basicFieldExtractor)
        {
            this.basicFieldExtractor = basicFieldExtractor ??
                throw new ArgumentNullException(nameof(basicFieldExtractor));
        }

        public IReadOnlyList<BenchmarkDateFact> Extract(
            DocumentReadResult documentReadResult,
            string sourceDocument)
        {
            if (documentReadResult == null)
            {
                throw new ArgumentNullException(nameof(documentReadResult));
            }

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException(
                    "事实来源文档不能为空。",
                    nameof(sourceDocument));
            }

            List<BenchmarkDateFact> facts = basicFieldExtractor
                .Extract(documentReadResult)
                .Where(IsSupportedBenchmarkDate)
                .Select(occurrence => new BenchmarkDateFact(
                    sourceDocument,
                    occurrence.Location,
                    occurrence.RawText,
                    MapConfidence(occurrence.Confidence),
                    occurrence.NormalizedValue.CanonicalValue))
                .ToList();

            return new ReadOnlyCollection<BenchmarkDateFact>(facts);
        }

        private static bool IsSupportedBenchmarkDate(BasicFieldOccurrence occurrence)
        {
            return occurrence.FieldType == BasicFieldType.ValuationDate &&
                   occurrence.HasNormalizedValue &&
                   occurrence.NormalizedValue.DateValue.HasValue &&
                   !string.IsNullOrWhiteSpace(occurrence.RawText);
        }

        private static FactConfidence MapConfidence(
            ClassificationConfidence confidence)
        {
            switch (confidence)
            {
                case ClassificationConfidence.High:
                    return new FactConfidence(FactConfidenceBand.High, 0.95m);
                case ClassificationConfidence.Medium:
                    return new FactConfidence(FactConfidenceBand.Medium, 0.75m);
                case ClassificationConfidence.Low:
                    return new FactConfidence(FactConfidenceBand.Low, 0.50m);
                default:
                    throw new ArgumentOutOfRangeException(nameof(confidence));
            }
        }
    }
}
