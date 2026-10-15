using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.ViewModels;
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
    public sealed class RealWorldNearMatchNoiseTests
    {
        private readonly SpellChecker _checker = new SpellChecker();

        [TestMethod]
        [DataRow("本项目涉及输配电资产。")]
        [DataRow("各项资产已经完成资产清查。")]
        [DataRow("产权持有单位已提交合成资料。")]
        [DataRow("市场价格与市场价值不是同一个术语。")]
        [DataRow("增值税率按增值税法核对。")]
        [DataRow("本次评估值的计算已完成。")]
        [DataRow("本次评估项目的资料已经整理。")]
        [DataRow("评估的依据由合成资料构成。")]
        public void CompleteLegalExpressions_DoNotProduceNearMatchReminder(string text)
        {
            var candidates = Check(text).Candidates.Where(candidate =>
                candidate.Kind == TypoCandidateKind.ProfessionalTermNearMatch).ToList();
            Assert.AreEqual(0, candidates.Count, string.Join(", ", candidates.Select(item =>
                item.OriginalText + " -> " + item.SuggestedTerm)));
        }

        [TestMethod]
        [DataRow("报告使用者")]
        [DataRow("报告使用者应结合合成资料理解结论。")]
        [DataRow("资产投运")]
        [DataRow("本次资产投运验收已完成。")]
        [DataRow("增值税进项税额")]
        [DataRow("其他费用所发生的增值税进项税额。")]
        [DataRow("市场价确定设备购置价。")]
        [DataRow("以市场价确定其购置价及相关运杂费。")]
        [DataRow("增值税改革")]
        [DataRow("《关于深化增值税改革的合成说明》")]
        [DataRow("本次评估根据生产投资规模开展。")]
        [DataRow("出售一项资产所能收到的款项。")]
        public void LegalConceptsAndCompoundBoundaries_ProduceNoSpellingSuggestion(string text)
        {
            Assert.AreEqual(0, Check(text).Candidates.Count);
        }

        [TestMethod]
        [DataRow("评估价直", "评估价值")]
        [DataRow("本次评估价直为合成金额。", "评估价值")]
        [DataRow("本项含增直税金额需要复核。", "含增值税")]
        [DataRow("本次核对资产青单。", "资产清单")]
        [DataRow("本次核对评估依剧。", "评估依据")]
        [DataRow("房屋建筑筑物", "房屋建筑物")]
        public void EvidencedSpellingShapes_WithReliableBoundaries_RemainReviewCandidates(
            string text, string expected)
        {
            TypoCandidate candidate = Check(text).Candidates.Single();
            Assert.AreEqual(TypoCandidateKind.ProfessionalTermNearMatch, candidate.Kind);
            Assert.AreEqual(expected, candidate.SuggestedTerm);
            Assert.IsTrue(candidate.Evidence.Any(item => item.StartsWith("候选边界：")));
            Assert.IsTrue(candidate.Evidence.Any(item => item.StartsWith("错写依据：")));
        }

        [TestMethod]
        [DataRow("未确认前缀评估价直为合成金额。")]
        [DataRow("评估价直属于未确认复合名称。")]
        [DataRow("X评估价直")]
        [DataRow("评估价直X")]
        [DataRow("评估价至为未定义表达")]
        public void UnreliableBoundaryOrUncontrolledEdit_Abstains(string text)
        {
            Assert.AreEqual(0, Check(text).Candidates.Count);
        }

        [TestMethod]
        public void SplitRuns_DoNotChangeLegalCompoundOrTrueTypoResult()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("增值税进")), new Run(new Text("项税额。"))),
                new Paragraph(new Run(new Text("报告使用")), new Run(new Text("者。"))),
                new Paragraph(new Run(new Text("评估价")), new Run(new Text("直。")))))
            {
                string before = Hash(file.FilePath);
                SpellCheckResult result = _checker.Check(new DocxDocumentReader().Read(file.FilePath), "文档 A");
                TypoCandidate candidate = result.Candidates.Single();
                Assert.AreEqual("评估价直", candidate.OriginalText);
                Assert.AreEqual(2, candidate.Location.ParagraphIndex);
                Assert.AreEqual(0, candidate.Span.Start);
                Assert.AreEqual(4, candidate.Span.Length);
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public async Task ProductionDualDocument_ConfirmedLegalCasesStayQuiet_AndControlledTypoKeepsSource()
        {
            using (SyntheticDocxFile documentA = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("报告使用者已确认资产投运验收。"))),
                new Paragraph(new Run(new Text("增值税进项税额及增值税改革资料已经整理。")))))
            using (SyntheticDocxFile documentB = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("以市场价确定其购置价。"))),
                new Paragraph(new Run(new Text("本项目评估基准曰为合成日期。")))))
            {
                string hashA = Hash(documentA.FilePath);
                string hashB = Hash(documentB.FilePath);
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                Assert.IsFalse(result.Issues.Any(item => item.GovernanceKind == "Spell.ProfessionalTermNearMatch"));
                ReportCheckIssue typo = result.Issues.Single(item =>
                    item.GovernanceKind == "Spell.ConfusableProfessionalTerm");
                Assert.AreEqual("文档 B", typo.Occurrences.Single().DocumentLabel);
                Assert.AreEqual(1, typo.Occurrences.Single().Location.ParagraphIndex);
                Assert.AreEqual(IssueAuthority.ReviewSuggested, typo.Authority);
                Assert.AreEqual(hashA, Hash(documentA.FilePath));
                Assert.AreEqual(hashB, Hash(documentB.FilePath));
            }
        }

        [TestMethod]
        public void GenericProfessionalTypo_StillProducesReviewCandidate()
        {
            TypoCandidate candidate = Check("本次评估价直为一百万元。")
                .Candidates.Single(item => item.SuggestedTerm == "评估价值");

            Assert.AreEqual(TypoCandidateKind.ProfessionalTermNearMatch, candidate.Kind);
            Assert.AreEqual("评估价直", candidate.OriginalText);
        }

        [TestMethod]
        public void ControlledConfusableAndKnownTypo_StillProduceCandidates()
        {
            Assert.IsTrue(Check("本项目评估基准曰为合成日期。")
                .Candidates.Any(item => item.Kind == TypoCandidateKind.ConfusableProfessionalTerm &&
                    item.SuggestedTerm == "评估基准日"));
            Assert.IsTrue(Check("本资产评估报吿仅供人工复核。")
                .Candidates.Any(item => item.Kind == TypoCandidateKind.KnownTypo &&
                    item.SuggestedTerm == "资产评估报告"));
        }

        [TestMethod]
        public void TaxQualifierMustBeExact_ButInternalTypoRemainsDetectable()
        {
            Assert.IsFalse(Check("按增值税法核对。").Candidates.Any(item =>
                item.SuggestedTerm == "含增值税"));
            Assert.IsTrue(Check("本项含增直税金额需要复核。")
                .Candidates.Any(item => item.Kind == TypoCandidateKind.ProfessionalTermNearMatch &&
                    item.SuggestedTerm == "含增值税"));
        }

        [TestMethod]
        public void GenericNearMatch_IsDetailedReview_WhileControlledRulesStayFocused()
        {
            TypoCandidate nearCandidate = Check("本次评估价直为一百万元。")
                .Candidates.Single(item => item.SuggestedTerm == "评估价值");
            var nearIssue = new TextQualityIssueMapper().Map(
                new SpellCheckResult(new[] { nearCandidate },
                    new SpellCheckDiagnostics(1, 0, 0, 0, 1, 1)), 0).Single();
            Assert.AreEqual(IssueCategory.Language, nearIssue.Category);
            Assert.AreEqual(IssueAuthority.ReviewSuggested, nearIssue.Authority);
            Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(nearIssue)));

            foreach (string controlled in new[]
            {
                "本项目评估基准曰为合成日期。",
                "本资产评估报吿仅供人工复核。"
            })
            {
                SpellCheckResult result = Check(controlled);
                var issue = new TextQualityIssueMapper().Map(result, 0).Single();
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(issue)));
            }
        }

        [TestMethod]
        public async Task ProductionDualDocument_LeavesLegalExpressionQuietAndKeepsTypoReviewReadOnly()
        {
            using (SyntheticDocxFile documentA = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("本项目涉及输配电资产，产权持有单位已确认。")))))
            using (SyntheticDocxFile documentB = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("本项目评估基准曰为合成日期。")))))
            {
                string hashA = Hash(documentA.FilePath);
                string hashB = Hash(documentB.FilePath);
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null));

                Assert.IsTrue(result.IsSuccess);
                Assert.IsFalse(result.Issues.Any(item => item.Category == IssueCategory.Language &&
                    item.Occurrences.Any(occurrence => occurrence.DocumentLabel == "文档 A")));
                Assert.IsTrue(result.Issues.Any(item => item.Category == IssueCategory.Language &&
                    item.Authority == IssueAuthority.ReviewSuggested &&
                    item.Occurrences.Any(occurrence => occurrence.DocumentLabel == "文档 B")));
                Assert.AreEqual(hashA, Hash(documentA.FilePath));
                Assert.AreEqual(hashB, Hash(documentB.FilePath));
            }
        }

        private SpellCheckResult Check(string text)
        {
            var element = new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(DocumentPart.MainBody, 0, null, null, null, new string[0]),
                0,
                string.Empty,
                null);
            return _checker.Check(DocumentReadResult.Succeeded(new[] { element }, 1, 0), "文档 A");
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
