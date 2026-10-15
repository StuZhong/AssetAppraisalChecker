using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public static class V2GoldenSemanticAnalyzerFactory
    {
        public static V2GoldenSemanticAnalyzerSession Create(
            V2GoldenCaseSemanticExpectation expectation)
        {
            return new V2GoldenSemanticAnalyzerSession(
                expectation ?? throw new ArgumentNullException(nameof(expectation)));
        }
    }

    public sealed class V2GoldenSemanticAnalyzerSession : ISemanticAnalyzer
    {
        private readonly V2GoldenCaseSemanticExpectation expectation;
        private readonly List<V2GoldenCaseSemanticCallRecord> calls =
            new List<V2GoldenCaseSemanticCallRecord>();

        internal V2GoldenSemanticAnalyzerSession(
            V2GoldenCaseSemanticExpectation expectation)
        {
            this.expectation = expectation;
            Calls = new ReadOnlyCollection<V2GoldenCaseSemanticCallRecord>(calls);
        }

        public int CallCount => calls.Count;

        public IReadOnlyList<V2GoldenCaseSemanticCallRecord> Calls { get; }

        public SemanticAnalysisResult Analyze(string textContent)
        {
            ParsedSemanticInput input = ParsedSemanticInput.Parse(textContent);
            calls.Add(new V2GoldenCaseSemanticCallRecord(
                calls.Count + 1,
                input.FieldName,
                input.OriginalValue,
                input.CandidateValue));

            if (string.Equals(expectation.Mode, "Forbidden", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Semantic analysis is forbidden by this Golden Case profile.");
            }

            if (string.Equals(expectation.Mode, "ControlledFailure", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(expectation.FailureMessage)
                        ? "Configured Golden Case semantic failure."
                        : expectation.FailureMessage);
            }

            if (!string.Equals(expectation.Mode, "DeterministicMock", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unsupported Golden Case Semantic mode '" + expectation.Mode + "'.");
            }

            V2GoldenCaseSemanticResponseDefinition response = expectation.Responses.SingleOrDefault(
                item => string.Equals(item.FieldName, input.FieldName, StringComparison.Ordinal));
            if (response == null)
            {
                throw new InvalidOperationException(
                    "No deterministic Semantic response is configured for field '"
                    + input.FieldName + "'.");
            }

            return new SemanticAnalysisResult(
                input.FieldName,
                input.CandidateValue,
                ParseUnitInterval(response.SimilarityScore, "SimilarityScore"),
                ParseUnitInterval(response.Confidence, "Confidence"),
                response.Evidence);
        }

        private static decimal ParseUnitInterval(string value, string fieldName)
        {
            decimal parsed;
            if (!decimal.TryParse(
                    value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out parsed)
                || parsed < 0m
                || parsed > 1m)
            {
                throw new InvalidOperationException(
                    fieldName + " must be a decimal between 0 and 1.");
            }

            return parsed;
        }

        private sealed class ParsedSemanticInput
        {
            private ParsedSemanticInput(
                string fieldName,
                string originalValue,
                string candidateValue)
            {
                FieldName = fieldName;
                OriginalValue = originalValue;
                CandidateValue = candidateValue;
            }

            public string FieldName { get; }

            public string OriginalValue { get; }

            public string CandidateValue { get; }

            public static ParsedSemanticInput Parse(string textContent)
            {
                if (string.IsNullOrWhiteSpace(textContent))
                {
                    throw new ArgumentException(
                        "Semantic input cannot be empty.",
                        nameof(textContent));
                }

                var values = textContent
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(new[] { '=' }, 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

                return new ParsedSemanticInput(
                    Require(values, "FieldName"),
                    Require(values, "OriginalValue"),
                    Require(values, "CandidateValue"));
            }

            private static string Require(
                IReadOnlyDictionary<string, string> values,
                string key)
            {
                string value;
                if (!values.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        "Semantic input is missing '" + key + "'.");
                }

                return value;
            }
        }
    }
}
