using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using AssetAppraisalChecker.TextQuality;
using AssetAppraisalChecker.TextQuality.Grammar;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.TextQuality
{
    [TestClass]
    public sealed class GrammarExpressionCheckerTests
    {
        private readonly GrammarExpressionChecker _checker = new GrammarExpressionChecker();

        [TestMethod]
        public void Catalog_IsSmallControlledAndExplainable()
        {
            IReadOnlyList<ExpressionRule> rules = _checker.Catalog.Rules;

            Assert.AreEqual(24, rules.Count);
            Assert.AreEqual(rules.Count, rules.Select(item => item.RuleId).Distinct().Count());
            Assert.IsTrue(rules.All(item => !string.IsNullOrWhiteSpace(item.Trigger)));
            Assert.IsTrue(rules.All(item => !string.IsNullOrWhiteSpace(item.ExpectedStructure)));
            Assert.IsTrue(rules.All(item => !string.IsNullOrWhiteSpace(item.Explanation)));
        }

        [TestMethod]
        [DataRow("本次评估对象的主要构成主要包括。", "GE-DANGLE-001")]
        [DataRow("本次评估根据现场勘查以及。", "GE-DANGLE-004")]
        [DataRow("本次分析所采用的参数具体为。", "GE-DANGLE-002")]
        [DataRow("本次评估主要考虑了。", "GE-DANGLE-013")]
        public void DanglingControlledEnding_ReportsOneReminder(string text, string ruleId)
        {
            ExpressionCandidate candidate = Check(text).Candidates.Single();

            Assert.AreEqual(GrammarExpressionKind.DanglingSentenceEnding, candidate.Evidence.Kind);
            Assert.AreEqual(ruleId, candidate.Evidence.RuleId);
        }

        [TestMethod]
        public void RepeatedSyntacticSkeleton_ReportsOneCandidate()
        {
            ExpressionCandidate candidate = Check(
                "评估人员对机器设备进行了现场勘查进行核实。")
                .Candidates.Single();

            Assert.AreEqual(GrammarExpressionKind.RepeatedSyntacticSkeleton, candidate.Evidence.Kind);
            Assert.AreEqual("GE-REPEAT-001", candidate.Evidence.RuleId);
        }

        [TestMethod]
        public void RepeatedAdoptionSkeleton_ReportsWithoutMechanicalTokenDuplication()
        {
            ExpressionCandidate candidate = Check(
                "本次评估采用市场法进行评估采用收益法形成结论。")
                .Candidates.Single();

            Assert.AreEqual("GE-REPEAT-002", candidate.Evidence.RuleId);
        }

        [TestMethod]
        [DataRow("本次评估截至评估日为。", "GE-FIXED-001")]
        [DataRow("本次评估采用进行市场法。", "GE-FIXED-002")]
        [DataRow("本次评估依据根据现有资料形成结论。", "GE-FIXED-003")]
        public void BrokenFixedExpression_ReportsOneCandidate(string text, string ruleId)
        {
            Assert.AreEqual(ruleId, Check(text).Candidates.Single().Evidence.RuleId);
        }

        [TestMethod]
        [DataRow("虽然市场发生变化，但是。", "GE-PAIR-001")]
        [DataRow("如果资料发生变化，那么。", "GE-PAIR-002")]
        [DataRow("本次评估不仅考虑成本因素，而且。", "GE-PAIR-003")]
        [DataRow("本次评估既考虑市场因素，又。", "GE-PAIR-004")]
        [DataRow("一方面需要核实资料，另一方面。", "GE-PAIR-005")]
        public void ClearlyBrokenCorrelativePair_ReportsOneCandidate(string text, string ruleId)
        {
            ExpressionCandidate candidate = Check(text).Candidates.Single();

            Assert.AreEqual(GrammarExpressionKind.MissingCorrelativeStructure, candidate.Evidence.Kind);
            Assert.AreEqual(ruleId, candidate.Evidence.RuleId);
        }

        [TestMethod]
        [DataRow("本次评估主要包括机器设备、车辆等。")]
        [DataRow("虽然市场发生变化，评估结果仍可参考。")]
        [DataRow("本次评估采用市场法，并结合现场勘查和市场资料形成结论。")]
        [DataRow("评估基准日为2026年5月31日，评估价值为998.22万元。")]
        [DataRow("项目名称：云南合成企业机器设备市场价值评估项目。")]
        [DataRow("委托人：云南合成评估有限责任公司。")]
        public void NormalBusinessExpression_DoesNotReport(string text)
        {
            Assert.AreEqual(0, Check(text).Candidates.Count, CandidateSummary(Check(text)));
        }

        [TestMethod]
        public void HeadingAndTableHeader_AreSkipped()
        {
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[]
                {
                    Element("评估对象和评估范围", 0, DocumentElementType.Heading, "Heading1"),
                    Cell("评估方法", 1, 0)
                },
                1,
                1);
            GrammarCheckResult result = _checker.Check(document, "文档 A");

            Assert.AreEqual(0, result.Candidates.Count);
            Assert.AreEqual(1, result.Diagnostics.SkippedHeadings);
            Assert.AreEqual(1, result.Diagnostics.SkippedTables);
        }

        [TestMethod]
        public void LongTableExplanation_CanBeCheckedConservatively()
        {
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[] { Cell("该表用于说明本次评估过程中已经收集现场资料并根据现场勘查以及。", 0, 0) },
                0,
                1);

            Assert.AreEqual(1, _checker.Check(document, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void ListIntroduction_FollowedByNumberedList_DoesNotReport()
        {
            DocumentReadResult document = Document(
                "主要包括：",
                "1、机器设备",
                "2、车辆");

            Assert.AreEqual(0, _checker.Check(document, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void SpecificFollowingStructure_DoesNotReport()
        {
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[]
                {
                    Element("本次评估对象的构成具体如下：", 0),
                    Cell("机器设备", 1, 0)
                },
                1,
                1);

            Assert.AreEqual(0, _checker.Check(document, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void SentenceSegmenter_ProtectsNestedPunctuationAndTechnicalTokens()
        {
            DocumentElement element = Element(
                "依据《资产评估准则（试行；修订）》执行，日期2026.05.31，金额1.25万元，详见https://example.invalid/a;b。",
                0);
            DocumentReadResult document = DocumentReadResult.Succeeded(new[] { element }, 1, 0);
            IReadOnlyList<SentenceSpan> spans = new SentenceSegmenter().Segment(
                element,
                GrammarParagraphRole.Body,
                ProtectedTermRegistry.Build(document));

            Assert.AreEqual(1, spans.Count);
        }

        [TestMethod]
        public void MechanicalDuplicateTokensAndPunctuation_AreNotGrammarCandidates()
        {
            GrammarCheckResult result = Check("评估评估结果如下。。本次  评估采用市场法。");

            Assert.AreEqual(0, result.Candidates.Count, CandidateSummary(result));
        }

        [TestMethod]
        public void StableIdentity_ContainsDocumentGrammarRuleLocationAndSpan()
        {
            ExpressionCandidate candidate = Check("本次评估根据现场勘查以及。").Candidates.Single();

            StringAssert.Contains(candidate.StableIdentity, "文档 A|Language|Grammar.DanglingSentenceEnding");
            StringAssert.Contains(candidate.StableIdentity, "GE-DANGLE-004");
            StringAssert.Contains(candidate.StableIdentity, "MainBody");
            StringAssert.Contains(candidate.StableIdentity, candidate.NormalizedEvidence);
        }

        [TestMethod]
        public void SameRuleAndSpan_IsReportedOnlyOnce()
        {
            GrammarCheckResult result = Check("本次评估根据现场勘查以及。");

            Assert.AreEqual(1, result.Candidates.Count);
            Assert.AreEqual(result.Candidates.Count, result.Candidates.Select(item => item.StableIdentity).Distinct().Count());
        }

        [TestMethod]
        public void DocumentAAndB_AreIndependent()
        {
            DocumentReadResult document = Document("本次评估根据现场勘查以及。");
            ExpressionCandidate candidateA = _checker.Check(document, "文档 A").Candidates.Single();
            ExpressionCandidate candidateB = _checker.Check(document, "文档 B").Candidates.Single();

            Assert.AreEqual("文档 A", candidateA.SourceDocument);
            Assert.AreEqual("文档 B", candidateB.SourceDocument);
            Assert.AreNotEqual(candidateA.StableIdentity, candidateB.StableIdentity);
        }

        [TestMethod]
        public void Diagnostics_RecordSingleSegmentationAndCoverage()
        {
            GrammarCheckResult result = Check(
                "本次评估采用市场法。评估结论依据现场资料形成。",
                "本次评估根据现场勘查以及。");
            GrammarCheckDiagnostics diagnostics = result.Diagnostics;

            Assert.AreEqual(2, diagnostics.ParagraphsScanned);
            Assert.AreEqual(3, diagnostics.SentencesScanned);
            Assert.AreEqual(3, diagnostics.BodySentences);
            Assert.AreEqual(3 * 24, diagnostics.RulesEvaluated);
            Assert.AreEqual(1, diagnostics.Candidates);
            Assert.AreEqual(1, diagnostics.ReportedIssues);
            Assert.IsTrue(diagnostics.ProtectedSpanCount >= 0);
        }

        [TestMethod]
        public void ExpandedSyntheticAntiFalsePositiveCorpus_HasZeroIssues()
        {
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[]
                {
                    Element("资产评估报告", 0, DocumentElementType.Heading, "Heading1"),
                    Element("项目名称：云南合成企业机器设备市场价值评估项目。", 1),
                    Element("委托人：云南澄明合成有限责任公司；产权持有人：云南合成设备有限公司。", 2),
                    Element("评估基准日为2026年5月31日，评估价值为998.22万元。", 3),
                    Element("本次评估采用市场法，并结合公开市场资料和现场勘查结果形成结论。", 4),
                    Element("虽然市场发生变化，评估结果仍可作为本次经济行为的参考。", 5),
                    Element("本次评估涉及的资产主要包括：", 6),
                    Element("1、机器设备", 7),
                    Element("2、运输车辆", 8),
                    Cell("评估方法", 9, 0),
                    Cell("市场法", 10, 1),
                    Element("依据《资产评估准则》及相关规定执行，设备型号ABC-XJ2026。", 11)
                },
                10,
                1);

            Assert.AreEqual(0, _checker.Check(document, "文档 A").Candidates.Count);
        }

        [TestMethod]
        public void Mapper_UsesLanguageReviewSuggestedReminderGovernance()
        {
            GrammarCheckResult result = Check("本次评估根据现场勘查以及。");
            ReportCheckIssue issue = new GrammarIssueMapper().Map(result, 0).Single();

            Assert.AreEqual(IssueCategory.Language, issue.Category);
            Assert.AreEqual(IssueAuthority.ReviewSuggested, issue.Authority);
            Assert.AreEqual(IssueSeverity.Reminder, issue.Severity);
            Assert.IsTrue(issue.GovernanceKind.StartsWith("Grammar.", StringComparison.Ordinal));
            Assert.AreEqual("语句表达", issue.IssueTypeDisplayOverride);
        }

        [TestMethod]
        public async Task DesktopSingleDocument_ShowsGrammarIssueWithoutChangingFile()
        {
            using (SyntheticDocxFile file = CreateDocx("本次评估根据现场勘查以及。"))
            {
                string before = Hash(file.FilePath);
                ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);
                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.GovernanceKind.StartsWith("Grammar.", StringComparison.Ordinal));

                Assert.AreEqual("文档 A", issue.Occurrences.Single().DocumentLabel);
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public async Task DesktopDualDocument_LabelsEachGrammarIssueIndependently()
        {
            using (SyntheticDocxFile documentA = CreateDocx("本次评估根据现场勘查以及。"))
            using (SyntheticDocxFile documentB = CreateDocx("本次评估如果资料发生变化，那么。"))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null));
                ReportCheckIssue[] issues = result.Issues.Where(item =>
                    item.GovernanceKind.StartsWith("Grammar.", StringComparison.Ordinal)).ToArray();

                Assert.AreEqual(2, issues.Length);
                CollectionAssert.AreEquivalent(
                    new[] { "文档 A", "文档 B" },
                    issues.Select(item => item.Occurrences.Single().DocumentLabel).ToArray());
            }
        }

        [TestMethod]
        [DataRow("EvaluationMethod", "V2-GT-501")]
        [DataRow("Integrated", "V2-GT-901")]
        public void GoldenSmoke_HasNoGrammarIssueAndPreservesHash(string group, string caseId)
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
            GrammarCheckResult result = _checker.Check(new DocxDocumentReader().Read(path), "文档 A");
            GrammarCheckDiagnostics diagnostics = result.Diagnostics;

            Console.WriteLine(
                "{0}: ParagraphsScanned={1}; SentencesScanned={2}; BodySentences={3}; RulesEvaluated={4}; Candidates={5}; ReportedIssues={6}; SkippedHeadings={7}; SkippedTables={8}; ProtectedSpanCount={9}",
                caseId,
                diagnostics.ParagraphsScanned,
                diagnostics.SentencesScanned,
                diagnostics.BodySentences,
                diagnostics.RulesEvaluated,
                diagnostics.Candidates,
                diagnostics.ReportedIssues,
                diagnostics.SkippedHeadings,
                diagnostics.SkippedTables,
                diagnostics.ProtectedSpanCount);

            Assert.AreEqual(0, result.Candidates.Count, CandidateSummary(result));
            Assert.IsTrue(diagnostics.ParagraphsScanned > 0);
            Assert.IsTrue(diagnostics.SentencesScanned > 0);
            Assert.AreEqual(before, Hash(path));
        }

        private GrammarCheckResult Check(params string[] texts)
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

        private static DocumentElement Element(
            string text,
            int index,
            DocumentElementType type = DocumentElementType.Paragraph,
            string styleId = "")
        {
            return new DocumentElement(
                type,
                text,
                new DocumentLocation(DocumentPart.MainBody, index, null, null, null, new string[0]),
                index,
                styleId,
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

        private static SyntheticDocxFile CreateDocx(string text)
        {
            return SyntheticDocxFile.Create(new Paragraph(new Run(new Text(text))));
        }

        private static string CandidateSummary(GrammarCheckResult result)
        {
            return string.Join(
                " | ",
                result.Candidates.Select(item => item.Evidence.RuleId + ":" + item.OriginalSentence));
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
