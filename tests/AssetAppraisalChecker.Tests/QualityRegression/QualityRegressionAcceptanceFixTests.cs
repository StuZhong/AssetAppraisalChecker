using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.QualityRegression
{
    [TestClass]
    public sealed class QualityRegressionAcceptanceFixTests
    {
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TabEvidence_PreservesNativeAndLiteralTabs_ParagraphRunAndOffsets(bool nativeTab)
        {
            OpenXmlElement tab = nativeTab ? (OpenXmlElement)new TabChar() : new Text("\t");
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Paragraph("其他正常合成正文。"),
                new Paragraph(new Run(new Text("合成定位")),
                    new Run(new Text("前缀"), tab, new Text("结束。")))))
            {
                ReportCheckResult result = await CheckReadOnly(file);
                ReportCheckOccurrence occurrence = result.Issues.Single(item => item.GovernanceKind == "Tab")
                    .Occurrences.Single();
                Assert.AreEqual(1, occurrence.Location.ParagraphIndex);
                StringAssert.Contains(occurrence.RawText, "\t");
                string evidence = string.Join(";", occurrence.Evidence);
                StringAssert.Contains(evidence, "Unicode=U+0009");
                StringAssert.Contains(evidence, "元素内字符偏移（从 0 开始）=6");
                StringAssert.Contains(evidence, "段内字符偏移=6");
                StringAssert.Contains(evidence, "RunIndex=1；RunOffset=2");
                StringAssert.Contains(evidence, "/p[2]/r[2]/" + (nativeTab ? "tab[1]" : "t[2]"));
            }
        }

        [TestMethod]
        public async Task TabEvidence_TableCellRetainsCellAndParagraphOffsets()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(new TableRow(new TableCell(Paragraph("前段。"),
                    new Paragraph(new Run(new Text("本段"), new TabChar(), new Text("结束。"))))))))
            {
                ReportCheckOccurrence occurrence = (await CheckReadOnly(file)).Issues
                    .Single(item => item.GovernanceKind == "Tab").Occurrences.Single();
                Assert.AreEqual(0, occurrence.Location.TableIndex);
                Assert.AreEqual(0, occurrence.Location.RowIndex);
                Assert.AreEqual(0, occurrence.Location.CellIndex);
                string evidence = string.Join(";", occurrence.Evidence);
                StringAssert.Contains(evidence, "元素内字符偏移（从 0 开始）=6");
                StringAssert.Contains(evidence, "段内字符偏移=2");
                StringAssert.Contains(evidence, "/tbl[1]/tr[1]/tc[1]/p[2]/r[1]/tab[1]");
            }
        }

        [TestMethod]
        [DataRow("MixedParagraph")]
        [DataRow("DeletedOnlyParagraph")]
        [DataRow("TableCell")]
        public async Task DeletedRevisionEvidence_UsesAncestryNotVisibleText(string container)
        {
            const string raw = "被删除的合成片段";
            var revision = new DeletedRun(new Run(new DeletedText(raw))) { Id = "1", Author = "合成测试" };
            var target = new Paragraph();
            if (container != "DeletedOnlyParagraph") target.Append(new Run(new Text("相同合成正文。")));
            target.Append(revision);
            OpenXmlElement[] body = container == "TableCell" ? new OpenXmlElement[]
            {
                new Table(new TableRow(new TableCell(Paragraph("相同合成正文。")), new TableCell(target)))
            } : new OpenXmlElement[] { Paragraph("相同合成正文。"), target };
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(body))
            {
                ReportCheckOccurrence occurrence = (await CheckReadOnly(file)).Issues
                    .Single(item => item.GovernanceKind == "DeletedRevision").Occurrences.Single();
                Assert.AreEqual(raw, occurrence.RawText);
                if (container == "TableCell")
                {
                    Assert.AreEqual(0, occurrence.Location.TableIndex);
                    Assert.AreEqual(0, occurrence.Location.RowIndex);
                    Assert.AreEqual(1, occurrence.Location.CellIndex);
                }
                else Assert.AreEqual(1, occurrence.Location.ParagraphIndex);
                string evidence = string.Join(";", occurrence.Evidence);
                StringAssert.Contains(evidence, "修订类型=DeletedRevision");
                StringAssert.Contains(evidence, "Open XML 元素=del");
                StringAssert.Contains(evidence, container == "TableCell" ? "/tc[2]/p[1]/del[1]" : "/p[2]/del[1]");
                Assert.IsFalse(evidence.Contains("未能可靠定位"));
            }
        }

        [TestMethod]
        [DataRow("本次参考《合成资料清单》开展核对。")]
        [DataRow("本次整理资料清单供人工核对。")]
        [DataRow("本次评估根据现场勘查以及合成资料形成结论。")]
        [DataRow("本次评估根据现场勘查开展核对。")]
        public async Task LegalCompoundAndPredicate_HaveNoQualityPrompt(string text)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(Paragraph(text)))
            {
                ReportCheckResult result = await CheckReadOnly(file);
                Assert.AreEqual(0, result.Issues.Count(item => item.Category != IssueCategory.Business), Summary(result));
            }
        }

        [TestMethod]
        [DataRow("本次核对资产青单。", "资产清单")]
        [DataRow("本次核对评估依剧。", "评估依据")]
        [DataRow("本次核对资产评估报吿。", "资产评估报告")]
        public async Task GenuineTermErrors_AreStillDetectedOnce(string text, string suggested)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(Paragraph(text)))
            {
                ReportCheckResult result = await CheckReadOnly(file);
                ReportCheckIssue[] language = result.Issues.Where(item => item.Category == IssueCategory.Language).ToArray();
                Assert.AreEqual(1, language.Length, Summary(result));
                Assert.AreEqual(IssueAuthority.ReviewSuggested, language.Single().Authority);
                Assert.IsTrue(language.Single().Occurrences.SelectMany(item => item.Evidence)
                    .Any(item => item.Contains(suggested)), Summary(result));
            }
        }

        [TestMethod]
        [DataRow("QC-001")]
        [DataRow("QC-003")]
        [DataRow("QC-014")]
        public async Task SameRoot_IsNotDuplicatedAcrossMechanicalSpellAndGrammar(string caseId)
        {
            QualityRegressionCase sample = QualityErrorRegressionCorpus.Find(caseId);
            using (SyntheticDocxFile file = sample.Create(true))
            {
                ReportCheckResult result = await CheckReadOnly(file);
                ReportCheckIssue[] quality = result.Issues.Where(item => item.Category != IssueCategory.Business).ToArray();
                Assert.AreEqual(1, quality.Length, Summary(result));
                Assert.AreEqual(sample.GovernanceKind, quality.Single().GovernanceKind);
            }
        }

        private static Paragraph Paragraph(string text) => new Paragraph(new Run(new Text(text)));

        private static async Task<ReportCheckResult> CheckReadOnly(SyntheticDocxFile file)
        {
            string before = Hash(file.FilePath);
            ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(before, Hash(file.FilePath), "Input DOCX changed.");
            return result;
        }

        private static string Summary(ReportCheckResult result) => string.Join(";", result.Issues
            .Select(item => item.GovernanceKind + ":" + string.Join("/", item.Occurrences.Select(o => o.RawText))));

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
