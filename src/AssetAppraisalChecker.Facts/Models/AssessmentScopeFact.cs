using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public sealed class AssessmentScopeFact : BusinessFact
    {
        public AssessmentScopeFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence)
            : base(FactType.AssessmentScope, sourceDocument, sourceLocation, originalText, confidence)
        {
        }

        public AssessmentScopeFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string value)
            : base(
                FactType.AssessmentScope,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                value)
        {
        }
    }
}
