using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using AssetAppraisalChecker.TextQuality;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.TextQuality
{
    [TestClass]
    public sealed class TextQualitySpellCheckerTests
    {
        private readonly SpellChecker _checker = new SpellChecker();

        [TestMethod]
        [DataRow("资产评估")]
        [DataRow("评估基准日")]
        [DataRow("重置成本法")]
        [DataRow("房屋建筑物")]
        [DataRow("土地使用权")]
        [DataRow("产权持有人")]
        [DataRow("不含税评估值")]
        public void ProfessionalTerms_DoNotReport(string term)
        {
            Assert.AreEqual(0, Check(term).Candidates.Count);
        }

        [TestMethod]
        public void DefaultLexicon_HasFrozenVersionAndExpectedTerms()
        {
            Assert.AreEqual("v2-text-quality-professional/2.0", _checker.Lexicon.Version);
            Assert.AreEqual(106, _checker.Lexicon.Terms.Count);
            CollectionAssert.Contains(_checker.Lexicon.Terms.ToList(), "评估基准日");
        }

        [TestMethod]
        public void KnownTypo_ProducesReviewCandidate()
        {
            TypoCandidate candidate = Check("本资产评估报吿仅供人工复核。")
                .Candidates.Single();

            Assert.AreEqual(TypoCandidateKind.KnownTypo, candidate.Kind);
            Assert.AreEqual("资产评估报吿", candidate.OriginalText);
            Assert.AreEqual("资产评估报告", candidate.SuggestedTerm);
            Assert.AreEqual("文档 A", candidate.SourceDocument);
        }

        [TestMethod]
        public void UniqueNearMatch_ProducesCandidate()
        {
            SpellCheckResult result = Check("本项目评估基准曰为合成日期。");
            Assert.AreEqual(1, result.Candidates.Count, CandidateSummary(result));
            TypoCandidate candidate = result.Candidates.Single();

            Assert.AreEqual(TypoCandidateKind.ConfusableProfessionalTerm, candidate.Kind);
            Assert.AreEqual("评估基准曰", candidate.OriginalText);
            Assert.AreEqual("评估基准日", candidate.SuggestedTerm);
        }

        [TestMethod]
        public void ShortTermFuzzyMatch_IsNotReported()
        {
            Assert.AreEqual(0, Check("本项目采用成木法。") .Candidates.Count);
        }

        [TestMethod]
        public void TiedNearMatches_AreNotReported()
        {
            var checker = new SpellChecker(
                new ProfessionalLexicon("test/1.0", new[] { "评估基准日", "评估基准月" }),
                new KnownTypoRule[0]);

            Assert.AreEqual(0, checker.Check(Document("评估基准曰"), "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void MoreThanOneEdit_IsNotReported()
        {
            SpellCheckResult result = Check("本项目评估基淮曰为合成日期。");
            Assert.AreEqual(0, result.Candidates.Count, CandidateSummary(result));
        }

        [TestMethod]
        public void CandidateDoesNotCrossPunctuation()
        {
            Assert.AreEqual(0, Check("评估基准、曰") .Candidates.Count);
        }

        [TestMethod]
        [DataRow("项目名称：评估基准曰示范项目")]
        [DataRow("委托人：评估基准曰有限公司")]
        [DataRow("设备型号：BGE-2026-A01")]
        [DataRow("报告编号：云测评报字〔2026〕第001号")]
        [DataRow("日期为2026年6月15日，金额为998.22万元，占比12.5%。")]
        [DataRow("请访问https://example.invalid或发送邮件至qa@example.invalid。")]
        public void ProtectedTerms_AreNotReported(string text)
        {
            Assert.AreEqual(0, Check(text).Candidates.Count);
        }

        [TestMethod]
        public void TraditionalAndRareCharacters_DoNotCreateUniversalCorrection()
        {
            Assert.AreEqual(0, Check("該項資料僅作合成示例，𠮷字不作通用纠错。") .Candidates.Count);
        }

        [TestMethod]
        public void MechanicalRoots_AreNotDuplicatedAsLanguageIssues()
        {
            Assert.AreEqual(0, Check("评估评估采用市场法。。") .Candidates.Count);
        }

        [TestMethod]
        public void TableCellCandidate_PreservesStructuredLocation()
        {
            DocumentElement element = Element(
                "本项目评估基准曰为合成日期。",
                0,
                DocumentElementType.TableCell,
                new DocumentLocation(DocumentPart.MainBody, null, 2, 3, 4, new string[0]));

            SpellCheckResult result = _checker.Check(
                DocumentReadResult.Succeeded(new[] { element }, 0, 1),
                "文档 B");
            Assert.AreEqual(1, result.Candidates.Count, CandidateSummary(result));
            TypoCandidate candidate = result.Candidates.Single();

            Assert.AreEqual(2, candidate.Location.TableIndex);
            Assert.AreEqual(3, candidate.Location.RowIndex);
            Assert.AreEqual(4, candidate.Location.CellIndex);
            Assert.AreEqual("文档 B", candidate.SourceDocument);
        }

        [TestMethod]
        public void RepresentativeProfessionalTextAndTable_HaveNoFalsePositive()
        {
            const string text =
                "资产评估机构接受委托人的委托，对产权持有人申报的房屋建筑物、机器设备和土地使用权进行评估。" +
                "评估对象、评估范围、评估目的、经济行为和评估基准日均已明确。" +
                "评估人员采用资产基础法、市场法、收益法、成本法和重置成本法形成评估结论。" +
                "项目所在地为云南省昆明市，报告编号为云测评报字〔2026〕第001号，评估价值为998.22万元，占比80%。" +
                "设备型号为ABB-XJ2026A，联系邮箱为qa@example.invalid。";
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[]
                {
                    Element(text, 0),
                    Element(
                        "资产名称：合成机器设备；数量：6台；账面价值：8000元/台。",
                        1,
                        DocumentElementType.TableCell,
                        new DocumentLocation(DocumentPart.MainBody, null, 0, 1, 2, new string[0]))
                },
                1,
                1);

            Assert.AreEqual(0, _checker.Check(document, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public async Task DesktopWorkflow_KnownTypoMapsLanguageGovernance()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("本资产评估报吿仅供人工复核。")))))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);
                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.Category == IssueCategory.Language);

                Assert.AreEqual(IssueAuthority.ReviewSuggested, issue.Authority);
                Assert.AreEqual("文字质量", issue.IssueTypeDisplayOverride);
                Assert.AreEqual("文档 A", issue.Occurrences.Single().DocumentLabel);
                Assert.AreEqual("资产评估报告", issue.ExpectedValue);
                Assert.AreEqual("发现疑似用词异常", issue.Summary);
            }
        }

        [TestMethod]
        public async Task DesktopWorkflow_DualDocuments_ProducesIndependentStableIssues()
        {
            using (SyntheticDocxFile documentA = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("文档A评估基准曰为合成日期。")))))
            using (SyntheticDocxFile documentB = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("文档B评估基准曰为合成日期。")))))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null));
                List<ReportCheckIssue> languageIssues = result.Issues
                    .Where(item => item.Category == IssueCategory.Language)
                    .ToList();

                Assert.AreEqual(2, languageIssues.Count);
                Assert.AreEqual(2, languageIssues.Select(item => item.IssueId).Distinct().Count());
                CollectionAssert.AreEquivalent(
                    new[] { "文档 A", "文档 B" },
                    languageIssues.Select(item => item.Occurrences.Single().DocumentLabel).ToArray());
            }
        }

        [TestMethod]
        public async Task DesktopWorkflow_KeepsMechanicalIssuesWithoutLanguageNoise()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("本项目采用评估评估方法。。")))))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);

                Assert.AreEqual(0, result.Issues.Count(item => item.Category == IssueCategory.Language));
                Assert.IsTrue(result.Issues.Any(item => item.Summary.Contains("重复词语")));
                Assert.IsTrue(result.Issues.Any(item => item.Summary.Contains("重复标点")));
            }
        }

        [TestMethod]
        public async Task DocxCheck_DoesNotModifyInput()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("本项目评估基准曰为合成日期。")))))
            {
                string before = Hash(file.FilePath);
                await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        [DataRow("EvaluationMethod", "V2-GT-501")]
        [DataRow("Integrated", "V2-GT-901")]
        public void GoldenSmoke_HasNoLanguageFalsePositiveAndPreservesFile(
            string group,
            string caseId)
        {
            string path = Path.Combine(
                FindRepositoryRoot(),
                "tests",
                "AssetAppraisalChecker.Tests",
                "GoldenCases",
                "V2",
                group,
                caseId,
                "Report.docx");
            string before = Hash(path);
            DocumentReadResult document = new DocxDocumentReader().Read(path);

            Assert.IsTrue(document.IsSuccess, document.Message);
            Assert.AreEqual(0, _checker.Check(document, "文档 A").Candidates.Count);
            Assert.AreEqual(before, Hash(path));
        }

        private SpellCheckResult Check(string text)
        {
            return _checker.Check(Document(text), "文档 A");
        }

        private static string CandidateSummary(SpellCheckResult result)
        {
            return string.Join(
                " | ",
                result.Candidates.Select(item =>
                    item.OriginalText + " -> " + item.SuggestedTerm + " (" + item.Kind + ")"));
        }

        private static DocumentReadResult Document(params string[] texts)
        {
            List<DocumentElement> elements = texts
                .Select((text, index) => Element(text, index))
                .ToList();
            return DocumentReadResult.Succeeded(elements, elements.Count, 0);
        }

        private static DocumentElement Element(
            string text,
            int order,
            DocumentElementType type = DocumentElementType.Paragraph,
            DocumentLocation location = null)
        {
            return new DocumentElement(
                type,
                text,
                location ?? new DocumentLocation(
                    DocumentPart.MainBody,
                    order,
                    null,
                    null,
                    null,
                    new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "AssetAppraisalChecker.sln")))
            {
                current = current.Parent;
            }

            Assert.IsNotNull(current, "无法定位仓库根目录。");
            return current.FullName;
        }
    }
}
