using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class ReportCheckServiceTests
    {
        private readonly ReportCheckService _service = new ReportCheckService();

        [TestMethod]
        public async Task CheckAsync_ValidDocx_CompletesFullWorkflowSuccessfully()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Paragraph("项目名称：XX测试资产评估项目"),
                Paragraph("评估基准日：2026年6月30日")))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(DocumentReadStatus.Success, result.DocumentReadStatus);
                Assert.AreEqual(0, result.MoneyOccurrenceCount);
                Assert.AreEqual(2, result.BasicFieldOccurrenceCount);
                Assert.AreEqual(Path.GetFileName(file.FilePath), result.FileName);
                Assert.IsTrue(result.CompletedAt >= result.StartedAt);
                Assert.IsTrue(result.Duration >= TimeSpan.Zero);
            }
        }

        [TestMethod]
        public async Task CheckAsync_MoneyConflict_IsIncludedInUnifiedResult()
        {
            using (SyntheticDocxFile file = CreateMoneyDocument("170万元", "160万元"))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                ReportCheckIssue issue = result.Issues.Single();
                Assert.AreEqual(IssueType.MoneyConflict, issue.IssueType);
                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                Assert.AreEqual(2, issue.Occurrences.Count);
                Assert.IsTrue(issue.Occurrences.All(item => item.NormalizedValue.EndsWith("元")));
            }
        }

        [TestMethod]
        public async Task CheckAsync_BasicFieldConflict_IsIncludedInUnifiedResult()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Paragraph("报告编号：云测试评报字〔2026〕第001号"),
                Paragraph("报告编号：云测试评报字〔2026〕第002号")))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                ReportCheckIssue issue = result.Issues.Single();
                Assert.AreEqual(IssueType.BasicFieldConflict, issue.IssueType);
                Assert.AreEqual("报告编号", issue.FieldName);
                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            }
        }

        [TestMethod]
        public async Task CheckAsync_MoneyAndBasicConflicts_AreMergedAndSeveritySorted()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                StyledParagraph("Heading1", "报告摘要"),
                Paragraph("本项目不含税评估净值170万元。"),
                StyledParagraph("Heading1", "评估结论"),
                Paragraph("本项目不含税评估净值160万元。"),
                Paragraph("报告编号：云测试评报字〔2026〕第001号"),
                Paragraph("报告编号：云测试评报字〔2026〕第002号")))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                Assert.AreEqual(2, result.TotalCount);
                CollectionAssert.AreEquivalent(
                    new[] { IssueType.MoneyConflict, IssueType.BasicFieldConflict },
                    result.Issues.Select(item => item.IssueType).ToArray());
                Assert.IsTrue(result.Issues.All(item => item.Severity == IssueSeverity.Serious));
                Assert.AreEqual(2, result.SeriousCount);
            }
        }

        [TestMethod]
        public async Task CheckAsync_ConsistentReport_ReturnsNoIssueAndCautiousMessage()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Paragraph("项目名称：XX测试资产评估项目"),
                Paragraph("项目名称：XX测试资产评估项目"),
                Paragraph("报告编号：云测试评报字〔2026〕第001号"),
                Paragraph("报告编号：云测试评报字（2026）第001号"),
                Paragraph("本项目不含税评估净值170万元。"),
                Paragraph("本项目不含税评估净值人民币壹佰柒拾万元整。")))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.TotalCount);
                StringAssert.Contains(result.Message, "其他当前规则未发现能够确认的一致性问题。");
                StringAssert.Contains(result.Message, "未提供正确项目名称");
                Assert.AreEqual(
                    ProjectNameExpectedCheckStatus.MissingExpectedValue,
                    result.ProjectNameExpectedCheck.Status);
                Assert.IsFalse(result.Message.Contains("完全正确"));
            }
        }

        [TestMethod]
        public async Task CheckAsync_InvalidDocx_FailsSafelyWithoutIssue()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(
                Encoding.UTF8.GetBytes("完全虚构的无效 DOCX 内容")))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(DocumentReadStatus.InvalidDocx, result.DocumentReadStatus);
                Assert.AreEqual(0, result.TotalCount);
                StringAssert.Contains(result.Message, "文件不是有效的 DOCX");
                Assert.IsFalse(result.Message.Contains("Exception"));
            }
        }

        [TestMethod]
        public async Task CheckAsync_DocExtension_IsSafelyRejectedWithoutConversion()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(new byte[] { 1 }, ".doc"))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(DocumentReadStatus.UnsupportedFileType, result.DocumentReadStatus);
                StringAssert.Contains(result.Message, "WPS 另存为 DOCX");
            }
        }

        [TestMethod]
        public async Task CheckAsync_LegacyWordBinaryDisguisedAsDocx_ShowsStandardDocxGuidance()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(
                SyntheticCompoundFile.Create("WordDocument", "0Table")))
            {
                string before = ComputeSha256(file.FilePath);
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);
                string after = ComputeSha256(file.FilePath);

                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(DocumentReadStatus.UnsupportedFileType, result.DocumentReadStatus);
                Assert.AreEqual(
                    "当前文件不是标准 DOCX，请使用 WPS 另存为 DOCX 后重新校检。",
                    result.Message);
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public async Task CheckAsync_MissingFile_ReturnsFileNotFound()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".docx");

            ReportCheckResult result = await _service.CheckAsync(path);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(DocumentReadStatus.FileNotFound, result.DocumentReadStatus);
            StringAssert.Contains(result.Message, "文件不存在");
        }

        [TestMethod]
        public async Task CheckAsync_ReadOnlyWorkflow_DoesNotChangeSourceSha256()
        {
            using (SyntheticDocxFile file = CreateMoneyDocument("170万元", "160万元"))
            {
                string before = ComputeSha256(file.FilePath);

                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                string after = ComputeSha256(file.FilePath);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public async Task CheckAsync_RepeatedRun_DoesNotAccumulateIssues()
        {
            using (SyntheticDocxFile file = CreateMoneyDocument("170万元", "160万元"))
            {
                ReportCheckResult first = await _service.CheckAsync(file.FilePath);
                ReportCheckResult second = await _service.CheckAsync(file.FilePath);

                Assert.AreEqual(1, first.TotalCount);
                Assert.AreEqual(1, second.TotalCount);
                Assert.AreNotSame(first.Issues, second.Issues);
            }
        }

        [TestMethod]
        public async Task CheckAsync_SecondDocument_DoesNotContainFirstDocumentIssues()
        {
            using (SyntheticDocxFile conflict = CreateMoneyDocument("170万元", "160万元"))
            using (SyntheticDocxFile consistent = CreateMoneyDocument("170万元", "170万元"))
            {
                ReportCheckResult first = await _service.CheckAsync(conflict.FilePath);
                ReportCheckResult second = await _service.CheckAsync(consistent.FilePath);

                Assert.AreEqual(1, first.TotalCount);
                Assert.AreEqual(0, second.TotalCount);
            }
        }

        [TestMethod]
        public async Task CheckAsync_MoneyParseWarning_IsAvailableInConflictDetails()
        {
            using (SyntheticDocxFile file = CreateMoneyDocument("壹百柒拾万元", "160万元"))
            {
                ReportCheckResult result = await _service.CheckAsync(file.FilePath);

                ReportCheckIssue issue = result.Issues.Single();
                Assert.AreEqual(IssueType.MoneyConflict, issue.IssueType);
                Assert.IsTrue(issue.Occurrences.Any(item => item.Warnings.Count > 0));
                Assert.IsTrue(issue.Occurrences.SelectMany(item => item.Warnings)
                    .Any(item => item.Contains("不规范")));
            }
        }

        [TestMethod]
        public async Task CheckAsync_ProgressReportsRealStagesWithoutFakePercentage()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(Paragraph("项目名称：XX测试项目")))
            {
                var progress = new RecordingProgress();

                ReportCheckResult result = await _service.CheckAsync(file.FilePath, progress);

                Assert.IsTrue(result.IsSuccess);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "正在读取报告……",
                        "正在识别金额……",
                        "正在校检基础信息……",
                        "正在检查文字质量……",
                        "正在生成结果……"
                    },
                    progress.Messages.ToArray());
                Assert.IsFalse(progress.Messages.Any(item => item.Contains("%")));
            }
        }

        private static SyntheticDocxFile CreateMoneyDocument(string firstAmount, string secondAmount)
        {
            return SyntheticDocxFile.CreateWithStyles(
                HeadingStyles(),
                StyledParagraph("Heading1", "报告摘要"),
                Paragraph("本项目不含税评估净值" + firstAmount + "。"),
                StyledParagraph("Heading1", "评估结论"),
                Paragraph("本项目不含税评估净值" + secondAmount + "。"));
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

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private sealed class RecordingProgress : IProgress<string>
        {
            public List<string> Messages { get; } = new List<string>();

            public void Report(string value)
            {
                Messages.Add(value);
            }
        }
    }
}
