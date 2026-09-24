using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    public sealed class MoneyOccurrence
    {
        public MoneyOccurrence(
            string rawText,
            decimal normalizedAmountYuan,
            MoneyParseStatus moneyParseStatus,
            MoneyNumberFormat moneyNumberFormat,
            MoneyUnit moneyUnit,
            IEnumerable<string> parseWarnings,
            FieldIdentity fieldIdentity,
            DocumentLocation location,
            DocumentNearbyContext nearbyContext,
            ClassificationConfidence classificationConfidence,
            ClassificationConfidence valueCategoryConfidence,
            ClassificationConfidence taxBasisConfidence,
            ClassificationConfidence scopeConfidence,
            IEnumerable<string> classificationEvidence,
            ComparisonSubjectKey comparisonSubjectKey,
            ClassificationConfidence subjectConfidence,
            IEnumerable<string> subjectEvidence,
            int textStartIndex,
            int textLength)
        {
            if (fieldIdentity == null)
            {
                throw new ArgumentNullException(nameof(fieldIdentity));
            }

            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (nearbyContext == null)
            {
                throw new ArgumentNullException(nameof(nearbyContext));
            }

            if (comparisonSubjectKey == null)
            {
                throw new ArgumentNullException(nameof(comparisonSubjectKey));
            }

            if (textStartIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textStartIndex));
            }

            if (textLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textLength));
            }

            if (string.IsNullOrEmpty(rawText))
            {
                throw new ArgumentException("金额原文不能为空。", nameof(rawText));
            }

            if (rawText.Length != textLength)
            {
                throw new ArgumentException("金额原文长度必须与 TextLength 一致。", nameof(textLength));
            }

            RawText = rawText;
            NormalizedAmountYuan = normalizedAmountYuan;
            MoneyParseStatus = moneyParseStatus;
            MoneyNumberFormat = moneyNumberFormat;
            MoneyUnit = moneyUnit;
            ParseWarnings = new ReadOnlyCollection<string>(
                (parseWarnings ?? Enumerable.Empty<string>()).ToList());
            FieldIdentity = fieldIdentity;
            Location = location;
            NearbyContext = nearbyContext;
            ClassificationConfidence = classificationConfidence;
            ValueCategoryConfidence = valueCategoryConfidence;
            TaxBasisConfidence = taxBasisConfidence;
            ScopeConfidence = scopeConfidence;
            ClassificationEvidence = new ReadOnlyCollection<string>(
                (classificationEvidence ?? Enumerable.Empty<string>()).ToList());
            ComparisonSubjectKey = comparisonSubjectKey;
            SubjectConfidence = subjectConfidence;
            SubjectEvidence = new ReadOnlyCollection<string>(
                (subjectEvidence ?? Enumerable.Empty<string>()).ToList());
            TextStartIndex = textStartIndex;
            TextLength = textLength;
        }

        public string RawText { get; }

        public decimal NormalizedAmountYuan { get; }

        public MoneyParseStatus MoneyParseStatus { get; }

        public MoneyNumberFormat MoneyNumberFormat { get; }

        public MoneyUnit MoneyUnit { get; }

        public IReadOnlyList<string> ParseWarnings { get; }

        public FieldIdentity FieldIdentity { get; }

        public DocumentLocation Location { get; }

        public DocumentNearbyContext NearbyContext { get; }

        public ClassificationConfidence ClassificationConfidence { get; }

        public ClassificationConfidence ValueCategoryConfidence { get; }

        public ClassificationConfidence TaxBasisConfidence { get; }

        public ClassificationConfidence ScopeConfidence { get; }

        public IReadOnlyList<string> ClassificationEvidence { get; }

        public ComparisonSubjectKey ComparisonSubjectKey { get; }

        public SubjectKind SubjectKind => ComparisonSubjectKey.SubjectKind;

        public string SubjectId => ComparisonSubjectKey.NormalizedSubjectId;

        public ClassificationConfidence SubjectConfidence { get; }

        public IReadOnlyList<string> SubjectEvidence { get; }

        /// <summary>
        /// Zero-based character index relative to the source DocumentElement.Text.
        /// </summary>
        public int TextStartIndex { get; }

        public int TextLength { get; }
    }
}
