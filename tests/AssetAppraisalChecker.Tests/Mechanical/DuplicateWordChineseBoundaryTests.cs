using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class DuplicateWordChineseBoundaryTests
    {
        [TestMethod]
        [DataRow("以财务报告为目的的评估指南")]
        [DataRow("项目勘察勘察费")]
        [DataRow("为明确目的的说明。")]
        [DataRow("河流流量测定结果。")]
        [DataRow("各国国家标准的适用范围。")]
        [DataRow("长期期权合同。")]
        [DataRow("工程设计设计费计算说明。")]
        [DataRow("项目咨询咨询费说明。")]
        [DataRow("本次资产资产管理制度。")]
        [DataRow("该价值价值观表达涉及不同概念。")]
        [DataRow("根据根据地情况编写说明。")]
        [DataRow("进行进行曲演奏。")]
        [DataRow("评估评估师的工作表现。")]
        [DataRow("法规清单：相关相关规定。")]
        [DataRow("依据《合成资产资产管理办法》执行。")]
        [DataRow("依据《评估评估报告规范》执行。")]
        [DataRow("项目名称：合成资产评估评估报告项目。")]
        [DataRow("合成评估评估有限公司提供资料。")]
        [DataRow("本次勘察勘察资料未能确认词语边界。")]
        public void AmbiguousOverlapOrProtectedName_DoesNotGenerateCandidate(string text)
        {
            using (var file = SyntheticDocxFile.Create(P(text)))
            {
                Assert.AreEqual(0, Words(file.FilePath).Length);
            }
        }

        [TestMethod]
        [DataRow("资产评估评估报告", "评估评估")]
        [DataRow("根据根据相关规定", "根据根据")]
        [DataRow("本项目采用评估评估方法。", "评估评估")]
        [DataRow("本次评估评估工作依据合成资料开展。", "评估评估")]
        [DataRow("评估评估价值为一百万元。", "评估评估")]
        [DataRow("本次评估评估开展工作。", "评估评估")]
        [DataRow("本次根据根据开展工作。", "根据根据")]
        [DataRow("本次采用采用市场法。", "采用采用")]
        [DataRow("本次涉及涉及的资产。", "涉及涉及")]
        [DataRow("本次进行进行核实。", "进行进行")]
        [DataRow("评估评估", "评估评估")]
        [DataRow("本次评估评估评估工作。", "评估评估评估")]
        public void ControlledRepeat_WithBoundaryEvidence_IsDetectedAndLocated(string text, string raw)
        {
            using (var file = SyntheticDocxFile.Create(P(text)))
            {
                MechanicalIssue issue = Words(file.FilePath).Single();
                MechanicalIssueOccurrence occurrence = issue.Occurrences.Single();
                Assert.AreEqual(raw, occurrence.RawText);
                Assert.AreEqual(0, occurrence.Location.ParagraphIndex);
                CollectionAssert.Contains(occurrence.Evidence.ToArray(), "文档字符偏移=" + text.IndexOf(raw, StringComparison.Ordinal));
                CollectionAssert.Contains(occurrence.Evidence.ToArray(), "长度=" + raw.Length);
                Assert.IsTrue(occurrence.Evidence.Any(item => item.StartsWith("词边界依据=", StringComparison.Ordinal)));
                StringAssert.Contains(issue.Reason, "受控词边界");
            }
        }

        [TestMethod]
        public void EarlierAmbiguousOrProtectedSubstring_DoesNotMaskLaterControlledRepeat()
        {
            using (var file = SyntheticDocxFile.Create(P("目的的说明，依据《合成资产资产规则》，本次评估评估工作。")))
            {
                Assert.AreEqual("评估评估", Words(file.FilePath).Single().Occurrences.Single().RawText);
            }
        }

        [TestMethod]
        [DataRow("以财务报告为目的的评估指南", false)]
        [DataRow("项目勘察勘察费", false)]
        [DataRow("资产评估评估报告", true)]
        [DataRow("根据根据相关规定", true)]
        public void EveryPossibleRunSplit_HasSameCandidatesAndOffsets(string text, bool detected)
        {
            for (int split = 1; split < text.Length; split++)
            {
                using (var file = SyntheticDocxFile.Create(new Paragraph(
                    new Run(new Text(text.Substring(0, split))),
                    new Run(new Text(text.Substring(split))))))
                {
                    var hits = Words(file.FilePath);
                    Assert.AreEqual(detected ? 1 : 0, hits.Length, "Run boundary must not determine word boundary.");
                    if (detected)
                    {
                        string raw = hits[0].Occurrences[0].RawText;
                        CollectionAssert.Contains(hits[0].Occurrences[0].Evidence.ToArray(),
                            "文档字符偏移=" + text.IndexOf(raw, StringComparison.Ordinal));
                    }
                }
            }
        }

        [TestMethod]
        public void ParagraphsAndCells_AreNotConcatenatedIntoRepeatedWords()
        {
            using (var file = SyntheticDocxFile.Create(P("本次评估"), P("评估报告。"),
                new Table(new TableRow(new TableCell(P("根据")), new TableCell(P("根据相关规定。"))))))
            {
                Assert.AreEqual(0, Words(file.FilePath).Length);
            }
        }

        [TestMethod]
        public async Task SingleAndDualProductionChain_PreservesSourceSpanPolicyAndHashes()
        {
            using (var a = SyntheticDocxFile.Create(P("以财务报告为目的的评估指南"), P("资产评估评估报告")))
            using (var b = SyntheticDocxFile.Create(P("项目勘察勘察费"),
                new Table(new TableRow(new TableCell(P("根据根据相关规定"))))))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var service = new ReportCheckService();
                foreach (var request in new[] { new ReportCheckRequest(a.FilePath, null),
                    new ReportCheckRequest(a.FilePath, b.FilePath, null),
                    new ReportCheckRequest(b.FilePath, a.FilePath, null) })
                {
                    ReportCheckResult result = await service.CheckAsync(request);
                    Assert.IsTrue(result.IsSuccess);
                    var hits = result.Issues.Where(item => item.GovernanceKind == "DuplicateWord").ToArray();
                    Assert.AreEqual(request.HasDocumentB ? 2 : 1, hits.Length);
                    foreach (ReportCheckIssue hit in hits)
                    {
                        var occurrence = hit.Occurrences.Single();
                        var source = SourceNavigationService.FindSource(result.SourceDocuments, occurrence);
                        Assert.IsNotNull(source);
                        var target = new SourceLocationResolver().Resolve(source, occurrence);
                        Assert.IsNotNull(target);
                        Assert.AreEqual(occurrence.RawText, target.SpanText);
                        Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(hit)),
                            "Existing controlled-positive policy must remain unchanged.");
                        string expectedLabel = occurrence.RawText == "评估评估"
                            ? (request.DocumentAPath == a.FilePath ? "文档 A" : "文档 B")
                            : (request.DocumentAPath == b.FilePath ? "文档 A" : "文档 B");
                        Assert.AreEqual(expectedLabel, occurrence.DocumentLabel);
                    }
                }
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        // Opt-in local validation only: no real document path/content is stored in the
        // repository, test discovery, output or diagnostics. Never edit an original.
        public static void AuditApprovedReports(string pathA, string pathB)
        {
            string hashA = Hash(pathA), hashB = Hash(pathB);
            var service = new ReportCheckService();
            var baseline = service.CheckAsync(new ReportCheckRequest(pathA, pathB, null)).GetAwaiter().GetResult();
            Assert.IsTrue(baseline.IsSuccess, "Approved pair must load successfully.");
            Console.WriteLine("Approved: DuplicateWord={0}; Focused={1}; Detail={2}",
                baseline.Issues.Count(item => item.GovernanceKind == "DuplicateWord"),
                baseline.Issues.Count(item => !IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(item))),
                baseline.Issues.Count);
            for (int index = 0; index < 2; index++)
            {
                string original = index == 0 ? pathA : pathB;
                string copy = Path.Combine(Path.GetTempPath(), "AssetAppraisalChecker-DuplicateMutation-" + Guid.NewGuid().ToString("N") + ".docx");
                File.Copy(original, copy, false);
                try
                {
                    int start;
                    using (var package = WordprocessingDocument.Open(copy, true))
                    {
                        Body body = package.MainDocumentPart.Document.Body;
                        start = body.Elements<Paragraph>().Count();
                        foreach (string text in new[] { "资产评估评估报告", "根据根据相关规定", "项目勘察勘察费", "以核实范围为目的的说明" })
                            body.InsertBefore(P(text), body.GetFirstChild<SectionProperties>());
                        package.MainDocumentPart.Document.Save();
                    }
                    string copyHash = Hash(copy);
                    var result = service.CheckAsync(new ReportCheckRequest(index == 0 ? copy : pathA,
                        index == 1 ? copy : pathB, null)).GetAwaiter().GetResult();
                    Assert.IsTrue(result.IsSuccess);
                    var hits = result.Issues.Where(item => item.GovernanceKind == "DuplicateWord").ToArray();
                    Assert.AreEqual(2, hits.Length, "Only two injected controlled repeats may be reported.");
                    foreach (int offset in new[] { 0, 1 })
                    {
                        var hit = hits.Single(item => item.Occurrences.Any(o => o.Location.ParagraphIndex == start + offset &&
                            o.DocumentLabel == (index == 0 ? "文档 A" : "文档 B")));
                        var occurrence = hit.Occurrences.Single();
                        var source = SourceNavigationService.FindSource(result.SourceDocuments, occurrence);
                        Assert.AreEqual(occurrence.RawText, new SourceLocationResolver().Resolve(source, occurrence).SpanText);
                    }
                    Assert.AreEqual(copyHash, Hash(copy));
                    Console.WriteLine("Mutation{0}: Injected=2; Detected=2; Located=2; LegalControls=2; LegalFalsePositives=0; HashUnchanged=True", index == 0 ? "A" : "B");
                }
                finally { File.Delete(copy); }
            }
            Assert.AreEqual(hashA, Hash(pathA));
            Assert.AreEqual(hashB, Hash(pathB));
            Console.WriteLine("OriginalAHashUnchanged=True; OriginalBHashUnchanged=True; MutationCopiesRemoved=True");
        }

        private static MechanicalIssue[] Words(string path) => new DuplicateContentChecker()
            .Check(new MechanicalCheckContext(path, new DocxDocumentReader().Read(path), "文档 A"))
            .Where(item => item.Kind == MechanicalIssueKind.DuplicateWord).ToArray();
        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
