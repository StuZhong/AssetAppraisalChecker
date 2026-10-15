using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    public sealed class V2GoldenCaseSemanticExpectation
    {
        public V2GoldenCaseSemanticExpectation()
        {
            Mode = string.Empty;
            ExpectedCalls = Array.Empty<V2GoldenCaseSemanticCallAssertion>();
            Responses = Array.Empty<V2GoldenCaseSemanticResponseDefinition>();
            FailureMessage = string.Empty;
        }

        public string Mode { get; set; }

        public int ExpectedCallCount { get; set; }

        public V2GoldenCaseSemanticCallAssertion[] ExpectedCalls { get; set; }

        public V2GoldenCaseSemanticResponseDefinition[] Responses { get; set; }

        public string FailureMessage { get; set; }
    }

    public sealed class V2GoldenCaseSemanticCallAssertion
    {
        public V2GoldenCaseSemanticCallAssertion()
        {
            FieldName = string.Empty;
            OriginalValue = string.Empty;
            CandidateValue = string.Empty;
        }

        public int Order { get; set; }

        public string FieldName { get; set; }

        public string OriginalValue { get; set; }

        public string CandidateValue { get; set; }
    }

    public sealed class V2GoldenCaseSemanticResponseDefinition
    {
        public V2GoldenCaseSemanticResponseDefinition()
        {
            FieldName = string.Empty;
            SimilarityScore = string.Empty;
            Confidence = string.Empty;
            Evidence = Array.Empty<string>();
        }

        public string FieldName { get; set; }

        public string SimilarityScore { get; set; }

        public string Confidence { get; set; }

        public string[] Evidence { get; set; }
    }

    public sealed class V2GoldenCaseSemanticCallRecord
    {
        public V2GoldenCaseSemanticCallRecord(
            int order,
            string fieldName,
            string originalValue,
            string candidateValue)
        {
            Order = order;
            FieldName = fieldName ?? string.Empty;
            OriginalValue = originalValue ?? string.Empty;
            CandidateValue = candidateValue ?? string.Empty;
        }

        public int Order { get; }

        public string FieldName { get; }

        public string OriginalValue { get; }

        public string CandidateValue { get; }
    }
}
