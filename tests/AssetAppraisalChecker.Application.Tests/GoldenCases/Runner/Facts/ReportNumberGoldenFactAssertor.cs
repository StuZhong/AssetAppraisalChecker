using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public sealed class ReportNumberGoldenFactAssertor
        : V2GoldenFactAssertorBase<ReportNumberFact>
    {
        private readonly ReportNumberFactExtractor extractor =
            new ReportNumberFactExtractor();

        public override FactType FactType => FactType.ReportNumber;

        protected override IReadOnlyList<ReportNumberFact> ExtractFacts(
            DocumentReadResult document,
            string sourceDocument)
        {
            return extractor.Extract(document, sourceDocument);
        }

        protected override IDictionary<string, string> CreateFields(
            ReportNumberFact fact)
        {
            return new Dictionary<string, string>
            {
                { "Value", fact.Value ?? string.Empty },
                { "NormalizedNumber", fact.NormalizedNumber }
            };
        }
    }
}
