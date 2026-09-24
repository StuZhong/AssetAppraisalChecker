using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class ProjectNameExpectedWorkflowTests
    {
        private const string ProjectA = "虚构甲公司拟处置设备资产公允价值";
        private const string ProjectB = "虚构乙公司拟处置设备资产公允价值";
        private const string ProjectWrong = "虚构错误公司拟处置设备资产公允价值";
        private const string ProjectCorrect = "虚构正确公司拟处置设备资产公允价值";

        private readonly ReportCheckService service = new ReportCheckService();

        [TestMethod]
        public async Task CheckAsync_ExpectedCorrectAndThreeMatches_ReturnsNoMismatchIssue()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectA, ProjectA))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                Assert.AreEqual(ProjectNameExpectedCheckStatus.ValidExpectedValue,
                    result.ProjectNameExpectedCheck.Status);
                Assert.AreEqual(3, result.ProjectNameExpectedCheck.MatchCount);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
            }
        }

        [TestMethod]
        public async Task CheckAsync_CoverMismatchOnly_ReturnsCoverIssue()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectB, ProjectA, ProjectA))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                AssertSingleRole(result, ReportLevelProjectNameAuthorityRole.Cover);
            }
        }

        [TestMethod]
        public async Task CheckAsync_SummaryMismatchOnly_ReturnsSummaryIssue()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectB, ProjectA))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                AssertSingleRole(result, ReportLevelProjectNameAuthorityRole.Summary);
            }
        }

        [TestMethod]
        public async Task CheckAsync_BodyMismatchOnly_ReturnsBodyIssue()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectA, ProjectB))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                AssertSingleRole(result, ReportLevelProjectNameAuthorityRole.Body);
            }
        }

        [TestMethod]
        public async Task CheckAsync_ABBWithExpectedA_ReturnsSummaryAndBodyIssues()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectB, ProjectB))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        ReportLevelProjectNameAuthorityRole.Summary,
                        ReportLevelProjectNameAuthorityRole.Body
                    },
                    IssueRoles(result));
            }
        }

        [TestMethod]
        public async Task CheckAsync_ABBWithExpectedB_ReturnsCoverIssue()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectB, ProjectB))
            {
                ReportCheckResult result = await Check(file, ProjectB);

                AssertSingleRole(result, ReportLevelProjectNameAuthorityRole.Cover);
            }
        }

        [TestMethod]
        public async Task ViewModel_ABBWithExpectedA_PassesInputAndShowsTwoMismatchIssues()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectB, ProjectB))
            {
                MainWindowViewModel viewModel = await CheckThroughViewModel(file, ProjectA);

                IssueViewModel[] issues = viewModel.Issues.Where(item =>
                    item.IssueType == IssueType.ProjectNameExpectedMismatch).ToArray();
                Assert.AreEqual(2, issues.Length);
                Assert.IsTrue(issues.Any(item => item.Summary.Contains("报告摘要")));
                Assert.IsTrue(issues.Any(item => item.Summary.Contains("报告正文")));
            }
        }

        [TestMethod]
        public async Task ViewModel_ExpectedInput_PreservesMoneyConflict()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                StyledParagraph("Heading1", "报告摘要"),
                Paragraph("本项目不含税评估净值170万元。"),
                StyledParagraph("Heading1", "评估结论"),
                Paragraph("本项目不含税评估净值160万元。")))
            {
                MainWindowViewModel viewModel = await CheckThroughViewModel(file, ProjectA);

                Assert.AreEqual(1, viewModel.Issues.Count(item =>
                    item.IssueType == IssueType.MoneyConflict));
            }
        }

        [TestMethod]
        public async Task ViewModel_ExpectedInput_PreservesReportNumberConflict()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                Paragraph("报告编号：虚构评报字〔2026〕第001号"),
                Paragraph("报告编号：虚构评报字〔2026〕第002号")))
            {
                MainWindowViewModel viewModel = await CheckThroughViewModel(file, ProjectA);

                Assert.AreEqual(1, viewModel.Issues.Count(item =>
                    item.IssueType == IssueType.BasicFieldConflict &&
                    item.FieldName == "报告编号"));
            }
        }

        [TestMethod]
        public async Task ViewModel_ExpectedInput_PreservesValuationDateConflict()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                Paragraph("评估基准日：2026年1月2日"),
                Paragraph("评估基准日：2026年1月3日")))
            {
                MainWindowViewModel viewModel = await CheckThroughViewModel(file, ProjectA);

                Assert.AreEqual(1, viewModel.Issues.Count(item =>
                    item.IssueType == IssueType.BasicFieldConflict &&
                    item.FieldName == "评估基准日"));
            }
        }

        [TestMethod]
        public async Task CheckAsync_ThreeInternallyEqualWrongValues_ExposesAllMismatches()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectWrong,
                ProjectWrong,
                ProjectWrong))
            {
                ReportCheckResult result = await Check(file, ProjectCorrect);

                Assert.AreEqual(3, ProjectNameIssues(result).Count);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        ReportLevelProjectNameAuthorityRole.Cover,
                        ReportLevelProjectNameAuthorityRole.Summary,
                        ReportLevelProjectNameAuthorityRole.Body
                    },
                    IssueRoles(result));
            }
        }

        [TestMethod]
        public async Task CheckAsync_CandidateDocumentOrderChanged_PreservesSemanticResult()
        {
            using (SyntheticDocxFile ordinary = ProjectDocument(
                ProjectA,
                ProjectB,
                ProjectB))
            using (SyntheticDocxFile reordered = ProjectDocument(
                ProjectA,
                ProjectB,
                ProjectB,
                true))
            {
                ReportCheckResult first = await Check(ordinary, ProjectA);
                ReportCheckResult second = await Check(reordered, ProjectA);

                CollectionAssert.AreEquivalent(IssueRoles(first), IssueRoles(second));
            }
        }

        [TestMethod]
        public async Task CheckAsync_MissingExpected_ReturnsExplicitStatusWithoutMismatch()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectA, ProjectA))
            {
                ReportCheckResult result = await service.CheckAsync(file.FilePath);

                Assert.AreEqual(ProjectNameExpectedCheckStatus.MissingExpectedValue,
                    result.ProjectNameExpectedCheck.Status);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
                StringAssert.Contains(result.Message, "未提供正确项目名称");
            }
        }

        [TestMethod]
        public async Task CheckAsync_ValidExpectedWithoutCandidates_IsNotReportedAsPass()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Paragraph("完全虚构的普通正文。")))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                Assert.AreEqual(ProjectNameExpectedCheckStatus.NoAuthoritativeCandidates,
                    result.ProjectNameExpectedCheck.Status);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
                StringAssert.Contains(result.Message, "项目名称人工基准校检未完成");
            }
        }

        [TestMethod]
        public async Task CheckAsync_InvalidExpected_ReturnsNormalizationFailureWithoutMismatch()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectA, ProjectA))
            {
                ReportCheckResult result = await Check(file, "   ");

                Assert.AreEqual(ProjectNameExpectedCheckStatus.ExpectedNormalizationFailed,
                    result.ProjectNameExpectedCheck.Status);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
                StringAssert.Contains(result.Message, "无法形成有效标准值");
            }
        }

        [TestMethod]
        public void Mapper_CandidateNormalizationFailure_DoesNotBecomeMismatch()
        {
            ReportLevelProjectNameCandidate failed = CandidateWithNormalization(
                BasicFieldNormalizationResult.Failed("虚构候选规范化失败。"));
            ProjectNameExpectedCheckResult checkResult =
                new ExpectedProjectNameChecker().Check(ProjectA, new[] { failed });

            IReadOnlyList<ReportCheckIssue> issues =
                new ProjectNameExpectedIssueMapper().Map(checkResult, 0);

            Assert.AreEqual(ProjectNameExpectedCheckStatus.CandidateNormalizationFailed,
                checkResult.Status);
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public async Task CheckAsync_MismatchIssuePreservesAuthorityRole()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectB, ProjectA, ProjectA))
            {
                ReportCheckIssue issue = ProjectNameIssues(
                    await Check(file, ProjectA)).Single();

                Assert.AreEqual(BasicFieldType.ProjectName, issue.FieldType);
                Assert.AreEqual(ProjectA, issue.ExpectedValue);
                Assert.AreEqual(ReportLevelProjectNameAuthorityRole.Cover,
                    issue.Occurrences[0].ProjectNameCandidate.AuthorityRole);
            }
        }

        [TestMethod]
        public async Task CheckAsync_MismatchIssuePreservesFullCandidateProvenance()
        {
            using (SyntheticDocxFile file = ProjectDocument(ProjectA, ProjectB, ProjectA))
            {
                ReportCheckIssue issue = ProjectNameIssues(
                    await Check(file, ProjectA)).Single();
                ReportLevelProjectNameCandidate candidate =
                    issue.Occurrences[0].ProjectNameCandidate;

                Assert.IsNotNull(candidate);
                Assert.AreEqual(2, candidate.SourceParagraphs.Count);
                Assert.IsNotNull(candidate.Anchor);
                Assert.AreEqual(
                    ReportLevelProjectNameExtractionPattern.Pattern1TwoParagraphDirect,
                    candidate.ExtractionPattern);
                Assert.AreEqual(candidate.AssembledText, issue.Occurrences[0].RawText);
            }
        }

        [TestMethod]
        public async Task CheckAsync_LegacyProjectNameDoesNotEnterManualComparison()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                Paragraph("项目名称：" + ProjectB)))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                Assert.AreEqual(3, result.ProjectNameExpectedCheck.Comparisons.Count);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
                Assert.AreEqual(1, result.BasicFieldOccurrenceCount);
            }
        }

        [TestMethod]
        public async Task Regression_ReportNumberConflictRemainsUnchanged()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                Paragraph("报告编号：虚构评报字〔2026〕第001号"),
                Paragraph("报告编号：虚构评报字〔2026〕第002号")))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.IssueType == IssueType.BasicFieldConflict);
                Assert.AreEqual(BasicFieldType.ReportNumber, issue.FieldType);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
            }
        }

        [TestMethod]
        public async Task Regression_ValuationDateConflictRemainsUnchanged()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                Paragraph("评估基准日：2026年1月2日"),
                Paragraph("评估基准日：2026年1月3日")))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.IssueType == IssueType.BasicFieldConflict);
                Assert.AreEqual(BasicFieldType.ValuationDate, issue.FieldType);
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
            }
        }

        [TestMethod]
        public async Task Regression_MoneyConflictRemainsUnchanged()
        {
            using (SyntheticDocxFile file = ProjectDocument(
                ProjectA,
                ProjectA,
                ProjectA,
                false,
                StyledParagraph("Heading1", "报告摘要"),
                Paragraph("本项目不含税评估净值170万元。"),
                StyledParagraph("Heading1", "评估结论"),
                Paragraph("本项目不含税评估净值160万元。")))
            {
                ReportCheckResult result = await Check(file, ProjectA);

                Assert.AreEqual(1, result.Issues.Count(item =>
                    item.IssueType == IssueType.MoneyConflict));
                Assert.AreEqual(0, ProjectNameIssues(result).Count);
            }
        }

        private async Task<ReportCheckResult> Check(
            SyntheticDocxFile file,
            string expectedProjectName)
        {
            return await service.CheckAsync(new ReportCheckRequest(
                file.FilePath,
                expectedProjectName));
        }

        private async Task<MainWindowViewModel> CheckThroughViewModel(
            SyntheticDocxFile file,
            string expectedProjectName)
        {
            var viewModel = new MainWindowViewModel(
                service,
                new NoOpFileLauncher());
            viewModel.TrySelectFile(file.FilePath);
            viewModel.ExpectedProjectName = expectedProjectName;
            await viewModel.StartCheckAsync();
            return viewModel;
        }

        private static List<ReportCheckIssue> ProjectNameIssues(ReportCheckResult result)
        {
            return result.Issues.Where(item =>
                item.IssueType == IssueType.ProjectNameExpectedMismatch).ToList();
        }

        private static ReportLevelProjectNameAuthorityRole[] IssueRoles(
            ReportCheckResult result)
        {
            return ProjectNameIssues(result)
                .Select(item => item.Occurrences.Single()
                    .ProjectNameCandidate.AuthorityRole)
                .ToArray();
        }

        private static void AssertSingleRole(
            ReportCheckResult result,
            ReportLevelProjectNameAuthorityRole role)
        {
            ReportCheckIssue issue = ProjectNameIssues(result).Single();
            Assert.AreEqual(IssueType.ProjectNameExpectedMismatch, issue.IssueType);
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            Assert.AreEqual(role,
                issue.Occurrences.Single().ProjectNameCandidate.AuthorityRole);
        }

        private static SyntheticDocxFile ProjectDocument(
            string cover,
            string summary,
            string body,
            bool bodyBeforeSummary = false,
            params OpenXmlElement[] trailing)
        {
            var elements = new List<OpenXmlElement>();
            AddBlock(elements, cover, ReportLevelProjectNameAuthorityRole.Cover);
            if (bodyBeforeSummary)
            {
                AddBlock(elements, body, ReportLevelProjectNameAuthorityRole.Body);
                AddBlock(elements, summary, ReportLevelProjectNameAuthorityRole.Summary);
            }
            else
            {
                AddBlock(elements, summary, ReportLevelProjectNameAuthorityRole.Summary);
                AddBlock(elements, body, ReportLevelProjectNameAuthorityRole.Body);
            }

            elements.AddRange(trailing ?? new OpenXmlElement[0]);
            return SyntheticDocxFile.CreateWithStyles(
                HeadingStyles(),
                elements.ToArray());
        }

        private static void AddBlock(
            ICollection<OpenXmlElement> elements,
            string projectName,
            ReportLevelProjectNameAuthorityRole role)
        {
            int split = projectName.IndexOf("设备资产", StringComparison.Ordinal);
            Assert.IsTrue(split > 0, "虚构项目名称必须包含可拆分的设备资产短语。");
            elements.Add(Paragraph(projectName.Substring(0, split)));
            elements.Add(Paragraph(projectName.Substring(split)));
            if (role == ReportLevelProjectNameAuthorityRole.Cover)
            {
                elements.Add(Paragraph("资产评估报告"));
            }
            else if (role == ReportLevelProjectNameAuthorityRole.Summary)
            {
                elements.Add(Paragraph("资产评估报告摘要"));
            }
            else
            {
                elements.Add(StyledParagraph("Heading1", "资产评估报告"));
            }
        }

        private static ReportLevelProjectNameCandidate CandidateWithNormalization(
            BasicFieldNormalizationResult normalization)
        {
            var paragraphs = new[]
            {
                Element("虚构甲公司拟处置", 0),
                Element("设备资产公允价值", 1)
            };
            return new ReportLevelProjectNameCandidate(
                ProjectA,
                ReportLevelProjectNameAuthorityRole.Cover,
                paragraphs,
                Element("资产评估报告", 2),
                ReportLevelProjectNameExtractionPattern.Pattern1TwoParagraphDirect,
                normalization);
        }

        private static DocumentElement Element(string text, int order)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    order,
                    null,
                    null,
                    null,
                    null),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static IEnumerable<Style> HeadingStyles()
        {
            return new[]
            {
                new Style(new StyleName { Val = "heading 1" })
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Heading1"
                }
            };
        }

        private static Paragraph StyledParagraph(string styleId, string text)
        {
            return new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
                new Run(new Text(text)));
        }

        private static Paragraph Paragraph(string text)
        {
            return new Paragraph(new Run(new Text(text)));
        }

        private sealed class NoOpFileLauncher : IReportFileLauncher
        {
            public ReportFileLaunchResult Open(string filePath)
            {
                return new ReportFileLaunchResult(true, "虚构测试不实际打开文件。");
            }
        }
    }
}
