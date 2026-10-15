using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public abstract class V2GoldenFactAssertorBase<TFact> : IV2GoldenFactAssertor
        where TFact : BusinessFact
    {
        public abstract FactType FactType { get; }

        public IReadOnlyList<V2GoldenCaseFactSnapshot> Extract(
            DocumentReadResult document,
            string sourceDocument)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("Source document is required.", nameof(sourceDocument));
            }

            return ExtractFacts(document, sourceDocument)
                .Select(fact => new V2GoldenCaseFactSnapshot(
                    sourceDocument,
                    fact.FactType,
                    fact.SourceDocument,
                    fact.OriginalText,
                    fact.Confidence.Score,
                    fact.SourceLocation.DocumentPart.ToString(),
                    fact.SourceLocation.ParagraphIndex,
                    CreateFields(fact)))
                .ToArray();
        }

        protected abstract IReadOnlyList<TFact> ExtractFacts(
            DocumentReadResult document,
            string sourceDocument);

        protected abstract IDictionary<string, string> CreateFields(TFact fact);
    }
}
