using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public sealed class BenchmarkDateFact : BusinessFact
    {
        public BenchmarkDateFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence)
            : base(FactType.BenchmarkDate, sourceDocument, sourceLocation, originalText, confidence)
        {
            Evidence = CreateEvidence(
                sourceDocument,
                sourceLocation,
                originalText,
                null);
        }

        public BenchmarkDateFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string value)
            : this(
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                ParseStandardDate(value))
        {
        }

        private BenchmarkDateFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            DateTime normalizedDate)
            : base(
                FactType.BenchmarkDate,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                normalizedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        {
            NormalizedDate = normalizedDate.Date;
            Evidence = CreateEvidence(
                sourceDocument,
                sourceLocation,
                originalText,
                NormalizedDate);
        }

        public DateTime? NormalizedDate { get; }

        public IReadOnlyList<string> Evidence { get; }

        private static DateTime ParseStandardDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "评估基准日标准值不能为空。",
                    nameof(value));
            }

            DateTime parsed;
            if (!DateTime.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsed) ||
                !string.Equals(
                    value,
                    parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "评估基准日标准值必须是有效的 yyyy-MM-dd 日期。",
                    nameof(value));
            }

            return parsed.Date;
        }

        private static IReadOnlyList<string> CreateEvidence(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            DateTime? normalizedDate)
        {
            string location = string.Concat(
                sourceLocation.DocumentPart,
                " | ParagraphIndex=",
                FormatIndex(sourceLocation.ParagraphIndex),
                " | TableIndex=",
                FormatIndex(sourceLocation.TableIndex),
                " | RowIndex=",
                FormatIndex(sourceLocation.RowIndex),
                " | CellIndex=",
                FormatIndex(sourceLocation.CellIndex));

            return new ReadOnlyCollection<string>(
                new[]
                {
                    string.Concat(
                        sourceDocument,
                        " | ",
                        location,
                        " | ",
                        originalText,
                        " | NormalizedDate=",
                        normalizedDate.HasValue
                            ? normalizedDate.Value.ToString(
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture)
                            : "<missing>")
                });
        }

        private static string FormatIndex(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "<none>";
        }
    }
}
