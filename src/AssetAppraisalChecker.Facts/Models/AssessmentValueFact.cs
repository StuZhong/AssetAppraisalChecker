using System.Globalization;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public sealed class AssessmentValueFact : BusinessFact
    {
        public AssessmentValueFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence)
            : base(FactType.AssessmentValue, sourceDocument, sourceLocation, originalText, confidence)
        {
        }

        public AssessmentValueFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string value)
            : base(
                FactType.AssessmentValue,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                value)
        {
            decimal normalizedAmountYuan;
            if (decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out normalizedAmountYuan))
            {
                NormalizedAmountYuan = normalizedAmountYuan;
            }
        }

        public AssessmentValueFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            decimal normalizedAmountYuan)
            : base(
                FactType.AssessmentValue,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                normalizedAmountYuan.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture))
        {
            NormalizedAmountYuan = normalizedAmountYuan;
        }

        public decimal? NormalizedAmountYuan { get; }
    }
}
