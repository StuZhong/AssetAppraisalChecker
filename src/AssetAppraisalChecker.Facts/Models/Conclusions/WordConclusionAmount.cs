using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Facts.Models.Conclusions
{
    public sealed class WordConclusionAmount
    {
        public WordConclusionAmount(string sourceDocument, WordConclusionTarget target,
            WordConclusionSourceKind sourceKind, ValueCategory valueCategory, MoneyOccurrence occurrence,
            DocumentLocation originalLocation, string evidence)
        {
            if (occurrence == null) throw new ArgumentNullException(nameof(occurrence));
            SourceDocument = sourceDocument ?? throw new ArgumentNullException(nameof(sourceDocument));
            Target = target;
            SourceKind = sourceKind;
            ValueCategory = valueCategory;
            TaxBasis = occurrence.FieldIdentity.TaxBasis;
            RawText = occurrence.RawText;
            NormalizedAmountYuan = occurrence.NormalizedAmountYuan;
            NumberFormat = occurrence.MoneyNumberFormat;
            Location = originalLocation ?? throw new ArgumentNullException(nameof(originalLocation));
            TextStartIndex = occurrence.TextStartIndex;
            TextLength = occurrence.TextLength;
            NearbyContext = occurrence.NearbyContext;
            ParseWarnings = new ReadOnlyCollection<string>(occurrence.ParseWarnings.ToList());
            Evidence = evidence ?? string.Empty;
        }

        public string SourceDocument { get; }
        public WordConclusionTarget Target { get; }
        public WordConclusionSourceKind SourceKind { get; }
        public ValueCategory ValueCategory { get; }
        public TaxBasis TaxBasis { get; }
        public string RawText { get; }
        public decimal NormalizedAmountYuan { get; }
        public MoneyNumberFormat NumberFormat { get; }
        public DocumentLocation Location { get; }
        public int TextStartIndex { get; }
        public int TextLength { get; }
        public DocumentNearbyContext NearbyContext { get; }
        public IReadOnlyList<string> ParseWarnings { get; }
        public string Evidence { get; }
    }
}
