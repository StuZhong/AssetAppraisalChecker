using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public sealed class EvaluationMethodGoldenFactAssertor
        : V2GoldenFactAssertorBase<EvaluationMethodFact>
    {
        private readonly EvaluationMethodFactExtractor extractor =
            new EvaluationMethodFactExtractor();

        public override FactType FactType => FactType.EvaluationMethod;

        protected override IReadOnlyList<EvaluationMethodFact> ExtractFacts(
            DocumentReadResult document,
            string sourceDocument)
        {
            return extractor.Extract(document, sourceDocument);
        }

        protected override IDictionary<string, string> CreateFields(
            EvaluationMethodFact fact)
        {
            return new Dictionary<string, string>
            {
                { "Method", fact.Method ?? string.Empty },
                { "MethodCode", fact.MethodCode.ToString() },
                { "MethodFamily", fact.MethodFamily.ToString() },
                { "MethodSpecificity", fact.MethodSpecificity.ToString() },
                { "Subject", fact.Subject ?? string.Empty },
                { "SubjectKind", fact.SubjectKind.ToString() },
                { "NormalizedSubjectKey", fact.NormalizedSubjectKey ?? string.Empty },
                { "MethodRole", fact.MethodRole.ToString() },
                { "MethodSetId", fact.MethodSetId ?? string.Empty },
                { "SetCompleteness", fact.SetCompleteness.ToString() },
                { "Applicability", "Adopted" }
            };
        }
    }
}
