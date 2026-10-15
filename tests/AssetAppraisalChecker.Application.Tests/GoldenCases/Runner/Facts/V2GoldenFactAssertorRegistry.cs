using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts
{
    public sealed class V2GoldenFactAssertorRegistry
    {
        private readonly IReadOnlyDictionary<FactType, IV2GoldenFactAssertor> assertors;

        public V2GoldenFactAssertorRegistry()
            : this(new IV2GoldenFactAssertor[]
            {
                new AssessmentObjectGoldenFactAssertor(),
                new AssessmentValueGoldenFactAssertor(),
                new BenchmarkDateGoldenFactAssertor(),
                new ReportNumberGoldenFactAssertor(),
                new EvaluationMethodGoldenFactAssertor()
            })
        {
        }

        public V2GoldenFactAssertorRegistry(IEnumerable<IV2GoldenFactAssertor> assertors)
        {
            if (assertors == null)
            {
                throw new ArgumentNullException(nameof(assertors));
            }

            var items = assertors.ToArray();
            if (items.Any(item => item == null))
            {
                throw new ArgumentException("Fact assertors cannot contain null.", nameof(assertors));
            }

            if (items.GroupBy(item => item.FactType).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("Fact assertor FactType values must be unique.", nameof(assertors));
            }

            this.assertors = new ReadOnlyDictionary<FactType, IV2GoldenFactAssertor>(
                items.ToDictionary(item => item.FactType));
        }

        public V2GoldenFactAssertionReport Assert(
            IEnumerable<V2GoldenCaseFactGroupAssertion> expectedGroups,
            IReadOnlyDictionary<string, DocumentReadResult> documents)
        {
            if (expectedGroups == null)
            {
                throw new ArgumentNullException(nameof(expectedGroups));
            }

            if (documents == null)
            {
                throw new ArgumentNullException(nameof(documents));
            }

            var snapshots = new List<V2GoldenCaseFactSnapshot>();
            var failures = new List<V2GoldenCaseAssertionFailure>();

            foreach (V2GoldenCaseFactGroupAssertion expectedGroup in expectedGroups)
            {
                FactType factType;
                if (!Enum.TryParse(expectedGroup.FactType, false, out factType))
                {
                    throw new InvalidOperationException(
                        "Unsupported ExpectedFacts FactType '" + expectedGroup.FactType + "'.");
                }

                IV2GoldenFactAssertor assertor;
                if (!assertors.TryGetValue(factType, out assertor))
                {
                    throw new InvalidOperationException(
                        "No Fact assertion adapter is registered for " + factType + ".");
                }

                DocumentReadResult document;
                if (!documents.TryGetValue(expectedGroup.DocumentId, out document))
                {
                    throw new InvalidOperationException(
                        "ExpectedFacts references unknown DocumentId '"
                        + expectedGroup.DocumentId + "'.");
                }

                IReadOnlyList<V2GoldenCaseFactSnapshot> actual = assertor.Extract(
                    document,
                    expectedGroup.DocumentId);
                snapshots.AddRange(actual);

                bool countMatches = expectedGroup.AllowAdditionalFacts
                    ? actual.Count >= expectedGroup.ExpectedCount
                    : actual.Count == expectedGroup.ExpectedCount;
                if (!countMatches)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "Fact[" + expectedGroup.DocumentId + "/" + factType + "]",
                        "Expected count " + expectedGroup.ExpectedCount
                        + " but extracted " + actual.Count + "."));
                }

                int comparisonCount = Math.Min(expectedGroup.Facts.Length, actual.Count);
                for (int index = 0; index < comparisonCount; index++)
                {
                    AssertFact(
                        expectedGroup.Facts[index],
                        actual[index],
                        failures,
                        "Fact[" + expectedGroup.DocumentId + "/" + factType
                        + "/" + (index + 1) + "]");
                }
            }

            return new V2GoldenFactAssertionReport(snapshots, failures);
        }

        private static void AssertFact(
            V2GoldenCaseFactAssertion expected,
            V2GoldenCaseFactSnapshot actual,
            ICollection<V2GoldenCaseAssertionFailure> failures,
            string stage)
        {
            AddMismatch(failures, stage, "SourceDocument", expected.SourceDocument, actual.SourceDocument);
            AddMismatch(failures, stage, "OriginalText", expected.OriginalText, actual.OriginalText);

            decimal minimum = decimal.Parse(
                expected.ConfidenceMinimum,
                NumberStyles.Number,
                CultureInfo.InvariantCulture);
            if (actual.Confidence < minimum)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    "Confidence minimum " + minimum.ToString(CultureInfo.InvariantCulture)
                    + " but was " + actual.Confidence.ToString(CultureInfo.InvariantCulture) + "."));
            }

            if (!string.IsNullOrWhiteSpace(expected.DocumentPart))
            {
                AddMismatch(
                    failures,
                    stage,
                    "DocumentPart",
                    expected.DocumentPart,
                    actual.DocumentPart);
            }

            if (expected.ParagraphIndex.HasValue
                && expected.ParagraphIndex != actual.ParagraphIndex)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    "ParagraphIndex expected '" + expected.ParagraphIndex
                    + "' but was '" + actual.ParagraphIndex + "'."));
            }

            foreach (KeyValuePair<string, string> expectedField in expected.Fields)
            {
                string actualValue;
                if (!actual.Fields.TryGetValue(expectedField.Key, out actualValue))
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Actual Fact does not expose field '" + expectedField.Key + "'."));
                    continue;
                }

                AddMismatch(
                    failures,
                    stage,
                    "Fields." + expectedField.Key,
                    expectedField.Value,
                    actualValue);
            }
        }

        private static void AddMismatch(
            ICollection<V2GoldenCaseAssertionFailure> failures,
            string stage,
            string field,
            string expected,
            string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    field + " expected '" + expected + "' but was '" + actual + "'."));
            }
        }
    }

    public sealed class V2GoldenFactAssertionReport
    {
        public V2GoldenFactAssertionReport(
            IEnumerable<V2GoldenCaseFactSnapshot> snapshots,
            IEnumerable<V2GoldenCaseAssertionFailure> failures)
        {
            Snapshots = new ReadOnlyCollection<V2GoldenCaseFactSnapshot>(
                new List<V2GoldenCaseFactSnapshot>(snapshots));
            Failures = new ReadOnlyCollection<V2GoldenCaseAssertionFailure>(
                new List<V2GoldenCaseAssertionFailure>(failures));
        }

        public IReadOnlyList<V2GoldenCaseFactSnapshot> Snapshots { get; }

        public IReadOnlyList<V2GoldenCaseAssertionFailure> Failures { get; }
    }
}
