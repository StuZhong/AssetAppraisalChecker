using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class V2GoldenCasePrivacyValidatorTests
    {
        [TestMethod]
        public void Inspect_AcceptsSyntheticBusinessTextWithoutSensitivePatterns()
        {
            using (TemporaryDocxFile file = TemporaryDocxFile.Create(
                "合成资产评估报告：评估价值为998.22万元；评估基准日为2026年7月31日；报告编号第901号。"))
            {
                V2GoldenCasePrivacyScanResult result = Inspect(file.Path);

                Assert.IsTrue(
                    result.IsSafe,
                    string.Join(Environment.NewLine, result.Findings.Select(item => item.ToString())));
                Assert.AreEqual(V2GoldenCasePrivacyValidator.PolicyVersion, result.PolicyVersion);
                StringAssert.Contains(
                    V2GoldenCasePrivacyValidator.ScopeDisclaimer,
                    "does not prove");
            }
        }

        [TestMethod]
        [DataRow("PRIV-CN-ID", "11010519491231002X")]
        [DataRow("PRIV-CN-MOBILE", "138-0013-8000")]
        [DataRow("PRIV-USCC", "91350211M000100Y46")]
        [DataRow("PRIV-BANK-CARD", "4111 1111 1111 1111")]
        [DataRow("PRIV-BANK-ACCOUNT", "银行账号：6222 0202 0202 0202 020")]
        [DataRow("PRIV-KNOWN-BUSINESS", "AssetAppraisalChecker-Prohibited-Business-Identifier")]
        public void Inspect_RejectsConfiguredSensitivePatterns(
            string expectedRuleId,
            string sensitiveText)
        {
            using (TemporaryDocxFile file = TemporaryDocxFile.Create(sensitiveText))
            {
                V2GoldenCasePrivacyScanResult result = Inspect(file.Path);

                Assert.IsFalse(result.IsSafe);
                Assert.IsTrue(result.Findings.Any(item => item.RuleId == expectedRuleId));
            }
        }

        [TestMethod]
        [DataRow("word/header1.xml")]
        [DataRow("word/footer1.xml")]
        [DataRow("word/comments.xml")]
        [DataRow("docProps/core.xml")]
        [DataRow("customXml/item1.xml")]
        public void Inspect_ScansTextAcrossRequiredOoxmlPartScope(string partName)
        {
            var extraParts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { partName, XmlWithText("13800138000") }
            };
            using (TemporaryDocxFile file = TemporaryDocxFile.Create("合成正文", extraParts))
            {
                V2GoldenCasePrivacyScanResult result = Inspect(file.Path);

                Assert.IsTrue(result.Findings.Any(item =>
                    item.RuleId == "PRIV-CN-MOBILE"
                    && item.PartName == partName));
            }
        }

        [TestMethod]
        public void Inspect_RejectsUnapprovedAuthorMetadata()
        {
            var extraParts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "docProps/core.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                    + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" "
                    + "xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
                    + "<dc:creator>Unapproved Analyst</dc:creator></cp:coreProperties>"
                }
            };
            using (TemporaryDocxFile file = TemporaryDocxFile.Create("合成正文", extraParts))
            {
                V2GoldenCasePrivacyScanResult result = Inspect(file.Path);

                Assert.IsTrue(result.Findings.Any(item => item.RuleId == "PRIV-METADATA"));
            }
        }

        [TestMethod]
        public void Inspect_RejectsUnscannableMediaWithoutOcr()
        {
            var binaryParts = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                { "word/media/image1.png", new byte[] { 137, 80, 78, 71 } }
            };
            using (TemporaryDocxFile file = TemporaryDocxFile.Create(
                "合成正文",
                null,
                binaryParts))
            {
                V2GoldenCasePrivacyScanResult result = Inspect(file.Path);

                Assert.IsTrue(result.Findings.Any(item =>
                    item.RuleId == "PRIV-UNSCANNABLE-PART"
                    && item.PartName == "word/media/image1.png"));
            }
        }

        [TestMethod]
        public void Inspect_DiagnosticsMaskSensitiveValues()
        {
            const string sensitiveId = "11010519491231002X";
            using (TemporaryDocxFile file = TemporaryDocxFile.Create(sensitiveId))
            {
                V2GoldenCasePrivacyScanResult result = Inspect(file.Path);
                string diagnostic = string.Join(
                    Environment.NewLine,
                    result.Findings.Select(item => item.ToString()));

                Assert.IsFalse(diagnostic.Contains(sensitiveId));
                StringAssert.Contains(diagnostic, "110*************2X");
            }
        }

        [TestMethod]
        public void Runner_PrivacyFailureStopsBeforeDocumentAndPipeline()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AssetAppraisalChecker-GoldenPrivacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string report = Path.Combine(root, "Report.docx");
                string explanation = Path.Combine(root, "Explanation.docx");
                TemporaryDocxFile.CreateAt(report, "敏感候选：13800138000");
                TemporaryDocxFile.CreateAt(explanation, "合成正文");
                File.Copy(GetExpectedPath("V2-GT-101"), Path.Combine(root, "Expected.json"));
                string manifest =
                    "{\"SchemaVersion\":\"v2-golden-manifest/1.0\",\"Cases\":[{"
                    + "\"Order\":1,\"CaseId\":\"V2-GT-101\",\"ExpectedFile\":\"Expected.json\",\"Inputs\":["
                    + InputJson("report", "Report", "Report.docx", V2GoldenCaseSha256.Compute(report)) + ","
                    + InputJson("explanation", "Explanation", "Explanation.docx", V2GoldenCaseSha256.Compute(explanation))
                    + "]}]}";
                string manifestPath = Path.Combine(root, "Manifest.json");
                File.WriteAllText(manifestPath, manifest, Encoding.UTF8);

                V2GoldenCaseExecutionResult result = new V2GoldenCaseRunner().Run(
                    manifestPath,
                    "V2-GT-101");

                Assert.IsFalse(result.IsSuccess);
                Assert.IsNull(result.AnalysisResult);
                Assert.IsTrue(result.Failures.Any(item =>
                    item.Stage == "Privacy[report]"
                    && item.Message.Contains("PRIV-CN-MOBILE")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static V2GoldenCasePrivacyScanResult Inspect(string path)
        {
            return new V2GoldenCasePrivacyValidator().Inspect(
                "synthetic-document",
                path,
                V2GoldenCaseSha256.Compute(path));
        }

        private static string InputJson(
            string documentId,
            string role,
            string file,
            string sha256)
        {
            return "{\"DocumentId\":\"" + documentId
                + "\",\"Role\":\"" + role
                + "\",\"File\":\"" + file
                + "\",\"Sha256\":\"" + sha256 + "\"}";
        }

        private static string GetExpectedPath(string caseId)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "GoldenCases",
                "V2",
                "Runner",
                caseId,
                "Expected.json");
        }

        private static string XmlWithText(string text)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><root><text>"
                + SecurityElement.Escape(text)
                + "</text></root>";
        }

        private sealed class TemporaryDocxFile : IDisposable
        {
            private TemporaryDocxFile(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public static TemporaryDocxFile Create(
                string documentText,
                IDictionary<string, string> extraXmlParts = null,
                IDictionary<string, byte[]> extraBinaryParts = null)
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "AssetAppraisalChecker-Privacy-" + Guid.NewGuid().ToString("N") + ".docx");
                CreateAt(path, documentText, extraXmlParts, extraBinaryParts);
                return new TemporaryDocxFile(path);
            }

            public static void CreateAt(
                string path,
                string documentText,
                IDictionary<string, string> extraXmlParts = null,
                IDictionary<string, byte[]> extraBinaryParts = null)
            {
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
                {
                    AddTextEntry(
                        archive,
                        "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                        + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                        + "</Types>");
                    AddTextEntry(
                        archive,
                        "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>"
                        + "</Relationships>");
                    AddTextEntry(
                        archive,
                        "word/document.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                        + "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
                        + "<w:body><w:p><w:r><w:t>"
                        + SecurityElement.Escape(documentText)
                        + "</w:t></w:r></w:p></w:body></w:document>");

                    foreach (KeyValuePair<string, string> part
                        in extraXmlParts ?? new Dictionary<string, string>())
                    {
                        AddTextEntry(archive, part.Key, part.Value);
                    }

                    foreach (KeyValuePair<string, byte[]> part
                        in extraBinaryParts ?? new Dictionary<string, byte[]>())
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(
                            part.Key,
                            CompressionLevel.Optimal);
                        using (Stream partStream = entry.Open())
                        {
                            partStream.Write(part.Value, 0, part.Value.Length);
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }

            private static void AddTextEntry(
                ZipArchive archive,
                string path,
                string content)
            {
                ZipArchiveEntry entry = archive.CreateEntry(
                    path,
                    CompressionLevel.Optimal);
                using (var writer = new StreamWriter(
                    entry.Open(),
                    new UTF8Encoding(false)))
                {
                    writer.Write(content);
                }
            }
        }
    }
}
