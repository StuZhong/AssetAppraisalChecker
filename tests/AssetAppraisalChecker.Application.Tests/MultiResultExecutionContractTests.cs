using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class MultiResultExecutionContractTests
    {
        private const string CheckerName = "SyntheticMultiResultChecker";

        [TestMethod]
        public void CaseA_TwoPassResults_ExecutionSucceededAndOverallPass()
        {
            V2AnalysisResult analysis = Run(
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { Result("subject-a", ConsistencyStatus.PASS), Result("subject-b", ConsistencyStatus.PASS) }));

            Assert.AreEqual(FactCheckExecutionStatus.Succeeded, analysis.Executions[0].ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.PASS, analysis.StatusSummary);
            Assert.HasCount(2, analysis.Results);
        }

        [TestMethod]
        public void CaseB_PassAndWarning_ExecutionCompletedWithWarningAndOverallWarning()
        {
            V2AnalysisResult analysis = Run(
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { Result("subject-a", ConsistencyStatus.PASS), Result("subject-b", ConsistencyStatus.WARNING) }));

            Assert.AreEqual(FactCheckExecutionStatus.CompletedWithWarning, analysis.Executions[0].ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.WARNING, analysis.StatusSummary);
        }

        [TestMethod]
        public void CaseC_PassAndFail_ExecutionSucceededAndOverallFail()
        {
            V2AnalysisResult analysis = Run(
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { Result("subject-a", ConsistencyStatus.PASS), Result("subject-b", ConsistencyStatus.FAIL) }));

            Assert.AreEqual(FactCheckExecutionStatus.Succeeded, analysis.Executions[0].ExecutionStatus);
            Assert.AreEqual(ConsistencyStatus.FAIL, analysis.StatusSummary);
        }

        [TestMethod]
        public void CaseD_CheckerException_IsFailedWithoutInventedBusinessResult()
        {
            var pipeline = new V2DocumentAnalysisPipeline(
                new FactCheckRegistry(new IFactChecker[] { new ThrowingSyntheticChecker() }));

            V2AnalysisResult analysis = pipeline.Analyze(Input("report"), Input("explanation"));

            Assert.AreEqual(FactCheckExecutionStatus.Failed, analysis.Executions[0].ExecutionStatus);
            Assert.HasCount(0, analysis.Executions[0].Results);
            Assert.HasCount(0, analysis.Results);
            Assert.AreEqual(ConsistencyStatus.WARNING, analysis.StatusSummary);
        }

        [TestMethod]
        public void CaseE_NotApplicable_HasNoResultAndRequiresReason()
        {
            FactCheckExecutionEnvelope envelope =
                FactCheckExecutionEnvelope.NotApplicable(
                    CheckerName,
                    FactType.EvaluationMethod,
                    "文档不包含适用的合成比较单元。");

            Assert.AreEqual(FactCheckExecutionStatus.NotApplicable, envelope.ExecutionStatus);
            Assert.HasCount(0, envelope.Results);
            Assert.AreEqual("文档不包含适用的合成比较单元。", envelope.NoResultReason);
            Assert.ThrowsExactly<ArgumentException>(() =>
                new FactCheckExecutionEnvelope(
                    CheckerName,
                    FactType.EvaluationMethod,
                    FactCheckExecutionStatus.NotApplicable,
                    new ConsistencyResult[0],
                    new string[0],
                    null,
                    string.Empty));
        }

        [TestMethod]
        public void CaseF_DuplicateResultIdentity_IsRejected()
        {
            ConsistencyResult first = Result("subject-a", ConsistencyStatus.PASS);
            ConsistencyResult duplicate = Result("subject-a", ConsistencyStatus.FAIL);

            Assert.ThrowsExactly<ArgumentException>(() =>
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { first, duplicate }));
        }

        [TestMethod]
        public void CaseG_SameFactTypeDifferentSubjects_CoexistAndSingleProjectionFailsClosed()
        {
            FactCheckExecutionEnvelope envelope =
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { Result("subject-a", ConsistencyStatus.PASS), Result("subject-b", ConsistencyStatus.PASS) });

            Assert.HasCount(2, envelope.Results);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ConsistencyResult ignored = envelope.ConsistencyResult;
            });
        }

        [TestMethod]
        public void CaseH_ReversingResultOrder_DoesNotChangeStatusOrEvidenceProjection()
        {
            ConsistencyResult first = Result("subject-a", ConsistencyStatus.PASS);
            ConsistencyResult second = Result("subject-b", ConsistencyStatus.FAIL);
            FactCheckExecutionEnvelope forward =
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { first, second });
            FactCheckExecutionEnvelope reverse =
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    new[] { second, first });

            Assert.AreEqual(forward.ExecutionStatus, reverse.ExecutionStatus);
            CollectionAssert.AreEqual(forward.Evidence.ToArray(), reverse.Evidence.ToArray());
            Assert.AreEqual(Run(forward).StatusSummary, Run(reverse).StatusSummary);
        }

        [TestMethod]
        public void Identity_IsImmutableDeterministicValueAndRejectsUnnormalizedOrSensitiveKeys()
        {
            ConsistencyResultIdentity first = Identity("subject-a");
            ConsistencyResultIdentity same = Identity("subject-a");
            ConsistencyResultIdentity other = Identity("subject-b");

            Assert.AreEqual(first, same);
            Assert.AreEqual(first.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(first, other);
            Assert.AreEqual(
                "EvaluationMethod|subject/v1:subject-a|SyntheticConsistency|scope/v1:default",
                first.CanonicalValue);
            Assert.ThrowsExactly<ArgumentException>(() =>
                new ConsistencyResultIdentity(
                    FactType.EvaluationMethod,
                    "某真实公司",
                    "SyntheticConsistency",
                    ConsistencyResultIdentity.DefaultScopeKey));
            Assert.ThrowsExactly<ArgumentException>(() =>
                new ConsistencyResultIdentity(
                    FactType.EvaluationMethod,
                    "subject/v1:Subject-A",
                    "SyntheticConsistency",
                    ConsistencyResultIdentity.DefaultScopeKey));
        }

        [TestMethod]
        public void ResultsAndEvidence_AreReadOnlySnapshotsAndEvidenceDoesNotLeakAcrossResults()
        {
            var mutable = new List<ConsistencyResult>
            {
                Result("subject-a", ConsistencyStatus.PASS)
            };
            FactCheckExecutionEnvelope envelope =
                FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType.EvaluationMethod,
                    mutable);
            mutable.Add(Result("subject-b", ConsistencyStatus.PASS));

            Assert.HasCount(1, envelope.Results);
            Assert.ThrowsExactly<NotSupportedException>(() =>
                ((IList<ConsistencyResult>)envelope.Results).Add(
                    Result("subject-b", ConsistencyStatus.PASS)));
            Assert.IsFalse(envelope.Results[0].Evidence.Any(
                item => item.IndexOf("subject-b", StringComparison.Ordinal) >= 0));
        }

        [TestMethod]
        public void PassAndNotApplicable_OverallRemainsPass()
        {
            ConsistencyResult pass =
                Result("subject-a", ConsistencyStatus.PASS);
            FactCheckExecutionEnvelope succeeded =
                FactCheckExecutionEnvelope.FromResult(CheckerName, pass);
            FactCheckExecutionEnvelope notApplicable =
                FactCheckExecutionEnvelope.NotApplicable(
                    "SyntheticNotApplicableChecker",
                    FactType.AssessmentValue,
                    "该合成检查在当前输入中确定性不适用。");

            var analysis = new V2AnalysisResult(
                new[] { pass },
                new[] { succeeded, notApplicable });

            Assert.AreEqual(ConsistencyStatus.PASS, analysis.StatusSummary);
            Assert.AreEqual(
                FactCheckExecutionStatus.NotApplicable,
                notApplicable.ExecutionStatus);
            Assert.HasCount(0, notApplicable.Results);
        }

        [TestMethod]
        public void AllExecutionsNotApplicable_OverallIsFailClosedWarning()
        {
            FactCheckExecutionEnvelope first =
                FactCheckExecutionEnvelope.NotApplicable(
                    "SyntheticObjectChecker",
                    FactType.AssessmentObject,
                    "评估对象检查确定性不适用。");
            FactCheckExecutionEnvelope second =
                FactCheckExecutionEnvelope.NotApplicable(
                    "SyntheticValueChecker",
                    FactType.AssessmentValue,
                    "评估价值检查确定性不适用。");

            var analysis = new V2AnalysisResult(
                Array.Empty<ConsistencyResult>(),
                new[] { first, second });

            Assert.AreEqual(ConsistencyStatus.WARNING, analysis.StatusSummary);
            Assert.HasCount(0, analysis.Results);
            Assert.IsTrue(analysis.Executions.All(execution =>
                execution.ExecutionStatus
                    == FactCheckExecutionStatus.NotApplicable));
            Assert.IsTrue(analysis.Executions.All(execution =>
                !string.IsNullOrWhiteSpace(execution.NoResultReason)));
        }

        private static V2AnalysisResult Run(FactCheckExecutionEnvelope execution)
        {
            var pipeline = new V2DocumentAnalysisPipeline(
                new FactCheckRegistry(
                    new IFactChecker[]
                    {
                        new SyntheticMultiResultChecker(execution)
                    }));
            return pipeline.Analyze(Input("report"), Input("explanation"));
        }

        private static ConsistencyResult Result(
            string subject,
            ConsistencyStatus status)
        {
            return new ConsistencyResult(
                Identity(subject),
                status,
                "Synthetic " + subject + " " + status + ".",
                new[] { "Evidence=" + subject });
        }

        private static ConsistencyResultIdentity Identity(string subject)
        {
            return new ConsistencyResultIdentity(
                FactType.EvaluationMethod,
                "subject/v1:" + subject,
                "SyntheticConsistency",
                ConsistencyResultIdentity.DefaultScopeKey);
        }

        private static V2DocumentAnalysisInput Input(string sourceDocument)
        {
            const string text = "Synthetic Multi-Result contract input.";
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
            return new V2DocumentAnalysisInput(
                DocumentReadResult.Succeeded(new[] { element }, 1, 0),
                sourceDocument);
        }

        private sealed class SyntheticMultiResultChecker : IFactChecker
        {
            private readonly FactCheckExecutionEnvelope execution;

            public SyntheticMultiResultChecker(FactCheckExecutionEnvelope execution)
            {
                this.execution = execution;
            }

            public string CheckerName => MultiResultExecutionContractTests.CheckerName;
            public FactType FactType => FactType.EvaluationMethod;

            public FactCheckExecutionEnvelope Execute(
                V2DocumentAnalysisInput firstDocument,
                V2DocumentAnalysisInput secondDocument)
            {
                return execution;
            }
        }

        private sealed class ThrowingSyntheticChecker : IFactChecker
        {
            public string CheckerName => MultiResultExecutionContractTests.CheckerName;
            public FactType FactType => FactType.EvaluationMethod;

            public FactCheckExecutionEnvelope Execute(
                V2DocumentAnalysisInput firstDocument,
                V2DocumentAnalysisInput secondDocument)
            {
                throw new InvalidOperationException("Synthetic controlled failure.");
            }
        }
    }
}
