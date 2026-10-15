using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AssetAppraisalChecker.Application;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    public sealed class V2GoldenCaseExecutionResult
    {
        public V2GoldenCaseExecutionResult(
            string caseId,
            V2AnalysisResult analysisResult,
            int semanticCallCount,
            IDictionary<string, string> hashesBefore,
            IDictionary<string, string> hashesAfter,
            IEnumerable<V2GoldenCaseAssertionFailure> failures)
            : this(
                caseId,
                analysisResult,
                semanticCallCount,
                Array.Empty<V2GoldenCaseSemanticCallRecord>(),
                Array.Empty<V2GoldenCaseFactSnapshot>(),
                new Dictionary<string, bool>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal),
                hashesBefore,
                hashesAfter,
                failures)
        {
        }

        public V2GoldenCaseExecutionResult(
            string caseId,
            V2AnalysisResult analysisResult,
            int semanticCallCount,
            IEnumerable<V2GoldenCaseSemanticCallRecord> semanticCalls,
            IEnumerable<V2GoldenCaseFactSnapshot> facts,
            IDictionary<string, bool> openXmlPreflight,
            IDictionary<string, string> hashesBefore,
            IDictionary<string, string> hashesAfter,
            IEnumerable<V2GoldenCaseAssertionFailure> failures)
            : this(
                caseId,
                analysisResult,
                semanticCallCount,
                semanticCalls,
                facts,
                openXmlPreflight,
                new Dictionary<string, bool>(StringComparer.Ordinal),
                hashesBefore,
                hashesAfter,
                failures)
        {
        }

        public V2GoldenCaseExecutionResult(
            string caseId,
            V2AnalysisResult analysisResult,
            int semanticCallCount,
            IEnumerable<V2GoldenCaseSemanticCallRecord> semanticCalls,
            IEnumerable<V2GoldenCaseFactSnapshot> facts,
            IDictionary<string, bool> openXmlPreflight,
            IDictionary<string, bool> privacyPreflight,
            IDictionary<string, string> hashesBefore,
            IDictionary<string, string> hashesAfter,
            IEnumerable<V2GoldenCaseAssertionFailure> failures)
        {
            CaseId = caseId ?? string.Empty;
            AnalysisResult = analysisResult;
            SemanticCallCount = semanticCallCount;
            SemanticCalls = new ReadOnlyCollection<V2GoldenCaseSemanticCallRecord>(
                new List<V2GoldenCaseSemanticCallRecord>(
                    semanticCalls ?? Array.Empty<V2GoldenCaseSemanticCallRecord>()));
            Facts = new ReadOnlyCollection<V2GoldenCaseFactSnapshot>(
                new List<V2GoldenCaseFactSnapshot>(
                    facts ?? Array.Empty<V2GoldenCaseFactSnapshot>()));
            OpenXmlPreflight = new ReadOnlyDictionary<string, bool>(
                new Dictionary<string, bool>(
                    openXmlPreflight ?? new Dictionary<string, bool>(),
                    StringComparer.Ordinal));
            PrivacyPreflight = new ReadOnlyDictionary<string, bool>(
                new Dictionary<string, bool>(
                    privacyPreflight ?? new Dictionary<string, bool>(),
                    StringComparer.Ordinal));
            HashesBefore = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(hashesBefore ?? new Dictionary<string, string>(), StringComparer.Ordinal));
            HashesAfter = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(hashesAfter ?? new Dictionary<string, string>(), StringComparer.Ordinal));
            Failures = new ReadOnlyCollection<V2GoldenCaseAssertionFailure>(
                new List<V2GoldenCaseAssertionFailure>(failures ?? Array.Empty<V2GoldenCaseAssertionFailure>()));
        }

        public string CaseId { get; }

        public V2AnalysisResult AnalysisResult { get; }

        public int SemanticCallCount { get; }

        public IReadOnlyList<V2GoldenCaseSemanticCallRecord> SemanticCalls { get; }

        public IReadOnlyList<V2GoldenCaseFactSnapshot> Facts { get; }

        public IReadOnlyDictionary<string, bool> OpenXmlPreflight { get; }

        public IReadOnlyDictionary<string, bool> PrivacyPreflight { get; }

        public IReadOnlyDictionary<string, string> HashesBefore { get; }

        public IReadOnlyDictionary<string, string> HashesAfter { get; }

        public IReadOnlyList<V2GoldenCaseAssertionFailure> Failures { get; }

        public bool IsSuccess => Failures.Count == 0;
    }

    public sealed class V2GoldenCaseAssertionFailure
    {
        public V2GoldenCaseAssertionFailure(string stage, string message)
        {
            if (string.IsNullOrWhiteSpace(stage))
            {
                throw new ArgumentException("Assertion failure stage is required.", nameof(stage));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Assertion failure message is required.", nameof(message));
            }

            Stage = stage;
            Message = message;
        }

        public string Stage { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Stage + ": " + Message;
        }
    }
}
