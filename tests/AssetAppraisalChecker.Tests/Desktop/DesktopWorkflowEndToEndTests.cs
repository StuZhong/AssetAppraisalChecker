using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
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
    public sealed class DesktopWorkflowEndToEndTests
    {
        [TestMethod]
        public async Task ViewModel_ConflictDocx_ShowsMoneyAndReportNumberIssuesWithDetails()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(
                HeadingStyles(),
                Paragraph("项目名称：XX测试资产评估项目"),
                Paragraph("报告编号：云测试评报字〔2026〕第001号"),
                Paragraph("评估基准日：2026年6月30日"),
                StyledParagraph("Heading1", "报告摘要"),
                Paragraph("本项目不含税评估净值170万元。"),
                StyledParagraph("Heading1", "评估结论"),
                Paragraph("本项目不含税评估净值160万元。"),
                Paragraph("报告编号：云测试评报字〔2026〕第002号")))
            {
                var viewModel = new MainWindowViewModel(
                    new ReportCheckService(),
                    new NoOpFileLauncher());
                viewModel.TrySelectFile(file.FilePath);
                viewModel.ExpectedProjectName = "XX测试资产评估项目";

                await viewModel.StartCheckAsync();

                Assert.AreEqual(2, viewModel.TotalCount);
                Assert.IsTrue(viewModel.SeriousCount >= 2);
                Assert.AreEqual(2, viewModel.Issues.Count);
                CollectionAssert.AreEquivalent(
                    new[] { IssueType.MoneyConflict, IssueType.BasicFieldConflict },
                    viewModel.Issues.Select(item => item.IssueType).ToArray());

                foreach (IssueViewModel issue in viewModel.Issues)
                {
                    viewModel.SelectedIssue = issue;
                    Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.SelectedIssue.Reason));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.SelectedIssue.SuggestedAction));
                    Assert.IsTrue(viewModel.SelectedIssue.Occurrences.Count >= 2);
                    Assert.IsTrue(viewModel.SelectedIssue.Occurrences.All(
                        occurrence => !string.IsNullOrWhiteSpace(occurrence.LocationText)));
                }
            }
        }

        [TestMethod]
        public async Task ViewModel_ConsistentDocx_ShowsCautiousZeroIssueMessage()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(
                HeadingStyles(),
                Paragraph("项目名称：XX测试资产评估项目"),
                Paragraph("项目名称：XX测试资产评估项目"),
                Paragraph("报告编号：云测试评报字〔2026〕第001号"),
                Paragraph("报告编号：云测试评报字（2026）第001号"),
                Paragraph("评估基准日：2026年6月30日"),
                Paragraph("评估基准日：2026/06/30"),
                StyledParagraph("Heading1", "报告摘要"),
                Paragraph("本项目不含税评估净值170万元。"),
                StyledParagraph("Heading1", "评估结论"),
                Paragraph("本项目不含税评估净值人民币壹佰柒拾万元整。")))
            {
                var viewModel = new MainWindowViewModel(
                    new ReportCheckService(),
                    new NoOpFileLauncher());
                viewModel.TrySelectFile(file.FilePath);
                viewModel.ExpectedProjectName = "XX测试资产评估项目";

                await viewModel.StartCheckAsync();

                Assert.AreEqual(0, viewModel.TotalCount);
                Assert.AreEqual(0, viewModel.Issues.Count);
                StringAssert.Contains(
                    viewModel.ResultMessage,
                    "其他当前规则未发现能够确认的一致性问题。");
                StringAssert.Contains(viewModel.ResultMessage, "未识别到经认证的报告级项目名称位置");
                Assert.IsFalse(viewModel.ResultMessage.Contains("报告完全正确"));
            }
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
