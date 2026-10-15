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
    public sealed class TextQualitySpellCheckerPhaseBTests
    {
        private readonly SpellChecker _checker = new SpellChecker();

        [TestMethod]
        public void Lexicon_IsVersionedCategorizedAndControlled()
        {
            Assert.AreEqual("v2-text-quality-professional/2.0", _checker.Lexicon.Version);
            Assert.AreEqual(106, _checker.Lexicon.Terms.Count);
            Assert.IsTrue(_checker.Lexicon.Entries
                .Select(item => item.Category)
                .Distinct()
                .Count() == 8);
            Assert.AreEqual(
                ProfessionalTermCategory.ReportField,
                _checker.Lexicon.GetCategory("评估基准日"));
            Assert.AreEqual(
                ProfessionalTermCategory.ValuationMethod,
                _checker.Lexicon.GetCategory("重置成本法"));
        }

        [TestMethod]
        [DataRow("评估基准日")]
        [DataRow("重置成本法")]
        [DataRow("房屋建筑物")]
        [DataRow("账面价值")]
        [DataRow("评估结果表")]
        public void ExpandedLexicon_FormalTermsDoNotReport(string term)
        {
            Assert.AreEqual(0, Check(term).Candidates.Count);
        }

        [TestMethod]
        [DataRow("资产评估报吿", "资产评估报告", "KT-REPORT-001")]
        [DataRow("评估结伦", "评估结论", "KT-CONCLUSION-001")]
        [DataRow("评估范国", "评估范围", "KT-SCOPE-001")]
        public void KnownTypoRules_ReportExactlyOneReviewCandidate(
            string wrong,
            string suggested,
            string ruleId)
        {
            SpellCheckResult result = Check(wrong);
            TypoCandidate candidate = result.Candidates.Single();

            Assert.AreEqual(TypoCandidateKind.KnownTypo, candidate.Kind);
            Assert.AreEqual(suggested, candidate.SuggestedTerm);
            Assert.IsTrue(candidate.Evidence.Any(item => item.Contains(ruleId)));
            CollectionAssert.Contains(candidate.Reasons.ToList(), TypoEvidenceReason.KnownTypo);
        }

        [TestMethod]
        [DataRow("资产评估报告")]
        [DataRow("评估结论")]
        [DataRow("评估范围")]
        public void KnownTypoRules_CorrectFormsDoNotReport(string correct)
        {
            Assert.AreEqual(0, Check(correct).Candidates.Count);
        }

        [TestMethod]
        [DataRow("云南资产评估报吿科技有限公司")]
        [DataRow("项目名称：评估结伦合成项目")]
        [DataRow("资产名称：评估范国专用装置")]
        public void KnownTypoRules_ProtectedTermsTakePriority(string protectedText)
        {
            Assert.AreEqual(0, Check(protectedText).Candidates.Count);
        }

        [TestMethod]
        public void KnownTypo_TakesPriorityOverNearMatch()
        {
            TypoCandidate candidate = Check("评估结伦").Candidates.Single();

            Assert.AreEqual(TypoCandidateKind.KnownTypo, candidate.Kind);
            Assert.AreEqual(1, candidate.Reasons.Count);
        }

        [TestMethod]
        public void ConfusableProfessionalTerm_ReportsControlledCandidate()
        {
            TypoCandidate candidate = Check("本项目评估基准曰为合成日期。")
                .Candidates.Single();

            Assert.AreEqual(TypoCandidateKind.ConfusableProfessionalTerm, candidate.Kind);
            Assert.AreEqual("评估基准日", candidate.SuggestedTerm);
            CollectionAssert.Contains(
                candidate.Reasons.ToList(),
                TypoEvidenceReason.ConfusableProfessionalTerm);
        }

        [TestMethod]
        [DataRow("孔子曰学而时习之。")]
        [DataRow("合同签署曰由双方另行约定。")]
        public void ConfusableCharacter_InOrdinaryTextDoesNotReport(string text)
        {
            Assert.AreEqual(0, Check(text).Candidates.Count);
        }

        [TestMethod]
        public void ConfusableCharacter_InProtectedNameDoesNotReport()
        {
            Assert.AreEqual(0, Check("项目名称：评估基准曰示范项目").Candidates.Count);
        }

        [TestMethod]
        public void ConfusableCharacter_WithTwoLexiconCandidatesDoesNotReport()
        {
            var checker = new SpellChecker(
                new ProfessionalLexicon(
                    "test/2.0",
                    new[] { "评估基准日", "评估基准目" }),
                new KnownTypoRule[0],
                ProfessionalConfusableSet.Default);

            Assert.AreEqual(0, checker.Check(Document("评估基准曰"), "文档 A").Candidates.Count);
        }

        [TestMethod]
        [DataRow("房屋建筑筑物", "房屋建筑物")]
        public void NearMatch_SupportsEvidencedInternalRepeatedInsertion(
            string observed,
            string expected)
        {
            SpellCheckResult result = Check(observed);
            Assert.AreEqual(1, result.Candidates.Count, CandidateSummary(result));
            TypoCandidate candidate = result.Candidates.Single();

            Assert.AreEqual(TypoCandidateKind.ProfessionalTermNearMatch, candidate.Kind);
            Assert.AreEqual(expected, candidate.SuggestedTerm);
        }

        [TestMethod]
        public void NearMatch_MissingCharacterWithoutErrorEvidence_Abstains()
        {
            // Previously inferred a missing character solely from dictionary distance.
            // A closed fragment does not prove that an unfamiliar expression is wrong.
            Assert.AreEqual(0, Check("房屋建物").Candidates.Count);
        }

        [TestMethod]
        public void DocumentConsistencyEvidence_IsAddedFromSameDocumentOnly()
        {
            var texts = Enumerable.Repeat("评估基准日", 10)
                .Concat(new[] { "评估基准曰" })
                .ToArray();
            TypoCandidate candidate = _checker.Check(Document(texts), "文档 A")
                .Candidates.Single();

            CollectionAssert.Contains(
                candidate.Reasons.ToList(),
                TypoEvidenceReason.DocumentConsistencySupported);
            Assert.IsTrue(candidate.Evidence.Any(item => item.Contains("另出现 10 次")));
        }

        [TestMethod]
        public void NearMatchWithoutExactTerm_HasNoConsistencyEvidence()
        {
            TypoCandidate candidate = Check("评估基准曰").Candidates.Single();

            CollectionAssert.DoesNotContain(
                candidate.Reasons.ToList(),
                TypoEvidenceReason.DocumentConsistencySupported);
        }

        [TestMethod]
        public void CompanyFrequency_IsNeverUsedForCorrection()
        {
            string[] texts = Enumerable.Repeat("云南评估基准日科技有限公司", 10)
                .Concat(new[] { "云南评估基准曰科技有限公司" })
                .ToArray();

            Assert.AreEqual(0, _checker.Check(Document(texts), "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void DocumentAAndB_DoNotShareFrequencyEvidence()
        {
            DocumentReadResult documentA = Document(
                Enumerable.Repeat("评估基准日", 3)
                    .Concat(new[] { "评估基准曰" })
                    .ToArray());
            DocumentReadResult documentB = Document("评估基准曰");

            TypoCandidate candidateA = _checker.Check(documentA, "文档 A").Candidates.Single();
            TypoCandidate candidateB = _checker.Check(documentB, "文档 B").Candidates.Single();

            CollectionAssert.Contains(candidateA.Reasons.ToList(), TypoEvidenceReason.DocumentConsistencySupported);
            CollectionAssert.DoesNotContain(candidateB.Reasons.ToList(), TypoEvidenceReason.DocumentConsistencySupported);
        }

        [TestMethod]
        public void SplitRunsWithinParagraph_AreReconstructedSafely()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(
                    new Run(new Text("评估基")),
                    new Run(new Text("准曰")))))
            {
                string before = Hash(file.FilePath);
                DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
                TypoCandidate candidate = _checker.Check(document, "文档 A").Candidates.Single();

                Assert.AreEqual("评估基准曰", candidate.OriginalText);
                Assert.AreEqual(0, candidate.Span.Start);
                Assert.AreEqual(5, candidate.Span.Length);
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public void DifferentParagraphsAndCells_AreNeverConcatenated()
        {
            DocumentReadResult paragraphs = Document("评估基", "准曰");
            DocumentReadResult cells = DocumentReadResult.Succeeded(
                new[]
                {
                    Cell("评估基", 0, 0),
                    Cell("准曰", 1, 1)
                },
                0,
                1);

            Assert.AreEqual(0, _checker.Check(paragraphs, "文档 A").Candidates.Count);
            Assert.AreEqual(0, _checker.Check(cells, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void CoverageDiagnostics_ShowActualScanning()
        {
            SpellCheckResult result = Check(
                "项目名称：评估基准曰示范项目",
                "评估基准日",
                "资产评估报吿");

            Assert.IsTrue(result.Diagnostics.TotalChineseTextSpans >= 3);
            Assert.IsTrue(result.Diagnostics.ProtectedSpans > 0);
            Assert.IsTrue(result.Diagnostics.ExactLexiconHits > 0);
            Assert.AreEqual(1, result.Diagnostics.KnownTypoHits);
            Assert.AreEqual(1, result.Diagnostics.ReportedIssues);
        }

        [TestMethod]
        public void ExpandedAntiFalsePositiveCorpus_HasNoLanguageIssue()
        {
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[]
                {
                    Element("项目名称：云南合成企业机器设备市场价值评估项目", 0),
                    Element("委托人：云南澄明合成有限责任公司；产权持有人：云南罕见字样合成有限公司。", 1),
                    Element("法定代表人：张三；项目所在地：云南省昆明市合成路。", 2),
                    Element("报告编号：云合成评报字〔2026〕第015号；依据第十五号合成法规。", 3),
                    Element("评估基准日为2026年5月31日，评估价值为1,234.56万元，增值率为80%。", 4),
                    Element("设备型号ABC-XJ2026，面积120㎡，单价3000元/㎡，contact@example.invalid。", 5),
                    Element("引用内容：“采用市场法、成本法、重置成本法，结合成新率与可比实例形成评估结论。”", 6),
                    Cell("房屋建筑物", 7, 0),
                    Cell("机器设备", 8, 1)
                },
                7,
                1);

            Assert.AreEqual(0, _checker.Check(document, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public async Task DesktopConfusableIssue_KeepsLanguageReviewSuggestedReminder()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("本项目评估基准曰为合成日期。")))))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);
                ReportCheckIssue issue = result.Issues.Single(item => item.Category == IssueCategory.Language);

                Assert.AreEqual(IssueCategory.Language, issue.Category);
                Assert.AreEqual(IssueAuthority.ReviewSuggested, issue.Authority);
                Assert.AreEqual(AssetAppraisalChecker.Core.Consistency.IssueSeverity.Reminder, issue.Severity);
                Assert.AreEqual("发现疑似专业术语错别字", issue.Summary);
            }
        }

        [TestMethod]
        [DataRow("EvaluationMethod", "V2-GT-501")]
        [DataRow("Integrated", "V2-GT-901")]
        public void GoldenSmoke_RecordsCoverageAndPreservesHash(string group, string caseId)
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
            SpellCheckResult result = _checker.Check(document, "文档 A");

            Assert.AreEqual(0, result.Candidates.Count, CandidateSummary(result));
            Assert.IsTrue(result.Diagnostics.TotalChineseTextSpans > 0);
            Assert.IsTrue(result.Diagnostics.ExactLexiconHits > 0);
            Assert.AreEqual(before, Hash(path));
        }

        private SpellCheckResult Check(params string[] texts)
        {
            return _checker.Check(Document(texts), "文档 A");
        }

        private static DocumentReadResult Document(params string[] texts)
        {
            return DocumentReadResult.Succeeded(
                texts.Select((text, index) => Element(text, index)).ToList(),
                texts.Length,
                0);
        }

        private static DocumentElement Element(string text, int index)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(DocumentPart.MainBody, index, null, null, null, new string[0]),
                index,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static DocumentElement Cell(string text, int order, int cell)
        {
            return new DocumentElement(
                DocumentElementType.TableCell,
                text,
                new DocumentLocation(DocumentPart.MainBody, null, 0, 0, cell, new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static string CandidateSummary(SpellCheckResult result)
        {
            return string.Join(
                " | ",
                result.Candidates.Select(item =>
                    item.OriginalText + " -> " + item.SuggestedTerm + " (" + item.Kind + ")"));
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
