using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public sealed class ReportNumberFact : BusinessFact
    {
        private const string CandidateBasis =
            "CandidateBasis=DeterministicCoreCandidate";

        public ReportNumberFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string normalizedNumber)
            : this(
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                normalizedNumber,
                new string[0])
        {
        }

        public ReportNumberFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string normalizedNumber,
            IEnumerable<string> extractionEvidence)
            : base(
                FactType.ReportNumber,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                ValidateNormalizedNumber(normalizedNumber))
        {
            NormalizedNumber = Value;
            Evidence = CreateEvidence(
                sourceDocument,
                sourceLocation,
                originalText,
                NormalizedNumber,
                extractionEvidence);
        }

        public string NormalizedNumber { get; }

        public IReadOnlyList<string> Evidence { get; }

        private static string ValidateNormalizedNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "报告编号标准值不能为空。",
                    nameof(value));
            }

            BasicFieldNormalizationResult normalization =
                BasicFieldNormalizer.Normalize(
                    BasicFieldType.ReportNumber,
                    value);

            if (!normalization.IsSuccess ||
                normalization.NormalizedValue == null ||
                !string.Equals(
                    value,
                    normalization.NormalizedValue.CanonicalValue,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "报告编号必须是已完成括号和空格归一化的完整标准值。",
                    nameof(value));
            }

            return value;
        }

        private static IReadOnlyList<string> CreateEvidence(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            string normalizedNumber,
            IEnumerable<string> extractionEvidence)
        {
            if (extractionEvidence == null)
            {
                throw new ArgumentNullException(nameof(extractionEvidence));
            }

            List<string> sourceEvidence = extractionEvidence.ToList();
            if (sourceEvidence.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "报告编号提取证据不能包含空项。",
                    nameof(extractionEvidence));
            }

            var evidence = new List<string>
            {
                string.Concat(
                    sourceDocument,
                    " | ",
                    sourceLocation.DocumentPart,
                    " | ParagraphIndex=",
                    FormatIndex(sourceLocation.ParagraphIndex),
                    " | TableIndex=",
                    FormatIndex(sourceLocation.TableIndex),
                    " | RowIndex=",
                    FormatIndex(sourceLocation.RowIndex),
                    " | CellIndex=",
                    FormatIndex(sourceLocation.CellIndex),
                    " | OriginalText=",
                    originalText,
                    " | NormalizedNumber=",
                    normalizedNumber,
                    " | ",
                    CandidateBasis)
            };

            evidence.AddRange(sourceEvidence.Select(
                item => string.Concat("CoreEvidence=", item)));
            return new ReadOnlyCollection<string>(evidence);
        }

        private static string FormatIndex(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "<none>";
        }
    }
}
