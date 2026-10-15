using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public sealed class V2GoldenLegacyExpectedAdapter
    {
        public const string SchemaVersion = "legacy-v2-golden-case/1.0";

        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public IReadOnlyList<V2GoldenCaseAssertionFailure> AssertShadow(
            string path,
            V2GoldenCaseExecutionResult actual)
        {
            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            Dictionary<string, object> root = Load(path);
            var failures = new List<V2GoldenCaseAssertionFailure>();
            AddMismatch(failures, "Legacy", Text(root, "CaseId"), actual.CaseId, "CaseId");

            Dictionary<string, object> expectedResult = Object(root, "ExpectedResult");
            string factTypeText = Text(expectedResult, "FactType");
            FactType factType;
            if (!Enum.TryParse(factTypeText, false, out factType))
            {
                throw new InvalidDataException(
                    "Legacy ExpectedResult contains unsupported FactType '" + factTypeText + "'.");
            }

            AddMismatch(
                failures,
                "Legacy.Result",
                Text(expectedResult, "StatusSummary"),
                actual.AnalysisResult.StatusSummary.ToString(),
                "StatusSummary");
            AddMismatch(
                failures,
                "Legacy.Result",
                Integer(expectedResult, "ResultCount").ToString(CultureInfo.InvariantCulture),
                actual.AnalysisResult.Results.Count.ToString(CultureInfo.InvariantCulture),
                "ResultCount");

            ConsistencyResult result = actual.AnalysisResult.Results.SingleOrDefault(
                item => item.FactType == factType);
            if (result == null)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    "Legacy.Result",
                    "No actual result exists for FactType '" + factType + "'."));
            }
            else
            {
                AddMismatch(
                    failures,
                    "Legacy.Result",
                    Text(expectedResult, "ResultStatus"),
                    result.Status.ToString(),
                    "ResultStatus");
                string reasonContains = Text(expectedResult, "ReasonContains");
                if (result.Reason.IndexOf(reasonContains, StringComparison.Ordinal) < 0)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "Legacy.Result",
                        "Reason does not contain '" + reasonContains + "'."));
                }

                foreach (string evidence in Strings(expectedResult, "RequiredEvidence"))
                {
                    if (!result.Evidence.Any(item => item.IndexOf(
                            evidence,
                            StringComparison.Ordinal) >= 0))
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            "Legacy.Result",
                            "Evidence does not contain '" + evidence + "'."));
                    }
                }
            }

            AssertExecution(root, factType, expectedResult, actual, failures);
            AssertFacts(root, factType, actual, failures);
            AssertSemantic(root, actual, failures);
            return failures.AsReadOnly();
        }

        private static void AssertExecution(
            IDictionary<string, object> root,
            FactType factType,
            IDictionary<string, object> expectedResult,
            V2GoldenCaseExecutionResult actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            Dictionary<string, object> explicitExecution = OptionalObject(
                root,
                "ExpectedExecution");
            Dictionary<string, object> semantic = Object(
                root,
                "SemanticExpectedBehavior");
            bool isLegacySemanticFailure = explicitExecution == null
                && string.Equals(
                    Text(semantic, "Mode"),
                    "Failure",
                    StringComparison.Ordinal);
            string checkerName = explicitExecution == null
                ? factType + "Checker"
                : Text(explicitExecution, "CheckerName");
            string executionStatus = explicitExecution == null
                ? (isLegacySemanticFailure
                    ? "Failed"
                    : (string.Equals(
                            Text(expectedResult, "ResultStatus"),
                            ConsistencyStatus.WARNING.ToString(),
                            StringComparison.Ordinal)
                        ? "CompletedWithWarning"
                        : "Succeeded"))
                : Text(explicitExecution, "ExecutionStatus");
            string errorInformation = explicitExecution == null
                ? (isLegacySemanticFailure
                    ? "字段 " + Strings(semantic, "ExpectedFields").Single()
                        + " 的 Semantic 分析失败。"
                    : string.Empty)
                : Text(explicitExecution, "ErrorInformation");

            var envelope = actual.AnalysisResult.Executions.SingleOrDefault(
                item => item.FactType == factType);
            if (envelope == null)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    "Legacy.Execution",
                    "No execution exists for FactType '" + factType + "'."));
                return;
            }

            AddMismatch(failures, "Legacy.Execution", checkerName, envelope.CheckerName, "CheckerName");
            AddMismatch(
                failures,
                "Legacy.Execution",
                executionStatus,
                envelope.ExecutionStatus.ToString(),
                "ExecutionStatus");
            AddMismatch(
                failures,
                "Legacy.Execution",
                errorInformation,
                envelope.ErrorInformation,
                "ErrorInformation");
        }

        private static void AssertFacts(
            IDictionary<string, object> root,
            FactType factType,
            V2GoldenCaseExecutionResult actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            Dictionary<string, object> expectedFacts = Object(root, "ExpectedFacts");
            foreach (KeyValuePair<string, object> documentFact in expectedFacts)
            {
                string documentId = documentFact.Key.ToLowerInvariant();
                V2GoldenCaseFactSnapshot[] snapshots = actual.Facts
                    .Where(item => item.DocumentId == documentId && item.FactType == factType)
                    .ToArray();
                if (documentFact.Value == null)
                {
                    if (snapshots.Length != 0)
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            "Legacy.Fact[" + documentId + "]",
                            "Expected no Fact but extracted " + snapshots.Length + "."));
                    }

                    continue;
                }

                Dictionary<string, object> expectedFact = AsObject(documentFact.Value, "ExpectedFact");
                if (snapshots.Length != 1)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        "Legacy.Fact[" + documentId + "]",
                        "Expected one Fact but extracted " + snapshots.Length + "."));
                    continue;
                }

                V2GoldenCaseFactSnapshot snapshot = snapshots[0];
                string stage = "Legacy.Fact[" + documentId + "]";
                AddMismatch(failures, stage, Text(expectedFact, "FactType"), snapshot.FactType.ToString(), "FactType");
                AddMismatch(failures, stage, Text(expectedFact, "SourceDocument"), snapshot.SourceDocument, "SourceDocument");
                AddMismatch(failures, stage, Text(expectedFact, "OriginalText"), snapshot.OriginalText, "OriginalText");
                decimal minimum = decimal.Parse(
                    Text(expectedFact, "ConfidenceMinimum"),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture);
                if (snapshot.Confidence < minimum)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "Confidence is below " + minimum.ToString(CultureInfo.InvariantCulture) + "."));
                }

                foreach (KeyValuePair<string, object> field in expectedFact)
                {
                    if (field.Key == "FactType"
                        || field.Key == "SourceDocument"
                        || field.Key == "OriginalText"
                        || field.Key == "ConfidenceMinimum")
                    {
                        continue;
                    }

                    string actualValue;
                    if (!snapshot.Fields.TryGetValue(field.Key, out actualValue))
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            stage,
                            "Fact adapter does not expose field '" + field.Key + "'."));
                    }
                    else
                    {
                        AddMismatch(
                            failures,
                            stage,
                            Convert.ToString(field.Value, CultureInfo.InvariantCulture),
                            actualValue,
                            field.Key);
                    }
                }
            }
        }

        private static void AssertSemantic(
            IDictionary<string, object> root,
            V2GoldenCaseExecutionResult actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            Dictionary<string, object> semantic = Object(root, "SemanticExpectedBehavior");
            int expectedCallCount = Integer(semantic, "ExpectedCallCount");
            AddMismatch(
                failures,
                "Legacy.Semantic",
                expectedCallCount.ToString(CultureInfo.InvariantCulture),
                actual.SemanticCallCount.ToString(CultureInfo.InvariantCulture),
                "CallCount");
            CollectionAssertEquivalent(
                Strings(semantic, "ExpectedFields"),
                actual.SemanticCalls.Select(item => item.FieldName),
                failures);
        }

        private Dictionary<string, object> Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("Legacy Expected.json was not found.", path);
            }

            object value = serializer.DeserializeObject(File.ReadAllText(path));
            return AsObject(value, "Legacy Expected");
        }

        private static Dictionary<string, object> Object(
            IDictionary<string, object> parent,
            string name)
        {
            object value;
            if (!parent.TryGetValue(name, out value) || value == null)
            {
                throw new InvalidDataException(name + " is required in Legacy Expected.json.");
            }

            return AsObject(value, name);
        }

        private static Dictionary<string, object> OptionalObject(
            IDictionary<string, object> parent,
            string name)
        {
            object value;
            return parent.TryGetValue(name, out value) && value != null
                ? AsObject(value, name)
                : null;
        }

        private static Dictionary<string, object> AsObject(object value, string name)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary == null)
            {
                throw new InvalidDataException(name + " must be a JSON object.");
            }

            return dictionary;
        }

        private static string Text(IDictionary<string, object> parent, string name)
        {
            object value;
            if (!parent.TryGetValue(name, out value) || value == null)
            {
                throw new InvalidDataException(name + " is required in Legacy Expected.json.");
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int Integer(IDictionary<string, object> parent, string name)
        {
            return Convert.ToInt32(parent[name], CultureInfo.InvariantCulture);
        }

        private static string[] Strings(IDictionary<string, object> parent, string name)
        {
            object value;
            if (!parent.TryGetValue(name, out value) || value == null)
            {
                return Array.Empty<string>();
            }

            var items = value as IEnumerable;
            if (items == null || value is string)
            {
                throw new InvalidDataException(name + " must be a JSON array.");
            }

            return items.Cast<object>()
                .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static void CollectionAssertEquivalent(
            IEnumerable<string> expected,
            IEnumerable<string> actual,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            string[] expectedItems = expected.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] actualItems = actual.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!expectedItems.SequenceEqual(actualItems, StringComparer.Ordinal))
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    "Legacy.Semantic",
                    "Expected fields '" + string.Join(",", expectedItems)
                    + "' but observed '" + string.Join(",", actualItems) + "'."));
            }
        }

        private static void AddMismatch(
            ICollection<V2GoldenCaseAssertionFailure> failures,
            string stage,
            string expected,
            string actual,
            string field)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    field + " expected '" + expected + "' but was '" + actual + "'."));
            }
        }
    }
}
