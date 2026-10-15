using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    internal static class V2GoldenCaseExtendedSchemaValidator
    {
        public static void Validate(V2GoldenCaseExpected expected)
        {
            ValidateFacts(expected.ExpectedFacts);
            ValidateEvidenceAssertions(expected);
            ValidateSemantic(expected);
        }

        private static void ValidateEvidenceAssertions(V2GoldenCaseExpected expected)
        {
            if (expected.ExpectedExecutions != null)
            {
                foreach (V2GoldenCaseExecutionAssertion execution in expected.ExpectedExecutions)
                {
                    ValidateEvidenceTokens(
                        execution.RequiredEvidence,
                        execution.ForbiddenEvidence,
                        "execution evidence");
                    ValidateEvidenceTokens(
                        execution.RequiredExecutionEvidence,
                        execution.ForbiddenExecutionEvidence,
                        "execution-level evidence");
                    if (execution.Results != null)
                    {
                        foreach (V2GoldenCaseConsistencyResultAssertion result
                            in execution.Results)
                        {
                            ValidateEvidenceTokens(
                                result.RequiredEvidence,
                                result.ForbiddenEvidence,
                                "execution result evidence");
                        }
                    }
                }
            }

            if (expected.ExpectedAnalysis != null
                && expected.ExpectedAnalysis.Results != null)
            {
                foreach (V2GoldenCaseConsistencyResultAssertion result
                    in expected.ExpectedAnalysis.Results)
                {
                    ValidateEvidenceTokens(
                        result.RequiredEvidence,
                        result.ForbiddenEvidence,
                        "result evidence");
                }
            }
        }

        private static void ValidateEvidenceTokens(
            string[] required,
            string[] forbidden,
            string description)
        {
            if (required == null
                || forbidden == null
                || required.Any(string.IsNullOrWhiteSpace)
                || forbidden.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    description + " arrays must contain only non-empty values.");
            }

            if (required.Distinct(StringComparer.Ordinal).Count() != required.Length
                || forbidden.Distinct(StringComparer.Ordinal).Count() != forbidden.Length)
            {
                throw new InvalidDataException(
                    description + " arrays cannot contain duplicate values.");
            }

            if (required.Intersect(forbidden, StringComparer.Ordinal).Any())
            {
                throw new InvalidDataException(
                    description + " cannot require and forbid the same value.");
            }
        }

        private static void ValidateFacts(
            V2GoldenCaseFactGroupAssertion[] groups)
        {
            if (groups == null || groups.Length == 0)
            {
                throw new InvalidDataException("ExpectedFacts cannot be empty.");
            }

            if (groups.GroupBy(
                    group => (group.DocumentId ?? string.Empty) + "\n"
                        + (group.FactType ?? string.Empty),
                    StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidDataException(
                    "ExpectedFacts DocumentId/FactType pairs must be unique.");
            }

            foreach (V2GoldenCaseFactGroupAssertion group in groups)
            {
                RequireText(group.DocumentId, "ExpectedFacts DocumentId");
                RequireEnum<FactType>(group.FactType, "ExpectedFacts FactType");
                if (group.ExpectedCount < 0)
                {
                    throw new InvalidDataException(
                        "ExpectedFacts ExpectedCount cannot be negative.");
                }

                if (group.Facts == null || group.Facts.Length != group.ExpectedCount)
                {
                    throw new InvalidDataException(
                        "ExpectedFacts Facts length must equal ExpectedCount.");
                }

                foreach (V2GoldenCaseFactAssertion fact in group.Facts)
                {
                    RequireText(fact.SourceDocument, "Fact SourceDocument");
                    RequireText(fact.OriginalText, "Fact OriginalText");
                    ParseUnitInterval(fact.ConfidenceMinimum, "Fact ConfidenceMinimum");
                    if (!string.IsNullOrWhiteSpace(fact.DocumentPart))
                    {
                        RequireEnum<DocumentPart>(fact.DocumentPart, "Fact DocumentPart");
                    }

                    if (fact.ParagraphIndex.HasValue && fact.ParagraphIndex.Value < 0)
                    {
                        throw new InvalidDataException(
                            "Fact ParagraphIndex cannot be negative.");
                    }

                    if (fact.Fields == null
                        || fact.Fields.Any(item => string.IsNullOrWhiteSpace(item.Key)
                            || item.Value == null))
                    {
                        throw new InvalidDataException(
                            "Fact Fields must contain non-empty keys and non-null values.");
                    }
                }
            }
        }

        private static void ValidateSemantic(V2GoldenCaseExpected expected)
        {
            V2GoldenCaseSemanticExpectation semantic = expected.Semantic;
            if (semantic == null)
            {
                throw new InvalidDataException("Semantic is required.");
            }

            if (semantic.ExpectedCallCount < 0
                || semantic.ExpectedCallCount != expected.ExpectedSemanticCallCount)
            {
                throw new InvalidDataException(
                    "Semantic ExpectedCallCount must equal ExpectedSemanticCallCount.");
            }

            if (semantic.ExpectedCalls == null
                || semantic.ExpectedCalls.Length != semantic.ExpectedCallCount)
            {
                throw new InvalidDataException(
                    "Semantic ExpectedCalls length must equal ExpectedCallCount.");
            }

            if (semantic.Responses == null)
            {
                throw new InvalidDataException("Semantic Responses cannot be null.");
            }

            int[] orders = semantic.ExpectedCalls.Select(item => item.Order).ToArray();
            if (orders.Any(order => order <= 0)
                || orders.Distinct().Count() != orders.Length)
            {
                throw new InvalidDataException(
                    "Semantic ExpectedCalls Order values must be positive and unique.");
            }

            foreach (V2GoldenCaseSemanticCallAssertion call in semantic.ExpectedCalls)
            {
                RequireText(call.FieldName, "Semantic call FieldName");
                RequireText(call.OriginalValue, "Semantic call OriginalValue");
                RequireText(call.CandidateValue, "Semantic call CandidateValue");
            }

            if (string.Equals(semantic.Mode, "Forbidden", StringComparison.Ordinal))
            {
                if (semantic.ExpectedCallCount != 0 || semantic.Responses.Length != 0)
                {
                    throw new InvalidDataException(
                        "Forbidden Semantic profiles cannot define calls or responses.");
                }

                return;
            }

            if (string.Equals(semantic.Mode, "ControlledFailure", StringComparison.Ordinal))
            {
                if (semantic.ExpectedCallCount == 0
                    || string.IsNullOrWhiteSpace(semantic.FailureMessage))
                {
                    throw new InvalidDataException(
                        "ControlledFailure Semantic profiles require calls and a failure message.");
                }

                return;
            }

            if (!string.Equals(semantic.Mode, "DeterministicMock", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Unsupported Semantic Mode '" + semantic.Mode + "'.");
            }

            if (semantic.ExpectedCallCount == 0 || semantic.Responses.Length == 0)
            {
                throw new InvalidDataException(
                    "DeterministicMock Semantic profiles require calls and responses.");
            }

            if (semantic.Responses.GroupBy(item => item.FieldName, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidDataException(
                    "Semantic response FieldName values must be unique.");
            }

            foreach (V2GoldenCaseSemanticResponseDefinition response in semantic.Responses)
            {
                RequireText(response.FieldName, "Semantic response FieldName");
                ParseUnitInterval(response.SimilarityScore, "Semantic SimilarityScore");
                ParseUnitInterval(response.Confidence, "Semantic Confidence");
                if (response.Evidence == null
                    || response.Evidence.Length == 0
                    || response.Evidence.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidDataException(
                        "Semantic response Evidence must contain non-empty values.");
                }
            }
        }

        private static decimal ParseUnitInterval(string value, string name)
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
                throw new InvalidDataException(name + " must be between 0 and 1.");
            }

            return parsed;
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(name + " is required.");
            }
        }

        private static void RequireEnum<T>(string value, string name)
            where T : struct
        {
            T parsed;
            if (string.IsNullOrWhiteSpace(value)
                || !Enum.TryParse(value, false, out parsed)
                || !Enum.IsDefined(typeof(T), parsed))
            {
                throw new InvalidDataException(
                    name + " contains an unsupported value '" + value + "'.");
            }
        }
    }
}
