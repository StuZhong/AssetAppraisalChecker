using System.Collections.Generic;
using System.Globalization;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public sealed class BenchmarkDateGoldenFactAssertor
        : V2GoldenFactAssertorBase<BenchmarkDateFact>
    {
        private readonly BenchmarkDateFactExtractor extractor =
            new BenchmarkDateFactExtractor();

        public override FactType FactType => FactType.BenchmarkDate;

        protected override IReadOnlyList<BenchmarkDateFact> ExtractFacts(
            DocumentReadResult document,
            string sourceDocument)
        {
            return extractor.Extract(document, sourceDocument);
        }

        protected override IDictionary<string, string> CreateFields(
            BenchmarkDateFact fact)
        {
            string date = fact.NormalizedDate.HasValue
                ? fact.NormalizedDate.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture)
                : string.Empty;
            return new Dictionary<string, string>
            {
                { "Value", fact.Value ?? string.Empty },
                { "NormalizedDate", date }
            };
        }
    }
}
