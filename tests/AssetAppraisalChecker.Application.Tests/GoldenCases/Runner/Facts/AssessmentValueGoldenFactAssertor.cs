using System.Collections.Generic;
using System.Globalization;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public sealed class AssessmentValueGoldenFactAssertor
        : V2GoldenFactAssertorBase<AssessmentValueFact>
    {
        private readonly AssessmentValueFactExtractor extractor =
            new AssessmentValueFactExtractor();

        public override FactType FactType => FactType.AssessmentValue;

        protected override IReadOnlyList<AssessmentValueFact> ExtractFacts(
            DocumentReadResult document,
            string sourceDocument)
        {
            return extractor.Extract(document, sourceDocument);
        }

        protected override IDictionary<string, string> CreateFields(
            AssessmentValueFact fact)
        {
            string amount = fact.NormalizedAmountYuan.HasValue
                ? fact.NormalizedAmountYuan.Value.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture)
                : string.Empty;
            return new Dictionary<string, string>
            {
                { "Value", fact.Value ?? string.Empty },
                { "NormalizedAmountYuan", amount }
            };
        }
    }
}
