using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Semantic;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class DesktopLocalAiServiceTests
    {
        [TestMethod]
        public async Task Unconfigured_PreservesRuleResults()
        {
            using (var a = Doc("评估基准日：2026年6月15日"))
            using (var b = Doc("评估基准日：2026年5月31日"))
            using (var service = new DesktopLocalAiService(null))
            {
                var request = new ReportCheckRequest(a.FilePath, b.FilePath, null);
                var expected = await new ReportCheckService().CheckAsync(request);
                var actual = await service.CheckAsync(request);
                CollectionAssert.AreEquivalent(expected.Issues.Select(Key).ToArray(),
                    actual.Issues.Select(Key).ToArray());
                Assert.IsTrue(actual.IsSuccess);
                Assert.Contains("AI 不可用", service.AiStatusMessage);
            }
        }

        [TestMethod]
        public async Task SingleDocument_DoesNotInitializeModels()
        {
            using (var a = Doc("评估基准日：2026年6月15日"))
            using (var service = new DesktopLocalAiService(() =>
                throw new AssertFailedException("Single document must not initialize AI")))
            {
                Assert.IsTrue((await service.CheckAsync(a.FilePath)).IsSuccess);
                Assert.Contains("单文档", service.AiStatusMessage);
                Assert.Contains("未调用 AI", service.AiStatusMessage);
            }
        }

        [TestMethod]
        public async Task Configured_NoCandidate_DoesNotClaimInference()
        {
            using (var a = Doc("房屋建筑物采用成本法。"))
            using (var b = Doc("房屋建筑物采用成本法。"))
            using (var service = new DesktopLocalAiService(() =>
                throw new AssertFailedException("No candidate must not initialize AI")))
            {
                Assert.Contains("尚未加载或执行", service.AiStatusMessage);
                Assert.IsTrue((await service.CheckAsync(new ReportCheckRequest(
                    a.FilePath, b.FilePath, null))).IsSuccess);
                Assert.Contains("未执行 AI 推理", service.AiStatusMessage);
            }
        }

        [TestMethod]
        public async Task ActivePipeline_InvokesRankerAndPreservesWarningAndHashes()
        {
            var ranker = new TestRanker();
            using (var a = Doc("房屋建筑物采用重置成本法。"))
            using (var b = Doc("房屋建筑物按现行重建成本测算。"))
            using (var service = new DesktopLocalAiService(() => ranker))
            {
                byte[] beforeA = File.ReadAllBytes(a.FilePath);
                byte[] beforeB = File.ReadAllBytes(b.FilePath);
                var result = await service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(1, ranker.Calls);
                Assert.IsFalse(ranker.ShadowOnly);
                Assert.IsTrue(ranker.PromotionBlocked);
                Assert.IsFalse(result.Issues.Any(i => i.Severity == IssueSeverity.Serious));
                Assert.Contains("实际推理完成", service.AiStatusMessage);
                CollectionAssert.AreEqual(beforeA, File.ReadAllBytes(a.FilePath));
                CollectionAssert.AreEqual(beforeB, File.ReadAllBytes(b.FilePath));
                await service.CheckAsync(a.FilePath);
                Assert.Contains("未调用 AI", service.AiStatusMessage);
                Assert.AreEqual(1, ranker.Calls);
            }
            Assert.IsTrue(ranker.Disposed);
        }

        [TestMethod]
        public async Task MissingAssets_RulesContinue_NoFabricatedBusinessFailOrSensitiveMessage()
        {
            int factories = 0;
            using (var a = Doc("房屋建筑物采用重置成本法。"))
            using (var b = Doc("房屋建筑物按现行重建成本测算。"))
            using (var service = new DesktopLocalAiService(() =>
            {
                factories++;
                throw new FileNotFoundException("sensitive document or local path");
            }))
            {
                var request = new ReportCheckRequest(a.FilePath, b.FilePath, null);
                Assert.IsTrue((await service.CheckAsync(request)).IsSuccess);
                var result = await service.CheckAsync(request);
                Assert.AreEqual(1, factories, "Failed runtime is not silently retried or replaced.");
                Assert.IsFalse(result.Issues.Any(i => i.Severity == IssueSeverity.Serious));
                Assert.Contains("AI 不可用", service.AiStatusMessage);
                Assert.DoesNotContain("sensitive", service.AiStatusMessage);
            }
        }

        [TestMethod]
        public async Task HardConflict_IsNotOverriddenOrSentToAi()
        {
            using (var a = Doc("房屋建筑物采用市场法。"))
            using (var b = Doc("房屋建筑物采用收益法。"))
            using (var service = new DesktopLocalAiService(() =>
                throw new AssertFailedException("Hard conflict must not initialize AI")))
            {
                var result = await service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                Assert.IsTrue(result.Issues.Any(i => i.Severity == IssueSeverity.Serious));
                Assert.Contains("未执行 AI 推理", service.AiStatusMessage);
            }
        }

        [TestMethod]
        public void RelativeModelPath_IsBasedOnExecutableNotWorkingDirectory()
        {
            Assert.AreEqual(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"),
                DesktopLocalAiService.ResolvePath("models"));
        }

        [TestMethod]
        public void MissingNativeLibrary_IsRejectedBeforeAnyOrtInitialization()
        {
            string absent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var error = Assert.ThrowsExactly<FileNotFoundException>(() =>
                LocalAiDependencyCheck.Verify(absent, Path.Combine(absent, "ortextensions.dll")));
            Assert.AreEqual("LocalAiDependencyMissing", error.Message);
        }

        [TestMethod]
        public void CorruptNativeLibrary_IsRejectedBeforeAnyOrtInitialization()
        {
            string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "onnxruntime.dll");
            try
            {
                File.WriteAllBytes(file, new byte[] { 0 });
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    LocalAiDependencyCheck.Verify(folder, Path.Combine(folder, "ortextensions.dll")));
            }
            finally { File.Delete(file); Directory.Delete(folder); }
        }

        private static SyntheticDocxFile Doc(string text)
            => SyntheticDocxFile.Create(new Paragraph(new Run(new Text(text))));

        private static string Key(ReportCheckIssue issue)
            => issue.FieldName + "|" + issue.IssueType + "|" + issue.Severity + "|" + issue.Summary;

        // Injection spy only; actual model acceptance is a separate local smoke, never this test.
        private sealed class TestRanker : ISemanticCandidateRanker, IDisposable
        {
            public int Calls { get; private set; }
            public bool ShadowOnly { get; private set; }
            public bool PromotionBlocked { get; private set; }
            public bool Disposed { get; private set; }
            public SemanticCandidateRankingResult Rank(SemanticCandidateRequest request,
                SemanticCandidateSelectionContext context)
            {
                Calls++;
                ShadowOnly = context.ShadowOnly;
                string candidate = request.AllowedNormalizedCandidates[0];
                var result = new SemanticCandidateRankingResult(request.RequestId,
                    request.CandidateSetIdentity, context, SemanticCandidateRankingStatus.Ranked,
                    new[] { new SemanticCandidateRankingItem(
                        new SemanticCandidateIdentity(request.CandidateSetIdentity, candidate),
                        candidate, 0.75, 1, 2.0, 1) },
                    "test-embedding", "test-revision", new string('a', 64),
                    "test-reranker", "test-revision", new string('b', 64), "TestOnly");
                PromotionBlocked = !result.FactPromotionAllowed;
                return result;
            }
            public void Dispose() { Disposed = true; }
        }
    }
}
