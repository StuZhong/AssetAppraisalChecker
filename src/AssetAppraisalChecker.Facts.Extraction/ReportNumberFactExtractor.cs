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
    public sealed class ReportNumberFactExtractor
    {
        private readonly BasicFieldExtractor _basicFieldExtractor;

        public ReportNumberFactExtractor()
            : this(new BasicFieldExtractor())
        {
        }

        internal ReportNumberFactExtractor(
            BasicFieldExtractor basicFieldExtractor)
        {
            _basicFieldExtractor = basicFieldExtractor ??
                throw new ArgumentNullException(nameof(basicFieldExtractor));
        }

        public IReadOnlyList<ReportNumberFact> Extract(
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

            List<ReportNumberFact> facts = _basicFieldExtractor
                .Extract(documentReadResult)
                .Where(IsSupportedReportNumber)
                .Select(occurrence => new ReportNumberFact(
                    sourceDocument,
                    occurrence.Location,
                    occurrence.RawValue,
                    MapConfidence(occurrence.Confidence),
                    occurrence.NormalizedValue.CanonicalValue,
                    occurrence.Evidence.Concat(
                        new[] { string.Concat("SourceText=", occurrence.RawText) })))
                .ToList();

            return new ReadOnlyCollection<ReportNumberFact>(facts);
        }

        private static bool IsSupportedReportNumber(
            BasicFieldOccurrence occurrence)
        {
            return occurrence.FieldType == BasicFieldType.ReportNumber &&
                   occurrence.HasNormalizedValue &&
                   occurrence.NormalizedValue.Kind == BasicFieldValueKind.Text &&
                   !string.IsNullOrWhiteSpace(
                       occurrence.NormalizedValue.CanonicalValue) &&
                   !string.IsNullOrWhiteSpace(occurrence.RawText) &&
                   !string.IsNullOrWhiteSpace(occurrence.RawValue);
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
