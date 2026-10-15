using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public sealed class V2GoldenCaseJsonLoader
    {
        public const string ManifestSchemaVersion = "v2-golden-manifest/1.0";
        public const string ExpectedSchemaVersionV1 = "v2-golden-case/1.0";
        public const string ExpectedSchemaVersionV2 = "v2-golden-case/2.0";
        public const string ExpectedSchemaVersion = ExpectedSchemaVersionV1;

        private static readonly Regex Sha256Pattern = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public V2GoldenCaseManifest LoadManifest(string path)
        {
            return ParseManifest(File.ReadAllText(RequireFile(path)));
        }

        public V2GoldenCaseExpected LoadExpected(string path)
        {
            return ParseExpected(File.ReadAllText(RequireFile(path)));
        }

        public V2GoldenCaseManifest ParseManifest(string json)
        {
            V2GoldenCaseUnknownFieldValidator.Validate<V2GoldenCaseManifest>(
                json,
                "manifest");
            var manifest = Deserialize<V2GoldenCaseManifest>(json, "manifest");
            if (!string.Equals(manifest.SchemaVersion, ManifestSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Unsupported manifest SchemaVersion '" + manifest.SchemaVersion
                    + "'. Expected '" + ManifestSchemaVersion + "'.");
            }

            if (manifest.Cases == null || manifest.Cases.Length == 0)
            {
                throw new InvalidDataException("Manifest must contain at least one case.");
            }

            EnsureUniquePositiveOrder(manifest.Cases.Select(item => item.Order), "manifest case");
            EnsureUniqueRequiredValues(manifest.Cases.Select(item => item.CaseId), "CaseId");

            foreach (var item in manifest.Cases)
            {
                RequireText(item.CaseId, "CaseId");
                V2GoldenCasePathGuard.ValidateRelativePath(item.ExpectedFile, "ExpectedFile");
                ValidateLegacyConfiguration(item);

                if (item.Inputs == null || item.Inputs.Length != 2)
                {
                    throw new InvalidDataException(
                        "Case '" + item.CaseId + "' must define exactly two document inputs.");
                }

                EnsureUniqueRequiredValues(item.Inputs.Select(input => input.DocumentId), "DocumentId");
                EnsureUniqueRequiredValues(item.Inputs.Select(input => input.Role), "Role");

                var roles = new HashSet<string>(item.Inputs.Select(input => input.Role), StringComparer.Ordinal);
                if (!roles.SetEquals(new[] { "Report", "Explanation" }))
                {
                    throw new InvalidDataException(
                        "Case '" + item.CaseId + "' must contain Report and Explanation roles.");
                }

                foreach (var input in item.Inputs)
                {
                    RequireText(input.DocumentId, "DocumentId");
                    V2GoldenCasePathGuard.ValidateRelativePath(input.File, "Input File");
                    if (!Sha256Pattern.IsMatch(input.Sha256 ?? string.Empty))
                    {
                        throw new InvalidDataException(
                            "Input '" + input.DocumentId + "' must contain a lowercase SHA256 value.");
                    }
                }
            }

            return manifest;
        }

        public V2GoldenCaseExpected ParseExpected(string json)
        {
            string schemaVersion = ReadSchemaVersion(json);
            V2GoldenCaseExpected expected;

            if (string.Equals(
                schemaVersion,
                ExpectedSchemaVersionV1,
                StringComparison.Ordinal))
            {
                V2GoldenCaseUnknownFieldValidator
                    .Validate<V2GoldenCaseExpectedV1Schema>(
                        json,
                        "expected result");
                V2GoldenCaseExpectedV1Schema source =
                    Deserialize<V2GoldenCaseExpectedV1Schema>(
                        json,
                        "expected result");
                ValidateV1Shape(source);
                expected = V2GoldenCaseExpectedSchemaAdapter.FromV1(source);
            }
            else if (string.Equals(
                schemaVersion,
                ExpectedSchemaVersionV2,
                StringComparison.Ordinal))
            {
                V2GoldenCaseUnknownFieldValidator
                    .Validate<V2GoldenCaseExpectedV2Schema>(
                        json,
                        "expected result");
                V2GoldenCaseExpectedV2Schema source =
                    Deserialize<V2GoldenCaseExpectedV2Schema>(
                        json,
                        "expected result");
                ValidateV2Shape(source);
                expected = V2GoldenCaseExpectedSchemaAdapter.FromV2(source);
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported expected SchemaVersion '" + schemaVersion
                    + "'. Expected '" + ExpectedSchemaVersionV1
                    + "' or '" + ExpectedSchemaVersionV2 + "'.");
            }

            ValidateExpected(expected);
            return expected;
        }

        private static void ValidateExpected(V2GoldenCaseExpected expected)
        {
            RequireText(expected.CaseId, "CaseId");
            if (expected.ExpectedSemanticCallCount < 0)
            {
                throw new InvalidDataException("ExpectedSemanticCallCount cannot be negative.");
            }

            V2GoldenCaseExtendedSchemaValidator.Validate(expected);

            if (expected.ExpectedExecutions == null || expected.ExpectedExecutions.Length == 0)
            {
                throw new InvalidDataException("ExpectedExecutions cannot be empty.");
            }

            EnsureUniquePositiveOrder(
                expected.ExpectedExecutions.Select(item => item.Order),
                "expected execution");
            EnsureUniqueRequiredValues(
                expected.ExpectedExecutions.Select(item => item.CheckerName),
                "execution CheckerName");
            EnsureUniqueRequiredValues(
                expected.ExpectedExecutions.Select(item => item.FactType),
                "execution FactType");

            foreach (var execution in expected.ExpectedExecutions)
            {
                RequireText(execution.CheckerName, "CheckerName");
                RequireEnum<FactType>(execution.FactType, "execution FactType");
                RequireEnum<FactCheckExecutionStatus>(execution.ExecutionStatus, "ExecutionStatus");
                if (execution.Results == null
                    || execution.Results.Length != execution.ExpectedResultCount)
                {
                    throw new InvalidDataException(
                        "Execution Results length must equal ExpectedResultCount.");
                }

                foreach (V2GoldenCaseConsistencyResultAssertion result
                    in execution.Results)
                {
                    ValidateIdentity(result.Identity, "execution result Identity");
                    if (!string.Equals(
                        result.Identity.FactType,
                        execution.FactType,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "Result Identity FactType must equal its parent Execution FactType.");
                    }
                }

                if (string.Equals(
                    expected.SchemaVersion,
                    ExpectedSchemaVersionV2,
                    StringComparison.Ordinal))
                {
                    ValidateV2ExecutionSemantics(execution);
                }
            }

            if (expected.ExpectedAnalysis == null)
            {
                throw new InvalidDataException("ExpectedAnalysis is required.");
            }

            RequireEnum<ConsistencyStatus>(expected.ExpectedAnalysis.StatusSummary, "StatusSummary");
            if (expected.ExpectedAnalysis.ExpectedResultCount < 0)
            {
                throw new InvalidDataException("ExpectedResultCount cannot be negative.");
            }

            if (expected.ExpectedAnalysis.Results == null
                || expected.ExpectedAnalysis.Results.Length != expected.ExpectedAnalysis.ExpectedResultCount)
            {
                throw new InvalidDataException(
                    "Results length must equal ExpectedResultCount.");
            }

            EnsureUniquePositiveOrder(
                expected.ExpectedAnalysis.Results.Select(item => item.Order),
                "expected consistency result");
            EnsureUniqueRequiredValues(
                expected.ExpectedAnalysis.Results.Select(
                    item => IdentityKey(item.Identity)),
                "result Identity");

            foreach (var result in expected.ExpectedAnalysis.Results)
            {
                ValidateIdentity(result.Identity, "result Identity");
                RequireEnum<ConsistencyStatus>(result.Status, "result Status");
                RequireText(result.ReasonContains, "ReasonContains");
                if (result.RequiredEvidence == null
                    || result.RequiredEvidence.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidDataException("RequiredEvidence must contain non-empty values.");
                }
            }
        }

        private string ReadSchemaVersion(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException(
                    "The expected result JSON cannot be empty.");
            }

            try
            {
                var root = serializer.DeserializeObject(json)
                    as IDictionary<string, object>;
                object rawVersion;
                if (root == null
                    || !root.TryGetValue("SchemaVersion", out rawVersion)
                    || !(rawVersion is string))
                {
                    throw new InvalidDataException(
                        "Expected result SchemaVersion is required.");
                }

                return (string)rawVersion;
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Invalid expected result JSON.",
                    exception);
            }
        }

        private static void ValidateV1Shape(
            V2GoldenCaseExpectedV1Schema source)
        {
            if (source.ExpectedExecutions == null
                || source.ExpectedAnalysis == null
                || source.ExpectedAnalysis.Results == null)
            {
                throw new InvalidDataException(
                    "Schema 1.0 ExpectedExecutions and ExpectedAnalysis are required.");
            }
        }

        private static void ValidateV2Shape(
            V2GoldenCaseExpectedV2Schema source)
        {
            if (source.ExpectedExecutions == null
                || source.ExpectedAnalysis == null)
            {
                throw new InvalidDataException(
                    "Schema 2.0 ExpectedExecutions and ExpectedAnalysis are required.");
            }

            foreach (V2GoldenCaseExecutionAssertionV2Schema execution
                in source.ExpectedExecutions)
            {
                if (execution == null || execution.Results == null)
                {
                    throw new InvalidDataException(
                        "Schema 2.0 Execution Results cannot be null.");
                }
            }
        }

        private static void ValidateV2ExecutionSemantics(
            V2GoldenCaseExecutionAssertion execution)
        {
            FactCheckExecutionStatus status;
            Enum.TryParse(execution.ExecutionStatus, false, out status);

            if (status == FactCheckExecutionStatus.NotApplicable)
            {
                if (execution.ExpectedResultCount != 0
                    || string.IsNullOrWhiteSpace(execution.NoResultReason)
                    || !string.IsNullOrWhiteSpace(execution.ErrorInformation))
                {
                    throw new InvalidDataException(
                        "NotApplicable Execution requires zero Results, NoResultReason, and no ErrorInformation.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(execution.NoResultReason))
            {
                throw new InvalidDataException(
                    "Only NotApplicable Execution can contain NoResultReason.");
            }

            if (status == FactCheckExecutionStatus.Failed)
            {
                RequireText(
                    execution.ErrorInformation,
                    "Failed Execution ErrorInformation");
            }
            else if (!string.IsNullOrWhiteSpace(execution.ErrorInformation))
            {
                throw new InvalidDataException(
                    "Only Failed Execution can contain ErrorInformation.");
            }

            if (status == FactCheckExecutionStatus.Succeeded)
            {
                if (execution.ExpectedResultCount == 0
                    || execution.Results.Any(result => string.Equals(
                        result.Status,
                        ConsistencyStatus.WARNING.ToString(),
                        StringComparison.Ordinal)))
                {
                    throw new InvalidDataException(
                        "Succeeded Execution requires Results and cannot contain WARNING.");
                }
            }

            if (status == FactCheckExecutionStatus.CompletedWithWarning
                && (execution.ExpectedResultCount == 0
                    || !execution.Results.Any(result => string.Equals(
                        result.Status,
                        ConsistencyStatus.WARNING.ToString(),
                        StringComparison.Ordinal))))
            {
                throw new InvalidDataException(
                    "CompletedWithWarning Execution requires at least one WARNING Result.");
            }
        }

        private static void ValidateIdentity(
            V2GoldenCaseResultIdentityAssertion identity,
            string name)
        {
            if (identity == null)
            {
                throw new InvalidDataException(name + " is required.");
            }

            FactType factType;
            if (string.IsNullOrWhiteSpace(identity.FactType)
                || !Enum.TryParse(identity.FactType, false, out factType))
            {
                throw new InvalidDataException(name + " FactType is invalid.");
            }

            try
            {
                new ConsistencyResultIdentity(
                    factType,
                    identity.SubjectKey,
                    identity.ResultRole,
                    identity.ScopeKey);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    name + " is invalid.",
                    exception);
            }
        }

        private static string IdentityKey(
            V2GoldenCaseResultIdentityAssertion identity)
        {
            if (identity == null)
            {
                return string.Empty;
            }

            return string.Concat(
                identity.FactType,
                "|",
                identity.SubjectKey,
                "|",
                identity.ResultRole,
                "|",
                identity.ScopeKey);
        }

        private static void ValidateLegacyConfiguration(V2GoldenCaseManifestEntry item)
        {
            var hasLegacyExpectedFile = !string.IsNullOrWhiteSpace(item.LegacyExpectedFile);
            var hasLegacySchemaVersion = !string.IsNullOrWhiteSpace(item.LegacySchemaVersion);
            if (hasLegacyExpectedFile != hasLegacySchemaVersion)
            {
                throw new InvalidDataException(
                    "Case '" + item.CaseId
                    + "' must define both LegacyExpectedFile and LegacySchemaVersion, or neither.");
            }

            if (!hasLegacyExpectedFile)
            {
                return;
            }

            V2GoldenCasePathGuard.ValidateRelativePath(
                item.LegacyExpectedFile,
                "LegacyExpectedFile");
            if (!string.Equals(
                    item.LegacySchemaVersion,
                    V2GoldenLegacyExpectedAdapter.SchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Unsupported LegacySchemaVersion '" + item.LegacySchemaVersion
                    + "'. Expected '" + V2GoldenLegacyExpectedAdapter.SchemaVersion + "'.");
            }
        }

        private T Deserialize<T>(string json, string description)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("The " + description + " JSON cannot be empty.");
            }

            try
            {
                var value = serializer.Deserialize<T>(json);
                if (value == null)
                {
                    throw new InvalidDataException("The " + description + " JSON produced no value.");
                }

                return value;
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Invalid " + description + " JSON.", exception);
            }
        }

        private static string RequireFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A JSON file path is required.", nameof(path));
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Golden Case JSON file was not found.", fullPath);
            }

            return fullPath;
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
                throw new InvalidDataException(name + " contains an unsupported value '" + value + "'.");
            }
        }

        private static void EnsureUniquePositiveOrder(IEnumerable<int> values, string name)
        {
            var materialized = values.ToArray();
            if (materialized.Any(value => value <= 0)
                || materialized.Distinct().Count() != materialized.Length)
            {
                throw new InvalidDataException(name + " Order values must be positive and unique.");
            }
        }

        private static void EnsureUniqueRequiredValues(IEnumerable<string> values, string name)
        {
            var materialized = values.ToArray();
            if (materialized.Any(string.IsNullOrWhiteSpace)
                || materialized.Distinct(StringComparer.Ordinal).Count() != materialized.Length)
            {
                throw new InvalidDataException(name + " values must be non-empty and unique.");
            }
        }
    }
}
