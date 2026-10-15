using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Facts;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public sealed class V2GoldenCaseRunner
    {
        private readonly V2GoldenCaseJsonLoader jsonLoader;
        private readonly V2GoldenCaseResultAssertor resultAssertor;
        private readonly V2GoldenFactAssertorRegistry factAssertors;
        private readonly V2GoldenCaseOpenXmlPreflight openXmlPreflight;
        private readonly V2GoldenCasePrivacyValidator privacyValidator;

        public V2GoldenCaseRunner()
            : this(
                new V2GoldenCaseJsonLoader(),
                new V2GoldenCaseResultAssertor(),
                new V2GoldenFactAssertorRegistry(),
                new V2GoldenCaseOpenXmlPreflight(),
                new V2GoldenCasePrivacyValidator())
        {
        }

        internal V2GoldenCaseRunner(
            V2GoldenCaseJsonLoader jsonLoader,
            V2GoldenCaseResultAssertor resultAssertor,
            V2GoldenFactAssertorRegistry factAssertors,
            V2GoldenCaseOpenXmlPreflight openXmlPreflight,
            V2GoldenCasePrivacyValidator privacyValidator)
        {
            this.jsonLoader = jsonLoader ?? throw new ArgumentNullException(nameof(jsonLoader));
            this.resultAssertor = resultAssertor ?? throw new ArgumentNullException(nameof(resultAssertor));
            this.factAssertors = factAssertors ?? throw new ArgumentNullException(nameof(factAssertors));
            this.openXmlPreflight = openXmlPreflight ?? throw new ArgumentNullException(nameof(openXmlPreflight));
            this.privacyValidator = privacyValidator ?? throw new ArgumentNullException(nameof(privacyValidator));
        }

        public V2GoldenCaseExecutionResult Run(string manifestPath, string caseId)
        {
            var hashesBefore = new Dictionary<string, string>(StringComparer.Ordinal);
            var hashesAfter = new Dictionary<string, string>(StringComparer.Ordinal);
            var resolvedInputs = new Dictionary<string, string>(StringComparer.Ordinal);
            var preflightStatus = new Dictionary<string, bool>(StringComparer.Ordinal);
            var privacyStatus = new Dictionary<string, bool>(StringComparer.Ordinal);
            var snapshots = new List<V2GoldenCaseFactSnapshot>();
            var failures = new List<V2GoldenCaseAssertionFailure>();
            V2AnalysisResult analysisResult = null;
            V2GoldenSemanticAnalyzerSession semanticSession = null;

            try
            {
                if (string.IsNullOrWhiteSpace(manifestPath))
                {
                    throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
                }

                if (string.IsNullOrWhiteSpace(caseId))
                {
                    throw new ArgumentException("CaseId is required.", nameof(caseId));
                }

                string fullManifestPath = Path.GetFullPath(manifestPath);
                V2GoldenCaseManifest manifest = jsonLoader.LoadManifest(fullManifestPath);
                V2GoldenCaseManifestEntry manifestCase = manifest.Cases.SingleOrDefault(
                    item => string.Equals(item.CaseId, caseId, StringComparison.Ordinal));
                if (manifestCase == null)
                {
                    throw new InvalidDataException(
                        "Case '" + caseId + "' is not registered in the manifest.");
                }

                string goldenCaseRoot = Path.GetDirectoryName(fullManifestPath);
                string expectedPath = V2GoldenCasePathGuard.ResolveExistingFile(
                    goldenCaseRoot,
                    manifestCase.ExpectedFile);
                V2GoldenCaseExpected expected = jsonLoader.LoadExpected(expectedPath);
                if (!string.Equals(expected.CaseId, caseId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Expected CaseId '" + expected.CaseId
                        + "' does not match manifest CaseId '" + caseId + "'.");
                }

                semanticSession = V2GoldenSemanticAnalyzerFactory.Create(expected.Semantic);

                foreach (V2GoldenCaseInputDefinition input in manifestCase.Inputs)
                {
                    string inputPath = V2GoldenCasePathGuard.ResolveExistingFile(
                        goldenCaseRoot,
                        input.File);
                    resolvedInputs.Add(input.DocumentId, inputPath);
                    string actualHash = V2GoldenCaseSha256.Compute(inputPath);
                    hashesBefore.Add(input.DocumentId, actualHash);
                    if (!string.Equals(input.Sha256, actualHash, StringComparison.Ordinal))
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            "SHA256",
                            "Input '" + input.DocumentId + "' does not match the manifest hash."));
                    }

                    IReadOnlyList<V2GoldenCaseAssertionFailure> preflightFailures =
                        openXmlPreflight.Inspect(input.DocumentId, inputPath);
                    preflightStatus[input.DocumentId] = preflightFailures.Count == 0;
                    failures.AddRange(preflightFailures);

                    V2GoldenCasePrivacyScanResult privacyResult = privacyValidator.Inspect(
                        input.DocumentId,
                        inputPath,
                        actualHash);
                    privacyStatus[input.DocumentId] = privacyResult.IsSafe;
                    foreach (V2GoldenCasePrivacyFinding finding in privacyResult.Findings)
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            "Privacy[" + input.DocumentId + "]",
                            finding.ToString() + "; SHA256=" + actualHash));
                    }
                }

                if (failures.Count == 0)
                {
                    var reader = new DocxDocumentReader();
                    var documents = new Dictionary<string, DocumentReadResult>(StringComparer.Ordinal);
                    foreach (V2GoldenCaseInputDefinition input in manifestCase.Inputs)
                    {
                        DocumentReadResult document = reader.Read(resolvedInputs[input.DocumentId]);
                        documents.Add(input.DocumentId, document);
                        if (!document.IsSuccess)
                        {
                            failures.Add(new V2GoldenCaseAssertionFailure(
                                "DocumentRead",
                                input.DocumentId + ": " + document.Message));
                        }
                    }

                    if (failures.Count == 0)
                    {
                        V2GoldenFactAssertionReport factReport = factAssertors.Assert(
                            expected.ExpectedFacts,
                            documents);
                        snapshots.AddRange(factReport.Snapshots);
                        failures.AddRange(factReport.Failures);
                    }

                    if (failures.Count == 0)
                    {
                        V2GoldenCaseInputDefinition reportInput = manifestCase.Inputs.Single(
                            input => string.Equals(input.Role, "Report", StringComparison.Ordinal));
                        V2GoldenCaseInputDefinition explanationInput = manifestCase.Inputs.Single(
                            input => string.Equals(input.Role, "Explanation", StringComparison.Ordinal));
                        IV2DocumentAnalysisPipeline pipeline =
                            new V2DocumentAnalysisPipeline(semanticSession);
                        analysisResult = pipeline.Analyze(
                            new V2DocumentAnalysisInput(
                                documents[reportInput.DocumentId],
                                reportInput.DocumentId),
                            new V2DocumentAnalysisInput(
                                documents[explanationInput.DocumentId],
                                explanationInput.DocumentId));
                        failures.AddRange(resultAssertor.Assert(
                            expected,
                            analysisResult,
                            semanticSession.Calls));
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    "Runner",
                    exception.GetType().Name + ": " + exception.Message));
            }
            finally
            {
                foreach (KeyValuePair<string, string> input in resolvedInputs)
                {
                    if (!File.Exists(input.Value))
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            "SourceProtection",
                            "Input '" + input.Key + "' no longer exists after execution."));
                        continue;
                    }

                    string postHash = V2GoldenCaseSha256.Compute(input.Value);
                    hashesAfter[input.Key] = postHash;
                    string preHash;
                    if (hashesBefore.TryGetValue(input.Key, out preHash)
                        && !string.Equals(preHash, postHash, StringComparison.Ordinal))
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            "SourceProtection",
                            "Input '" + input.Key + "' changed during execution."));
                    }
                }
            }

            IReadOnlyList<V2GoldenCaseSemanticCallRecord> calls = semanticSession == null
                ? Array.Empty<V2GoldenCaseSemanticCallRecord>()
                : semanticSession.Calls;
            return new V2GoldenCaseExecutionResult(
                caseId,
                analysisResult,
                calls.Count,
                calls,
                snapshots,
                preflightStatus,
                privacyStatus,
                hashesBefore,
                hashesAfter,
                failures);
        }
    }
}
