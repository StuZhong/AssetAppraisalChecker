using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    public sealed class AssessmentValueFactExtractor
    {
        private const string ExplicitAssessmentValueLabel = "评估价值";

        private readonly MoneyOccurrenceExtractor moneyOccurrenceExtractor;

        public AssessmentValueFactExtractor()
            : this(new MoneyOccurrenceExtractor())
        {
        }

        internal AssessmentValueFactExtractor(
            MoneyOccurrenceExtractor moneyOccurrenceExtractor)
        {
            this.moneyOccurrenceExtractor = moneyOccurrenceExtractor ??
                throw new ArgumentNullException(nameof(moneyOccurrenceExtractor));
        }

        public IReadOnlyList<AssessmentValueFact> Extract(
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

            List<AssessmentValueFact> facts = moneyOccurrenceExtractor
                .Extract(documentReadResult)
                .Where(IsSupportedAssessmentValue)
                .Select(occurrence => new AssessmentValueFact(
                    sourceDocument,
                    occurrence.Location,
                    occurrence.RawText,
                    MapConfidence(occurrence),
                    occurrence.NormalizedAmountYuan))
                .ToList();

            return new ReadOnlyCollection<AssessmentValueFact>(facts);
        }

        private static bool IsSupportedAssessmentValue(MoneyOccurrence occurrence)
        {
            return occurrence.FieldIdentity.ValueCategory == ValueCategory.AppraisedValue &&
                   occurrence.ValueCategoryConfidence == ClassificationConfidence.High &&
                   occurrence.ClassificationConfidence == ClassificationConfidence.High &&
                   occurrence.FieldIdentity.TaxBasis == TaxBasis.Unspecified &&
                   IsSupportedScope(occurrence.FieldIdentity.Scope) &&
                   HasExplicitAssessmentValueLabel(occurrence) &&
                   IsSuccessfulParse(occurrence.MoneyParseStatus) &&
                   !MethodCalculationContext.IsMethodSpecificResult(occurrence);
        }

        private static bool IsSupportedScope(Scope scope)
        {
            return scope == Scope.Unspecified ||
                   scope == Scope.Subtotal ||
                   scope == Scope.ProjectTotal;
        }

        private static bool HasExplicitAssessmentValueLabel(MoneyOccurrence occurrence)
        {
            return occurrence.NearbyContext.CurrentText.IndexOf(
                ExplicitAssessmentValueLabel,
                StringComparison.Ordinal) >= 0;
        }

        private static bool IsSuccessfulParse(MoneyParseStatus status)
        {
            return status == MoneyParseStatus.Success ||
                   status == MoneyParseStatus.SuccessWithWarning;
        }

        private static FactConfidence MapConfidence(MoneyOccurrence occurrence)
        {
            if (occurrence.MoneyParseStatus == MoneyParseStatus.SuccessWithWarning)
            {
                return new FactConfidence(FactConfidenceBand.Medium, 0.75m);
            }

            return new FactConfidence(FactConfidenceBand.High, 0.95m);
        }
    }
}
