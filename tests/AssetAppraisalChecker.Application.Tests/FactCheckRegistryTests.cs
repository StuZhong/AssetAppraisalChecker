using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class FactCheckRegistryTests
    {
        [TestMethod]
        public void DefaultRegistry_ExplicitlyRegistersExistingCheckersInStableOrder()
        {
            IFactCheckRegistry registry =
                DefaultFactCheckRegistryFactory.Create(new MockSemanticAnalyzer());

            Assert.HasCount(5, registry.Checkers);
            Assert.AreEqual(
                AssessmentObjectChecker.Name,
                registry.Checkers[0].CheckerName);
            Assert.AreEqual(
                FactType.AssessmentObject,
                registry.Checkers[0].FactType);
            Assert.AreEqual(
                AssessmentValueChecker.Name,
                registry.Checkers[1].CheckerName);
            Assert.AreEqual(
                FactType.AssessmentValue,
                registry.Checkers[1].FactType);
            Assert.AreEqual(
                BenchmarkDateChecker.Name,
                registry.Checkers[2].CheckerName);
            Assert.AreEqual(
                FactType.BenchmarkDate,
                registry.Checkers[2].FactType);
            Assert.AreEqual(
                ReportNumberChecker.Name,
                registry.Checkers[3].CheckerName);
            Assert.AreEqual(
                FactType.ReportNumber,
                registry.Checkers[3].FactType);
            Assert.AreEqual(
                EvaluationMethodChecker.Name,
                registry.Checkers[4].CheckerName);
            Assert.AreEqual(
                FactType.EvaluationMethod,
                registry.Checkers[4].FactType);

            Assert.IsTrue(
                registry.TryGet(
                    FactType.BenchmarkDate,
                    out IFactChecker benchmarkDateChecker));
            Assert.AreSame(registry.Checkers[2], benchmarkDateChecker);
            Assert.IsTrue(
                registry.TryGet(
                    FactType.ReportNumber,
                    out IFactChecker reportNumberChecker));
            Assert.AreSame(registry.Checkers[3], reportNumberChecker);
            Assert.IsTrue(
                registry.TryGet(
                    FactType.EvaluationMethod,
                    out IFactChecker evaluationMethodChecker));
            Assert.AreSame(registry.Checkers[4], evaluationMethodChecker);
        }

        [TestMethod]
        public void Registry_DiscoversCheckerByFactType()
        {
            var expected = new RecordingChecker(
                "value-checker",
                FactType.AssessmentValue,
                CreatePassEnvelope("value-checker", FactType.AssessmentValue));
            var registry = new FactCheckRegistry(new[] { expected });

            bool found = registry.TryGet(
                FactType.AssessmentValue,
                out IFactChecker actual);
            bool missing = registry.TryGet(
                FactType.BenchmarkDate,
                out IFactChecker missingChecker);

            Assert.IsTrue(found);
            Assert.AreSame(expected, actual);
            Assert.IsFalse(missing);
            Assert.IsNull(missingChecker);
        }

        [TestMethod]
        public void Registry_CopiesRegistrationAndExposesReadOnlyCollection()
        {
            var registrations = new List<IFactChecker>
            {
                new RecordingChecker(
                    "object-checker",
                    FactType.AssessmentObject,
                    CreatePassEnvelope(
                        "object-checker",
                        FactType.AssessmentObject))
            };
            var registry = new FactCheckRegistry(registrations);
            registrations.Clear();

            Assert.HasCount(1, registry.Checkers);
            var exposed = (IList<IFactChecker>)registry.Checkers;
            Assert.ThrowsExactly<NotSupportedException>(
                () => exposed.Add(
                    new RecordingChecker(
                        "value-checker",
                        FactType.AssessmentValue,
                        CreatePassEnvelope(
                            "value-checker",
                            FactType.AssessmentValue))));
        }

        [TestMethod]
        public void Registry_RejectsDuplicateFactType()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new FactCheckRegistry(
                    new IFactChecker[]
                    {
                        new RecordingChecker(
                            "object-checker-a",
                            FactType.AssessmentObject,
                            CreatePassEnvelope(
                                "object-checker-a",
                                FactType.AssessmentObject)),
                        new RecordingChecker(
                            "object-checker-b",
                            FactType.AssessmentObject,
                            CreatePassEnvelope(
                                "object-checker-b",
                                FactType.AssessmentObject))
                    }));
        }

        [TestMethod]
        public void ExecutionEnvelope_PreservesIdentityResultEvidenceAndReadOnlySnapshot()
        {
            var mutableEvidence = new List<string> { "report | evidence" };
            var result = new ConsistencyResult(
                FactType.AssessmentValue,
                ConsistencyStatus.PASS,
                "测试一致。",
                mutableEvidence);
            var envelope = new FactCheckExecutionEnvelope(
                "value-checker",
                FactType.AssessmentValue,
                FactCheckExecutionStatus.Succeeded,
                result,
                mutableEvidence,
                null);

            mutableEvidence.Add("external mutation");

            Assert.AreEqual("value-checker", envelope.CheckerName);
            Assert.AreEqual(FactType.AssessmentValue, envelope.FactType);
            Assert.AreEqual(
                FactCheckExecutionStatus.Succeeded,
                envelope.ExecutionStatus);
            Assert.AreSame(result, envelope.ConsistencyResult);
            Assert.HasCount(1, envelope.Evidence);
            Assert.AreEqual("report | evidence", envelope.Evidence[0]);
            Assert.AreEqual(string.Empty, envelope.ErrorInformation);
            Assert.ThrowsExactly<NotSupportedException>(
                () => ((IList<string>)envelope.Evidence).Add("mutation"));
        }

        [TestMethod]
        public void Pipeline_ExecutesCheckerDiscoveredFromRegistry()
        {
            FactCheckExecutionEnvelope expected =
                CreatePassEnvelope("custom-checker", FactType.EvaluationMethod);
            var checker = new RecordingChecker(
                "custom-checker",
                FactType.EvaluationMethod,
                expected);
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(
                    new FactCheckRegistry(new[] { checker }));

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report"),
                CreateInput("explanation"));

            Assert.AreEqual(1, checker.CallCount);
            Assert.HasCount(1, result.Executions);
            Assert.AreSame(expected, result.Executions[0]);
            Assert.HasCount(1, result.Results);
            Assert.AreEqual(FactType.EvaluationMethod, result.Results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
        }

        [TestMethod]
        public void Pipeline_ConvertsUnexpectedCheckerExceptionToFailedEnvelope()
        {
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(
                    new FactCheckRegistry(
                        new IFactChecker[]
                        {
                            new ThrowingChecker(
                                "throwing-checker",
                                FactType.AssessmentValue)
                        }));

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report"),
                CreateInput("explanation"));

            Assert.HasCount(1, result.Executions);
            FactCheckExecutionEnvelope execution = result.Executions[0];
            Assert.AreEqual(
                FactCheckExecutionStatus.Failed,
                execution.ExecutionStatus);
            Assert.AreEqual("throwing-checker", execution.CheckerName);
            Assert.AreEqual(FactType.AssessmentValue, execution.FactType);
            StringAssert.Contains(execution.ErrorInformation, "受控异常");
            Assert.HasCount(0, execution.Results);
            Assert.IsNull(execution.ConsistencyResult);
            Assert.AreEqual(ConsistencyStatus.WARNING, result.StatusSummary);
        }

        private static FactCheckExecutionEnvelope CreatePassEnvelope(
            string checkerName,
            FactType factType)
        {
            var result = new ConsistencyResult(
                factType,
                ConsistencyStatus.PASS,
                "测试一致。",
                new[] { "test evidence" });
            return FactCheckExecutionEnvelope.FromResult(checkerName, result);
        }

        private static V2DocumentAnalysisInput CreateInput(string sourceDocument)
        {
            const string text = "Registry Pipeline 测试文档。";
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                0,
                null,
                null,
                null,
                new string[0]);
            var element = new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                0,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[] { element },
                1,
                0);

            return new V2DocumentAnalysisInput(document, sourceDocument);
        }

        private sealed class RecordingChecker : IFactChecker
        {
            private readonly FactCheckExecutionEnvelope _execution;

            public RecordingChecker(
                string checkerName,
                FactType factType,
                FactCheckExecutionEnvelope execution)
            {
                CheckerName = checkerName;
                FactType = factType;
                _execution = execution;
            }

            public string CheckerName { get; }

            public FactType FactType { get; }

            public int CallCount { get; private set; }

            public FactCheckExecutionEnvelope Execute(
                V2DocumentAnalysisInput firstDocument,
                V2DocumentAnalysisInput secondDocument)
            {
                CallCount++;
                return _execution;
            }
        }

        private sealed class ThrowingChecker : IFactChecker
        {
            public ThrowingChecker(string checkerName, FactType factType)
            {
                CheckerName = checkerName;
                FactType = factType;
            }

            public string CheckerName { get; }

            public FactType FactType { get; }

            public FactCheckExecutionEnvelope Execute(
                V2DocumentAnalysisInput firstDocument,
                V2DocumentAnalysisInput secondDocument)
            {
                throw new InvalidOperationException("Registry 测试受控异常。");
            }
        }
    }
}
