using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class MainWindowViewModelTests
    {
        [TestMethod]
        public void Constructor_InitialStateHasNoSelectedFileAndCannotStart()
        {
            MainWindowViewModel viewModel = CreateViewModel();

            Assert.AreEqual("尚未选择文档 A", viewModel.SelectedFileName);
            Assert.AreEqual("尚未选择文档 A", viewModel.StatusMessage);
            Assert.IsFalse(viewModel.HasSelectedFile);
            Assert.IsFalse(viewModel.CanStartCheck);
            Assert.IsFalse(viewModel.StartCheckCommand.CanExecute(null));
            Assert.AreEqual(0, viewModel.TotalCount);
        }

        [TestMethod]
        public void Constructor_ExpectedProjectNameStartsEmpty()
        {
            MainWindowViewModel viewModel = CreateViewModel();

            Assert.IsTrue(string.IsNullOrEmpty(viewModel.ExpectedProjectName));
        }

        [TestMethod]
        public async Task StartCheckAsync_ValidExpectedProjectName_PassesExplicitRequest()
        {
            var service = new FakeReportCheckService();
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";

            await viewModel.StartCheckAsync();

            Assert.AreEqual(1, service.InvocationCount);
            Assert.IsNotNull(service.LastRequest);
            Assert.AreEqual(@"C:\虚构目录\报告.docx", service.LastRequest.FilePath);
            Assert.AreEqual("虚构正确项目名称", service.LastRequest.ExpectedProjectName);
        }

        [TestMethod]
        public async Task StartCheckAsync_EmptyExpectedProjectName_AllowsOptionalAuthority()
        {
            var service = new FakeReportCheckService();
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");

            await viewModel.StartCheckAsync();

            Assert.AreEqual(1, service.InvocationCount);
            Assert.IsNotNull(service.LastRequest);
            Assert.IsTrue(string.IsNullOrEmpty(service.LastRequest.ExpectedProjectName));
            Assert.IsFalse(viewModel.IsBusy);
        }

        [TestMethod]
        public async Task StartCheckAsync_WhitespaceExpectedProjectName_AllowsOptionalAuthority()
        {
            var service = new FakeReportCheckService();
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = " \t ";

            await viewModel.StartCheckAsync();

            Assert.AreEqual(1, service.InvocationCount);
            Assert.AreEqual(" \t ", service.LastRequest.ExpectedProjectName);
            Assert.IsFalse(viewModel.IsBusy);
        }

        [TestMethod]
        public async Task StartCheckAsync_DocumentBSelected_PassesNeutralDocumentPair()
        {
            var service = new FakeReportCheckService();
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectDocumentA(@"C:\虚构目录\文档A.docx");
            viewModel.TrySelectDocumentB(@"C:\虚构目录\文档B.docx");

            await viewModel.StartCheckAsync();

            Assert.AreEqual(@"C:\虚构目录\文档A.docx", service.LastRequest.DocumentAPath);
            Assert.AreEqual(@"C:\虚构目录\文档B.docx", service.LastRequest.DocumentBPath);
            Assert.IsTrue(service.LastRequest.HasDocumentB);
        }

        [TestMethod]
        public void TrySelectFile_AnotherDocumentClearsExpectedProjectName()
        {
            MainWindowViewModel viewModel = CreateViewModel();
            viewModel.TrySelectFile(@"C:\虚构目录\第一份报告.docx");
            viewModel.ExpectedProjectName = "第一份虚构项目名称";

            viewModel.TrySelectFile(@"C:\虚构目录\第二份报告.docx");

            Assert.IsTrue(string.IsNullOrEmpty(viewModel.ExpectedProjectName));
        }

        [TestMethod]
        public void TrySelectFile_DocxEnablesStartAndShowsFullPath()
        {
            MainWindowViewModel viewModel = CreateViewModel();

            bool selected = viewModel.TrySelectFile(@"C:\虚构目录\XX测试报告.docx");

            Assert.IsTrue(selected);
            Assert.AreEqual("XX测试报告.docx", viewModel.SelectedFileName);
            Assert.AreEqual(@"C:\虚构目录\XX测试报告.docx", viewModel.SelectedFilePath);
            Assert.IsTrue(viewModel.CanStartCheck);
            Assert.IsTrue(viewModel.StartCheckCommand.CanExecute(null));
        }

        [TestMethod]
        public void ReportFileDialogFilter_IncludesDocx()
        {
            StringAssert.Contains(
                AssetAppraisalChecker.Desktop.MainWindow.ReportFileDialogFilter,
                "*.docx");
        }

        [TestMethod]
        public void ReportFileDialogFilter_IncludesDoc()
        {
            const string filter = AssetAppraisalChecker.Desktop.MainWindow.ReportFileDialogFilter;

            StringAssert.Contains(filter, ";*.doc");
            Assert.IsTrue(filter.EndsWith("*.doc", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TrySelectFile_DocIsRejectedWithExplicitSaveAsGuidance()
        {
            MainWindowViewModel viewModel = CreateViewModel();

            bool selected = viewModel.TrySelectFile(@"C:\虚构目录\旧版报告.doc");

            Assert.IsFalse(selected);
            Assert.IsFalse(viewModel.HasSelectedFile);
            Assert.IsFalse(viewModel.CanStartCheck);
            Assert.AreEqual(
                "当前仅支持 DOCX 文件，请使用 WPS 或 Word 另存为 DOCX 后重新检查。",
                viewModel.StatusMessage);
            Assert.AreEqual(viewModel.StatusMessage, viewModel.ResultMessage);
        }

        [TestMethod]
        public async Task StartCheckAsync_WhilePendingSetsBusyAndDisablesRepeatedStart()
        {
            var completion = new TaskCompletionSource<ReportCheckResult>();
            var service = new FakeReportCheckService
            {
                Handler = (path, progress) => completion.Task
            };
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";

            Task check = viewModel.StartCheckAsync();

            Assert.IsTrue(viewModel.IsBusy);
            Assert.IsFalse(viewModel.CanStartCheck);
            Assert.IsFalse(viewModel.StartCheckCommand.CanExecute(null));
            completion.SetResult(SuccessResult(new ReportCheckIssue[0]));
            await check;
            Assert.IsFalse(viewModel.IsBusy);
        }

        [TestMethod]
        public async Task StartCheckAsync_WhenCompletedRestoresBusyState()
        {
            MainWindowViewModel viewModel = CreateViewModel(new FakeReportCheckService
            {
                Handler = (path, progress) => Task.FromResult(SuccessResult(new ReportCheckIssue[0]))
            });
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";

            await viewModel.StartCheckAsync();

            Assert.IsFalse(viewModel.IsBusy);
            Assert.IsTrue(viewModel.CanStartCheck);
            Assert.AreEqual("未发现当前规则能够确认的一致性问题。", viewModel.StatusMessage);
        }

        [TestMethod]
        public async Task ApplyResult_UpdatesAllSeverityCountsAndTotal()
        {
            ReportCheckIssue[] issues =
            {
                CreateIssue("S", IssueSeverity.Serious, 0),
                CreateIssue("N", IssueSeverity.Normal, 1),
                CreateIssue("R", IssueSeverity.Reminder, 2)
            };
            MainWindowViewModel viewModel = CreateForResult(issues);

            await viewModel.StartCheckAsync();

            Assert.AreEqual(1, viewModel.SeriousCount);
            Assert.AreEqual(1, viewModel.NormalCount);
            Assert.AreEqual(1, viewModel.ReminderCount);
            Assert.AreEqual(3, viewModel.TotalCount);
        }

        [TestMethod]
        public async Task ApplyResult_DefaultSortIsSeriousThenNormalThenReminder()
        {
            ReportCheckIssue[] issues =
            {
                CreateIssue("R", IssueSeverity.Reminder, 0),
                CreateIssue("N", IssueSeverity.Normal, 1),
                CreateIssue("S", IssueSeverity.Serious, 2)
            };
            MainWindowViewModel viewModel = CreateForResult(issues);

            await viewModel.StartCheckAsync();

            CollectionAssert.AreEqual(
                new[] { IssueSeverity.Serious, IssueSeverity.Normal, IssueSeverity.Reminder },
                viewModel.Issues.Select(item => item.Severity).ToArray());
        }

        [TestMethod]
        public async Task ApplyResult_SameSeverityKeepsStableSourceOrder()
        {
            ReportCheckIssue[] issues =
            {
                CreateIssue("second", IssueSeverity.Serious, 2),
                CreateIssue("first", IssueSeverity.Serious, 1)
            };
            MainWindowViewModel viewModel = CreateForResult(issues);

            await viewModel.StartCheckAsync();

            CollectionAssert.AreEqual(
                new[] { "first", "second" },
                viewModel.Issues.Select(item => item.IssueId).ToArray());
        }

        [TestMethod]
        public async Task Filter_AllShowsEveryExistingIssue()
        {
            MainWindowViewModel viewModel = CreateForResult(AllSeverityIssues());
            await viewModel.StartCheckAsync();

            viewModel.SelectedFilter = IssueSeverityFilter.All;

            Assert.AreEqual(3, viewModel.Issues.Count);
        }

        [TestMethod]
        public async Task Filter_SeriousShowsOnlySeriousIssues()
        {
            MainWindowViewModel viewModel = CreateForResult(AllSeverityIssues());
            await viewModel.StartCheckAsync();

            viewModel.SelectedFilter = IssueSeverityFilter.Serious;

            Assert.AreEqual(1, viewModel.Issues.Count);
            Assert.AreEqual(IssueSeverity.Serious, viewModel.Issues.Single().Severity);
        }

        [TestMethod]
        public async Task Filter_NormalShowsOnlyNormalIssues()
        {
            MainWindowViewModel viewModel = CreateForResult(AllSeverityIssues());
            await viewModel.StartCheckAsync();

            viewModel.SelectedFilter = IssueSeverityFilter.Normal;

            Assert.AreEqual(1, viewModel.Issues.Count);
            Assert.AreEqual(IssueSeverity.Normal, viewModel.Issues.Single().Severity);
        }

        [TestMethod]
        public async Task Filter_ReminderShowsOnlyReminderIssues()
        {
            MainWindowViewModel viewModel = CreateForResult(AllSeverityIssues());
            await viewModel.StartCheckAsync();

            viewModel.SelectedFilter = IssueSeverityFilter.Reminder;

            Assert.AreEqual(1, viewModel.Issues.Count);
            Assert.AreEqual(IssueSeverity.Reminder, viewModel.Issues.Single().Severity);
        }

        [TestMethod]
        public async Task Filter_UsesInMemoryIssuesWithoutRerunningService()
        {
            var service = new FakeReportCheckService
            {
                Handler = (path, progress) => Task.FromResult(SuccessResult(AllSeverityIssues()))
            };
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";
            await viewModel.StartCheckAsync();

            viewModel.SelectedFilter = IssueSeverityFilter.Serious;
            viewModel.SelectedFilter = IssueSeverityFilter.Normal;
            viewModel.SelectedFilter = IssueSeverityFilter.All;

            Assert.AreEqual(1, service.InvocationCount);
        }

        [TestMethod]
        public async Task SelectedIssue_UpdatesDetailObject()
        {
            MainWindowViewModel viewModel = CreateForResult(new[]
            {
                CreateIssue("A", IssueSeverity.Serious, 0, "字段A", "原始值A"),
                CreateIssue("B", IssueSeverity.Normal, 1, "字段B", "原始值B")
            });
            await viewModel.StartCheckAsync();

            viewModel.SelectedIssue = viewModel.Issues[1];

            Assert.IsTrue(viewModel.HasSelectedIssue);
            Assert.AreEqual("字段B", viewModel.SelectedIssue.FieldName);
            Assert.AreEqual("原始值B", viewModel.SelectedIssue.Occurrences.Single().RawText);
        }

        [TestMethod]
        public async Task TrySelectFile_AnotherDocumentClearsOldIssuesSummaryAndDetails()
        {
            MainWindowViewModel viewModel = CreateForResult(AllSeverityIssues());
            await viewModel.StartCheckAsync();

            viewModel.TrySelectFile(@"C:\虚构目录\第二份报告.docx");

            Assert.AreEqual(0, viewModel.Issues.Count);
            Assert.AreEqual(0, viewModel.TotalCount);
            Assert.IsNull(viewModel.SelectedIssue);
            Assert.AreEqual("尚未执行校检。", viewModel.ResultMessage);
        }

        [TestMethod]
        public async Task StartCheckAsync_RepeatedRunReplacesRatherThanAccumulates()
        {
            var service = new FakeReportCheckService
            {
                Handler = (path, progress) => Task.FromResult(SuccessResult(new[]
                {
                    CreateIssue("one", IssueSeverity.Serious, 0)
                }))
            };
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";

            await viewModel.StartCheckAsync();
            await viewModel.StartCheckAsync();

            Assert.AreEqual(2, service.InvocationCount);
            Assert.AreEqual(1, viewModel.TotalCount);
            Assert.AreEqual(1, viewModel.Issues.Count);
        }

        [TestMethod]
        public async Task StartCheckAsync_ReadFailureShowsSafeStatusAndNoIssues()
        {
            var service = new FakeReportCheckService
            {
                Handler = (path, progress) => Task.FromResult(FailedResult(
                    DocumentReadStatus.InvalidDocx,
                    "文件不是有效的 DOCX 或文件已损坏。原报告未被修改。"))
            };
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\损坏报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";

            await viewModel.StartCheckAsync();

            Assert.AreEqual(0, viewModel.TotalCount);
            Assert.AreEqual(0, viewModel.Issues.Count);
            StringAssert.Contains(viewModel.StatusMessage, "文件已损坏");
            Assert.IsFalse(viewModel.StatusMessage.Contains("StackTrace"));
        }

        [TestMethod]
        public void OpenReportCommand_UsesOnlyCurrentlySelectedPath()
        {
            var launcher = new FakeReportFileLauncher();
            MainWindowViewModel viewModel = CreateViewModel(
                new FakeReportCheckService(),
                launcher);
            viewModel.TrySelectFile(@"C:\虚构目录\当前报告.docx");

            viewModel.OpenReportCommand.Execute(null);

            Assert.AreEqual(@"C:\虚构目录\当前报告.docx", launcher.LastPath);
            Assert.AreEqual("虚构打开成功。", viewModel.StatusMessage);
        }

        [TestMethod]
        public async Task DisplayMode_DefaultFocusRetainsAuxiliaryRecordForDetailedReview()
        {
            ReportCheckIssue business = CreateIssue("BUS", IssueSeverity.Reminder, 0);
            var location = new DocumentLocation(DocumentPart.MainBody, 1, null, null, null, null);
            var occurrence = new ReportCheckOccurrence(
                "合成段落 ", string.Empty, location, "正文段落 2", "正文", string.Empty,
                new[] { "字符偏移=4；长度=1" }, new string[0], string.Empty, false,
                null, "文档 A");
            var layout = new ReportCheckIssue(
                "LAYOUT", IssueType.BasicFieldParseFailed, IssueSeverity.Reminder,
                "空格与字符", "段末空格", "排版提醒", "人工复核", new[] { occurrence }, 1,
                category: IssueCategory.Mechanical, governanceKind: "TrailingSpace");
            MainWindowViewModel viewModel = CreateForResult(new[] { business, layout });

            await viewModel.StartCheckAsync();

            Assert.AreEqual(IssueDisplayMode.Focused, viewModel.SelectedDisplayMode);
            Assert.AreEqual(1, viewModel.TotalCount);
            Assert.AreEqual(1, viewModel.AuxiliaryCount);
            Assert.AreEqual(2, viewModel.RetainedIssueCount);
            Assert.AreEqual("BUS", viewModel.Issues.Single().IssueId);
            StringAssert.Contains(viewModel.DisplayModeDescription, "并非已修复");

            viewModel.SelectedDisplayMode = IssueDisplayMode.Detailed;
            Assert.AreEqual(2, viewModel.TotalCount);
            Assert.AreEqual(2, viewModel.Issues.Count);

            viewModel.SelectedDisplayMode = IssueDisplayMode.Focused;
            Assert.AreEqual(1, viewModel.TotalCount);
            Assert.AreEqual(2, viewModel.RetainedIssueCount);
        }

        [TestMethod]
        public async Task DisplayMode_NormalStatisticalIssueRetainsEvidenceAndDetailedCounts()
        {
            ReportCheckIssue business = CreateIssue("BUS", IssueSeverity.Serious, 0);
            var occurrence = new ReportCheckOccurrence(
                "合成正文", string.Empty, new DocumentLocation(DocumentPart.MainBody, 2, null, null, null, null),
                "正文段落 3", "正文", string.Empty, new[] { "合成 peer 证据" }, new string[0],
                string.Empty, false, null, "文档 B");
            var formatting = new ReportCheckIssue("FORMAT", IssueType.BasicFieldParseFailed, IssueSeverity.Normal,
                "段落格式", "疑似格式异常", "多数 peer 推断", "人工复核", new[] { occurrence }, 1,
                category: IssueCategory.Formatting, authority: IssueAuthority.HighConfidence,
                governanceKind: "ParagraphFormatOutlier");
            MainWindowViewModel viewModel = CreateForResult(new[] { business, formatting });
            await viewModel.StartCheckAsync();
            Assert.AreEqual(1, viewModel.TotalCount);
            Assert.AreEqual(0, viewModel.NormalCount);
            Assert.AreEqual(1, viewModel.AuxiliaryCount);
            Assert.AreEqual(2, viewModel.RetainedIssueCount);
            viewModel.SelectedDisplayMode = IssueDisplayMode.Detailed;
            Assert.AreEqual(2, viewModel.TotalCount);
            Assert.AreEqual(1, viewModel.NormalCount);
            viewModel.SelectedIssue = viewModel.Issues.Single(item => item.IssueId == "FORMAT");
            StringAssert.Contains(viewModel.SelectedIssueDetails, "合成 peer 证据");
            Assert.AreSame(occurrence, viewModel.SelectedIssue.Occurrences[0].SourceOccurrence);
            Assert.IsTrue(viewModel.CopyIssueDetailsCommand.CanExecute(null));
            viewModel.SelectedDisplayMode = IssueDisplayMode.Focused;
            Assert.AreEqual(1, viewModel.TotalCount);
            Assert.AreEqual(2, viewModel.RetainedIssueCount);
            StringAssert.Contains(viewModel.DisplayModeDescription, "并非已修复");
        }

        [TestMethod]
        public void OpenDocumentBCommand_UsesOnlyDocumentBPath()
        {
            var launcher = new FakeReportFileLauncher();
            MainWindowViewModel viewModel = CreateViewModel(
                new FakeReportCheckService(),
                launcher);
            viewModel.TrySelectDocumentA(@"C:\虚构目录\文档A.docx");
            viewModel.TrySelectDocumentB(@"C:\虚构目录\文档B.docx");

            viewModel.OpenDocumentBCommand.Execute(null);

            Assert.AreEqual(@"C:\虚构目录\文档B.docx", launcher.LastPath);
        }

        private static MainWindowViewModel CreateForResult(IEnumerable<ReportCheckIssue> issues)
        {
            var service = new FakeReportCheckService
            {
                Handler = (path, progress) => Task.FromResult(SuccessResult(issues))
            };
            MainWindowViewModel viewModel = CreateViewModel(service);
            viewModel.TrySelectFile(@"C:\虚构目录\报告.docx");
            viewModel.ExpectedProjectName = "虚构正确项目名称";
            return viewModel;
        }

        private static MainWindowViewModel CreateViewModel(
            FakeReportCheckService service = null,
            IReportFileLauncher launcher = null)
        {
            return new MainWindowViewModel(
                service ?? new FakeReportCheckService(),
                launcher ?? new FakeReportFileLauncher());
        }

        private static ReportCheckIssue[] AllSeverityIssues()
        {
            return new[]
            {
                CreateIssue("S", IssueSeverity.Serious, 0),
                CreateIssue("N", IssueSeverity.Normal, 1),
                CreateIssue("R", IssueSeverity.Reminder, 2)
            };
        }

        private static ReportCheckIssue CreateIssue(
            string issueId,
            IssueSeverity severity,
            int sourceOrder,
            string fieldName = "测试字段",
            string rawText = "虚构原始值")
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                sourceOrder,
                null,
                null,
                null,
                null);
            var occurrence = new ReportCheckOccurrence(
                rawText,
                "虚构标准值",
                location,
                "正文段落 " + (sourceOrder + 1),
                "正文",
                string.Empty,
                new[] { "虚构分类证据" },
                new string[0],
                "当前：虚构上下文",
                false);
            return new ReportCheckIssue(
                issueId,
                IssueType.BasicFieldConflict,
                severity,
                fieldName,
                "虚构问题摘要",
                "虚构且可解释的问题原因",
                "请人工复核。",
                new[] { occurrence },
                sourceOrder);
        }

        private static ReportCheckResult SuccessResult(IEnumerable<ReportCheckIssue> issues)
        {
            List<ReportCheckIssue> issueList = issues.ToList();
            return new ReportCheckResult(
                @"C:\虚构目录\报告.docx",
                "报告.docx",
                DateTimeOffset.Now,
                DateTimeOffset.Now,
                TimeSpan.Zero,
                DocumentReadStatus.Success,
                0,
                0,
                issueList,
                true,
                issueList.Count == 0
                    ? "未发现当前规则能够确认的一致性问题。"
                    : "校检完成，共发现需要关注的问题。");
        }

        private static ReportCheckResult FailedResult(DocumentReadStatus status, string message)
        {
            return new ReportCheckResult(
                @"C:\虚构目录\报告.docx",
                "报告.docx",
                DateTimeOffset.Now,
                DateTimeOffset.Now,
                TimeSpan.Zero,
                status,
                0,
                0,
                new ReportCheckIssue[0],
                false,
                message);
        }

        private sealed class FakeReportCheckService : IReportCheckService
        {
            public Func<string, IProgress<string>, Task<ReportCheckResult>> Handler { get; set; }

            public int InvocationCount { get; private set; }

            public ReportCheckRequest LastRequest { get; private set; }

            public Task<ReportCheckResult> CheckAsync(
                string filePath,
                IProgress<string> progress = null)
            {
                InvocationCount++;
                return Handler == null
                    ? Task.FromResult(SuccessResult(new ReportCheckIssue[0]))
                    : Handler(filePath, progress);
            }

            public Task<ReportCheckResult> CheckAsync(
                ReportCheckRequest request,
                IProgress<string> progress = null)
            {
                if (request == null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                LastRequest = request;
                return CheckAsync(request.FilePath, progress);
            }
        }

        private sealed class FakeReportFileLauncher : IReportFileLauncher
        {
            public string LastPath { get; private set; }

            public ReportFileLaunchResult Open(string filePath)
            {
                LastPath = filePath;
                return new ReportFileLaunchResult(true, "虚构打开成功。");
            }
        }
    }
}
