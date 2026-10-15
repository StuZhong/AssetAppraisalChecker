using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    [TestClass]
    public sealed class LocalModelManifestAndDatasetTests
    {
        [TestMethod]
        public void BgeManifest_IsPinnedLicensedAndPendingWithoutFabricatedHash()
        {
            LoadedLocalModelEvaluationManifest loaded = LoadManifest(
                LocalModelAcceptanceTestPaths.BgeManifest);

            Assert.AreEqual("BAAI/bge-small-zh-v1.5", loaded.Manifest.ModelId);
            Assert.AreEqual("BAAI/bge-small-zh-v1.5", loaded.Manifest.Repository);
            Assert.AreEqual(40, loaded.Manifest.IntendedRevision.Length);
            Assert.AreEqual("MIT", loaded.Manifest.License);
            Assert.AreEqual("Allowed", loaded.Manifest.CommercialUseStatus);
            Assert.AreEqual("Pending", loaded.Manifest.DownloadStatus);
            Assert.IsNull(loaded.Manifest.ExpectedModelHash);
            Assert.AreEqual(512, loaded.Manifest.MaximumInputLength);
            Assert.AreEqual(512, loaded.Manifest.EmbeddingDimension);
            Assert.IsTrue(LocalModelAcceptanceHash.IsSha256(loaded.ManifestSha256));
        }

        [TestMethod]
        public void E5Manifest_IsPinnedLicensedAndPendingWithoutFabricatedHash()
        {
            LoadedLocalModelEvaluationManifest loaded = LoadManifest(
                LocalModelAcceptanceTestPaths.E5Manifest);

            Assert.AreEqual("intfloat/multilingual-e5-small", loaded.Manifest.ModelId);
            Assert.AreEqual("intfloat/multilingual-e5-small", loaded.Manifest.Repository);
            Assert.AreEqual(40, loaded.Manifest.IntendedRevision.Length);
            Assert.AreEqual("MIT", loaded.Manifest.License);
            Assert.AreEqual("Allowed", loaded.Manifest.CommercialUseStatus);
            Assert.AreEqual("Pending", loaded.Manifest.DownloadStatus);
            Assert.IsNull(loaded.Manifest.ExpectedModelHash);
            Assert.AreEqual(512, loaded.Manifest.MaximumInputLength);
            Assert.AreEqual(384, loaded.Manifest.EmbeddingDimension);
            Assert.AreEqual("query: ", loaded.Manifest.QueryPrefixRequirement);
            Assert.AreEqual("passage: ", loaded.Manifest.PassagePrefixRequirement);
            Assert.IsTrue(LocalModelAcceptanceHash.IsSha256(loaded.ManifestSha256));
        }

        [TestMethod]
        public void Manifest_FloatingRevisionIsRejected()
        {
            string json = File.ReadAllText(LocalModelAcceptanceTestPaths.BgeManifest)
                .Replace(
                    "7999e1d3359715c523056ef9478215996d62a620",
                    "main");

            Assert.Throws<InvalidDataException>(
                () => new LocalModelEvaluationManifestLoader().Parse(json));
        }

        [TestMethod]
        public void Manifest_UnknownLicenseIsRejected()
        {
            string json = File.ReadAllText(LocalModelAcceptanceTestPaths.BgeManifest)
                .Replace("\"License\": \"MIT\"", "\"License\": \"Unknown\"");

            Assert.Throws<InvalidDataException>(
                () => new LocalModelEvaluationManifestLoader().Parse(json));
        }

        [TestMethod]
        public void Manifest_PendingModelRejectsFabricatedHash()
        {
            string fakeHash = new string('a', 64);
            string json = File.ReadAllText(LocalModelAcceptanceTestPaths.BgeManifest)
                .Replace(
                    "\"ExpectedModelHash\": null",
                    "\"ExpectedModelHash\": \"" + fakeHash + "\"");

            Assert.Throws<InvalidDataException>(
                () => new LocalModelEvaluationManifestLoader().Parse(json));
        }

        [TestMethod]
        public void Manifest_DownloadedModelRequiresLowercaseSha256()
        {
            string json = File.ReadAllText(LocalModelAcceptanceTestPaths.BgeManifest)
                .Replace("\"DownloadStatus\": \"Pending\"", "\"DownloadStatus\": \"Downloaded\"")
                .Replace("\"QuantizationStatus\": \"Pending\"", "\"QuantizationStatus\": \"Fp32\"")
                .Replace("\"ExpectedModelHash\": null", "\"ExpectedModelHash\": \"not-a-sha\"");

            Assert.Throws<InvalidDataException>(
                () => new LocalModelEvaluationManifestLoader().Parse(json));
        }

        [TestMethod]
        public void Dataset_IsSyntheticValidAndContainsAllRequiredCategories()
        {
            LoadedLocalSemanticAcceptanceDataset loaded = LoadDataset();

            Assert.AreEqual("SyntheticOnly", loaded.Dataset.DataClassification);
            Assert.AreEqual(23, loaded.Dataset.Cases.Length);
            Assert.AreEqual(6, CountCategory(loaded, "Positive"));
            Assert.AreEqual(7, CountCategory(loaded, "HardNegative"));
            Assert.AreEqual(5, CountCategory(loaded, "Abstention"));
            Assert.AreEqual(5, CountCategory(loaded, "Safety"));
            Assert.IsTrue(LocalModelAcceptanceHash.IsSha256(loaded.DatasetSha256));
        }

        [TestMethod]
        public void Dataset_CoversRequiredEvaluationMethodAndAssessmentObjectBoundaries()
        {
            LoadedLocalSemanticAcceptanceDataset loaded = LoadDataset();
            string allText = string.Join("\n", loaded.Dataset.Cases.Select(item => item.InputText));

            StringAssert.Contains(allText, "重建成本");
            StringAssert.Contains(allText, "公开成交案例");
            StringAssert.Contains(allText, "未来可获得收益折现");
            StringAssert.Contains(allText, "未采用收益法");
            StringAssert.Contains(allText, "不适用市场法");
            StringAssert.Contains(allText, "错误绑定到机器设备");
            StringAssert.Contains(allText, "虚构甲方");
            StringAssert.Contains(allText, "规则数量为六台");
            Assert.IsTrue(loaded.Dataset.Cases.Any(item =>
                item.SimulatedCandidates.Length > 1));
        }

        [TestMethod]
        public void Dataset_DuplicateCaseIdIsRejected()
        {
            string json = File.ReadAllText(LocalModelAcceptanceTestPaths.AcceptanceDataset)
                .Replace("SEM-EVM-002", "SEM-EVM-001");

            Assert.Throws<InvalidDataException>(
                () => new LocalSemanticAcceptanceDatasetLoader().Parse(json));
        }

        [TestMethod]
        public void Dataset_UnsupportedSchemaIsRejected()
        {
            string json = File.ReadAllText(LocalModelAcceptanceTestPaths.AcceptanceDataset)
                .Replace(
                    "local-semantic-acceptance-dataset/1.0",
                    "local-semantic-acceptance-dataset/2.0");

            Assert.Throws<InvalidDataException>(
                () => new LocalSemanticAcceptanceDatasetLoader().Parse(json));
        }

        [TestMethod]
        public void PrivacyValidator_RejectsSensitivePatternWithoutEchoingValue()
        {
            const string sensitiveValue = "13812345678";
            LocalSemanticAcceptanceCase item = LoadDataset().Dataset.Cases[0];
            item.InputText = "合成文本中不应出现号码" + sensitiveValue;

            LocalModelAcceptancePrivacyValidationResult result =
                new LocalModelAcceptancePrivacyValidator().Inspect(new[] { item });

            Assert.IsFalse(result.IsSafe);
            Assert.IsTrue(result.RedactedFindings.Any(finding =>
                finding.Contains("MobileNumber")));
            Assert.IsTrue(result.RedactedFindings.All(finding =>
                !finding.Contains(sensitiveValue)));
            Assert.IsTrue(result.RedactedFindings.All(finding =>
                finding.Contains("[REDACTED]")));
        }

        [TestMethod]
        public void PrivacyValidator_InspectsCandidateAndNotesFields()
        {
            LocalSemanticAcceptanceCase item = LoadDataset().Dataset.Cases[0];
            item.Notes = "禁止号码11010519491231002X";

            LocalModelAcceptancePrivacyValidationResult result =
                new LocalModelAcceptancePrivacyValidator().Inspect(new[] { item });

            Assert.IsFalse(result.IsSafe);
            StringAssert.Contains(result.RedactedFindings[0], "Field=Notes");
            Assert.IsFalse(result.RedactedFindings[0].Contains("11010519491231002X"));
        }

        private static LoadedLocalModelEvaluationManifest LoadManifest(string path)
        {
            return new LocalModelEvaluationManifestLoader().Load(path);
        }

        private static LoadedLocalSemanticAcceptanceDataset LoadDataset()
        {
            return new LocalSemanticAcceptanceDatasetLoader().Load(
                LocalModelAcceptanceTestPaths.AcceptanceDataset);
        }

        private static int CountCategory(
            LoadedLocalSemanticAcceptanceDataset loaded,
            string category)
        {
            return loaded.Dataset.Cases.Count(item => string.Equals(
                item.Category,
                category,
                StringComparison.Ordinal));
        }
    }
}
