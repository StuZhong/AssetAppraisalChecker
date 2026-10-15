using System.Collections.Generic;
using System.Globalization;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public sealed class AssessmentObjectGoldenFactAssertor
        : V2GoldenFactAssertorBase<AssessmentObjectFact>
    {
        private readonly AssessmentObjectFactExtractor extractor =
            new AssessmentObjectFactExtractor();

        public override FactType FactType => FactType.AssessmentObject;

        protected override IReadOnlyList<AssessmentObjectFact> ExtractFacts(
            DocumentReadResult document,
            string sourceDocument)
        {
            return extractor.Extract(document, sourceDocument);
        }

        protected override IDictionary<string, string> CreateFields(
            AssessmentObjectFact fact)
        {
            return new Dictionary<string, string>
            {
                { "AssetType", fact.AssetType },
                { "Owner", fact.Owner },
                { "EconomicAction", fact.EconomicAction },
                {
                    "Quantity",
                    fact.Quantity.HasValue
                        ? fact.Quantity.Value.ToString(CultureInfo.InvariantCulture)
                        : string.Empty
                },
                { "Scope", fact.Scope }
            };
        }
    }
}
