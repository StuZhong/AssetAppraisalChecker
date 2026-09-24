using System.Linq;
using System.IO;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class DesktopPresentationTests
    {
        [TestMethod]
        public void Format_ParagraphLocation_UsesHeadingPathAndOneBasedIndex()
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                11,
                null,
                null,
                null,
                new[] { "评估结论", "评估结果" });

            string text = DocumentLocationFormatter.Format(location);

            Assert.AreEqual("评估结论 > 评估结果 > 正文段落 12", text);
            Assert.IsFalse(text.Contains("页"));
        }

        [TestMethod]
        public void Format_TableLocation_ConvertsAllIndexesToOneBased()
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                null,
                1,
                2,
                3,
                null);

            string text = DocumentLocationFormatter.Format(location);

            Assert.AreEqual("表格 2 / 第 3 行 / 第 4 列", text);
        }

        [TestMethod]
        public void Format_LocationWithoutIndexes_ReturnsNonBlankFallback()
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                null,
                null,
                null,
                null,
                null);

            Assert.AreEqual("文档正文", DocumentLocationFormatter.Format(location));
        }

        [TestMethod]
        public void OccurrenceViewModel_SuspectedOutlierUsesNeutralWording()
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                0,
                null,
                null,
                null,
                null);
            var occurrence = new ReportCheckOccurrence(
                "160万元",
                "1,600,000.00 元",
                location,
                "正文段落 1",
                "评估结论",
                "当前项目",
                new[] { "明确字段证据" },
                new string[0],
                "当前：本项目评估净值160万元。",
                true);

            var viewModel = new OccurrenceViewModel(occurrence, 1);

            Assert.AreEqual("疑似异常", viewModel.OutlierDisplay);
            Assert.IsFalse(viewModel.OutlierDisplay.Contains("错误值"));
        }

        [TestMethod]
        public void IssueViewModel_ExposesChineseSeverityAndIssueTypeText()
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                0,
                null,
                null,
                null,
                null);
            var occurrence = new ReportCheckOccurrence(
                "原始值",
                "标准值",
                location,
                "正文段落 1",
                "正文",
                string.Empty,
                new string[0],
                new string[0],
                string.Empty,
                false);
            var issue = new ReportCheckIssue(
                "BASIC-TEST",
                IssueType.BasicFieldConflict,
                IssueSeverity.Serious,
                "评估基准日",
                "两处日期不一致",
                "可解释原因",
                "请人工复核。",
                new[] { occurrence },
                0);

            var viewModel = new IssueViewModel(issue);

            Assert.AreEqual("严重", viewModel.SeverityDisplay);
            Assert.AreEqual("基础信息冲突", viewModel.IssueTypeDisplay);
            Assert.AreEqual("正文段落 1", viewModel.PrimaryLocation);
        }

        [TestMethod]
        public void IssueViewModel_ProjectNameExpectedMismatch_UsesSpecificChineseNameAndPreservesDetails()
        {
            ReportCheckOccurrence occurrence = CreateOccurrence("封面 > 正文段落 2");
            var issue = new ReportCheckIssue(
                "PROJECT-NAME-EXPECTED-COVER-01",
                IssueType.ProjectNameExpectedMismatch,
                IssueSeverity.Serious,
                "项目名称",
                "封面项目名称与输入的正确项目名称不一致",
                "虚构项目名称不一致。",
                "请人工复核。",
                new[] { occurrence },
                0);

            var viewModel = new IssueViewModel(issue);

            Assert.AreEqual("项目名称不一致", viewModel.IssueTypeDisplay);
            Assert.AreEqual(IssueSeverity.Serious, viewModel.Severity);
            Assert.AreEqual("封面 > 正文段落 2", viewModel.PrimaryLocation);
            Assert.AreEqual(1, viewModel.Occurrences.Count);
            StringAssert.Contains(viewModel.Occurrences[0].EvidenceText, "虚构分类证据");
        }

        [TestMethod]
        public void IssueViewModel_ThreeOccurrences_AggregatesAllLocationsInCurrentOrder()
        {
            ReportCheckOccurrence summary = CreateOccurrence("资产评估报告摘要");
            ReportCheckOccurrence conclusion = CreateOccurrence("资产评估报告评估结论");
            ReportCheckOccurrence resultTable = CreateOccurrence("资产评估结果表");
            var issue = new ReportCheckIssue(
                "MONEY-LOCATION-TEST",
                IssueType.MoneyConflict,
                IssueSeverity.Serious,
                "不含税评估净值",
                "三个虚构位置金额不一致",
                "虚构金额存在冲突。",
                "请人工复核。",
                new[] { summary, conclusion, resultTable },
                0);

            var viewModel = new IssueViewModel(issue);

            Assert.AreEqual(
                "共3处：资产评估报告摘要、资产评估报告评估结论、资产评估结果表",
                viewModel.PrimaryLocation);
            Assert.AreEqual(3, viewModel.Occurrences.Count);
            Assert.AreEqual("资产评估报告摘要", viewModel.Occurrences[0].LocationText);
            Assert.AreEqual("资产评估报告评估结论", viewModel.Occurrences[1].LocationText);
            Assert.AreEqual("资产评估结果表", viewModel.Occurrences[2].LocationText);
        }

        [TestMethod]
        public void IssueViewModel_OneOccurrence_DisplaysSingleLocationWithoutCountPrefix()
        {
            ReportCheckOccurrence occurrence = CreateOccurrence("评估结论 > 正文段落 3");
            var issue = new ReportCheckIssue(
                "BASIC-LOCATION-TEST",
                IssueType.BasicFieldConflict,
                IssueSeverity.Serious,
                "评估基准日",
                "虚构日期不一致",
                "虚构基础字段存在冲突。",
                "请人工复核。",
                new[] { occurrence },
                0);

            var viewModel = new IssueViewModel(issue);

            Assert.AreEqual("评估结论 > 正文段落 3", viewModel.PrimaryLocation);
            Assert.AreEqual(1, viewModel.Occurrences.Count);
        }

        [TestMethod]
        public void IssueViewModel_NoOccurrence_UsesExistingSafeFallback()
        {
            var issue = new ReportCheckIssue(
                "EMPTY-LOCATION-TEST",
                IssueType.BasicFieldParseFailed,
                IssueSeverity.Reminder,
                "报告编号",
                "虚构解析提醒",
                "没有可显示的位置。",
                "请人工复核。",
                new ReportCheckOccurrence[0],
                0);

            var viewModel = new IssueViewModel(issue);

            Assert.AreEqual("文档正文", viewModel.PrimaryLocation);
            Assert.AreEqual(0, viewModel.Occurrences.Count);
        }

        [TestMethod]
        public void StatusMessages_AllReadFailuresAreChineseAndDoNotExposeStackTrace()
        {
            DocumentReadStatus[] statuses =
            {
                DocumentReadStatus.FileNotFound,
                DocumentReadStatus.UnsupportedFileType,
                DocumentReadStatus.InvalidDocx,
                DocumentReadStatus.EncryptedOrProtected,
                DocumentReadStatus.AccessDenied,
                DocumentReadStatus.FileInUse,
                DocumentReadStatus.IoError,
                DocumentReadStatus.UnexpectedError
            };

            string[] messages = statuses
                .Select(DocumentReadStatusMessageProvider.GetMessage)
                .ToArray();

            Assert.IsTrue(messages.All(item => !string.IsNullOrWhiteSpace(item)));
            Assert.IsTrue(messages.All(item => !item.Contains("Exception")));
            Assert.IsTrue(messages.All(item => !item.Contains("StackTrace")));
        }

        [TestMethod]
        public void StatusMessage_UnsupportedFileType_RequestsStandardDocxSaveAs()
        {
            Assert.AreEqual(
                "当前文件不是标准 DOCX，请使用 WPS 另存为 DOCX 后重新校检。",
                DocumentReadStatusMessageProvider.GetMessage(DocumentReadStatus.UnsupportedFileType));
        }

        [TestMethod]
        public void FileLauncher_DocPathIsRejectedWithoutStartingAnyApplication()
        {
            ReportFileLaunchResult result = new WindowsReportFileLauncher().Open("虚构旧版报告.doc");

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, ".docx");
        }

        [TestMethod]
        public void FileLauncher_MissingDocxReturnsSafeMessage()
        {
            string path = Path.Combine(Path.GetTempPath(), "不存在-" + System.Guid.NewGuid().ToString("N") + ".docx");

            ReportFileLaunchResult result = new WindowsReportFileLauncher().Open(path);

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains(result.Message, "文件不存在");
        }

        private static ReportCheckOccurrence CreateOccurrence(string locationText)
        {
            return new ReportCheckOccurrence(
                "虚构原始值",
                "虚构标准值",
                new DocumentLocation(
                    DocumentPart.MainBody,
                    0,
                    null,
                    null,
                    null,
                    null),
                locationText,
                "虚构区域",
                string.Empty,
                new[] { "虚构分类证据" },
                new string[0],
                "虚构附近文本",
                false);
        }
    }
}
