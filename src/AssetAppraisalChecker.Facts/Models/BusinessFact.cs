using System;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public abstract class BusinessFact
    {
        protected BusinessFact(
            FactType factType,
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence)
        {
            if (factType == FactType.Unknown || !Enum.IsDefined(typeof(FactType), factType))
            {
                throw new ArgumentOutOfRangeException(nameof(factType));
            }

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("事实来源文档不能为空。", nameof(sourceDocument));
            }

            if (sourceLocation == null)
            {
                throw new ArgumentNullException(nameof(sourceLocation));
            }

            if (string.IsNullOrWhiteSpace(originalText))
            {
                throw new ArgumentException("事实原始文本不能为空。", nameof(originalText));
            }

            if (confidence == null)
            {
                throw new ArgumentNullException(nameof(confidence));
            }

            FactType = factType;
            SourceDocument = sourceDocument;
            SourceLocation = sourceLocation;
            OriginalText = originalText;
            Confidence = confidence;
        }

        protected BusinessFact(
            FactType factType,
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string value)
            : this(
                factType,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence)
        {
            Value = value;
        }

        public string Value { get; }

        public FactType FactType { get; }

        public string SourceDocument { get; }

        public DocumentLocation SourceLocation { get; }

        public string OriginalText { get; }

        public FactConfidence Confidence { get; }
    }
}
