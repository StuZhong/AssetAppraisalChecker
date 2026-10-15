using System;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Tests.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class SourceNavigationViewModelTests
    {
        [TestMethod]
        public void NoSelectedIssue_NavigationAndCopyAreDisabled()
        {
            var vm = new MainWindowViewModel(new ResultService(null), new FileLauncher(), clipboard: new TextClipboard());
            Assert.IsFalse(vm.NavigateToSourceCommand.CanExecute(null));
            Assert.IsFalse(vm.CopyIssueDetailsCommand.CanExecute(null));
            Assert.IsFalse(vm.OpenSourceCommand.CanExecute(null));
            Assert.IsFalse(vm.CopySourceSnippetCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SelectingOccurrenceB_NavigatesBAndFallbackOpensB()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            using (var b = SyntheticDocxFile.Create(SourceNavigationTests.P("乙原文。")))
            {
                var sources = new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A"), SourceNavigationTests.Capture(b.FilePath, "文档 B") };
                var nav = new Navigation();
                var launcher = new FileLauncher();
                var vm = await Load(sources, nav, launcher, new TextClipboard());
                vm.SelectedNavigationOccurrence = vm.SelectedIssue.Occurrences[1];
                await vm.NavigateToSourceCommand.ExecuteAsync();
                Assert.AreEqual(b.FilePath, nav.LastSource.FullPath);
                Assert.AreEqual("文档 B", nav.LastOccurrence.DocumentLabel);
                vm.OpenSourceCommand.Execute(null);
                Assert.AreEqual(b.FilePath, launcher.LastPath);
                StringAssert.Contains(vm.NavigationMessage, "不代表已精确定位");
            }
        }

        [TestMethod]
        public async Task CopyDetails_ContainsChineseTypeAuthoritySourcesAndNoInternalIdentity()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            using (var b = SyntheticDocxFile.Create(SourceNavigationTests.P("乙原文。")))
            {
                var sources = new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A"), SourceNavigationTests.Capture(b.FilePath, "文档 B") };
                var clipboard = new TextClipboard();
                var vm = await Load(sources, new Navigation(), new FileLauncher(), clipboard);
                vm.CopyIssueDetailsCommand.Execute(null);
                foreach (string expected in new[] { "问题类型：", "问题类别：语言", "判定级别：建议人工复核", "严重程度：提醒",
                    "文档 A", "文档 B", "结构化位置：", "原文片段：甲原文", "问题说明：合成原因", "建议操作：人工复核" })
                    StringAssert.Contains(clipboard.Text, expected);
                Assert.IsFalse(clipboard.Text.Contains("StableId"));
                Assert.IsFalse(clipboard.Text.Contains("稳定身份："));
                Assert.AreEqual(vm.SelectedIssueDetails, clipboard.Text);
                Assert.AreEqual(IssueAuthority.ReviewSuggested, vm.SelectedIssue.Authority);
                Assert.AreEqual(IssueCategory.Language, vm.SelectedIssue.Category);
            }
        }

        [TestMethod]
        public async Task CopySnippet_UsesSelectedBNotFirstOccurrence()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            using (var b = SyntheticDocxFile.Create(SourceNavigationTests.P("乙原文。")))
            {
                var clipboard = new TextClipboard();
                var vm = await Load(new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A"), SourceNavigationTests.Capture(b.FilePath, "文档 B") },
                    new Navigation(), new FileLauncher(), clipboard);
                vm.SelectedNavigationOccurrence = vm.SelectedIssue.Occurrences[1];
                vm.CopySourceSnippetCommand.Execute(null);
                Assert.AreEqual("乙原文。", clipboard.Text);
            }
        }

        [TestMethod]
        public async Task SingleDocument_UnlabelledOccurrenceUsesOnlyCheckedSource()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            {
                var sources = new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A") };
                var nav = new Navigation();
                var vm = await Load(sources, nav, new FileLauncher(), new TextClipboard(), unlabelled: true);
                await vm.NavigateToSourceAsync();
                Assert.AreEqual(a.FilePath, nav.LastSource.FullPath);
                StringAssert.Contains(vm.SelectedIssueDetails, "文档 A");
            }
        }

        [TestMethod]
        public async Task NoWord_DetailReportsUnavailableRatherThanSuccess()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            {
                var nav = new Navigation { Result = SourceNavigationResult.Unavailable("无法使用 Word 精确定位功能。") };
                var vm = await Load(new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A") }, nav, new FileLauncher(), new TextClipboard());
                await vm.NavigateToSourceAsync();
                StringAssert.Contains(vm.NavigationMessage, "无法定位：无法使用 Word 精确定位功能");
                Assert.IsTrue(vm.OpenSourceCommand.CanExecute(null));
                Assert.IsTrue(vm.CopySourceSnippetCommand.CanExecute(null));
            }
        }

        [TestMethod]
        public async Task ParagraphOnly_HasExplicitManualConfirmationMessage()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            {
                var nav = new Navigation { Result = new SourceNavigationResult(SourceNavigationStatus.ParagraphOnly, "已定位到相关段落，请人工确认。") };
                var vm = await Load(new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A") }, nav, new FileLauncher(), new TextClipboard());
                await vm.NavigateToSourceAsync();
                StringAssert.Contains(vm.NavigationMessage, "段落定位：");
                StringAssert.Contains(vm.NavigationMessage, "人工确认");
                Assert.IsFalse(vm.NavigationMessage.Contains("精确定位："));
            }
        }

        [TestMethod]
        public async Task TableCellAndRow_AreSuccessfulDegradationsNotUnavailable()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("合成定位。")))
            {
                var nav = new Navigation { Result = new SourceNavigationResult(
                    SourceNavigationStatus.TableCell, "已定位到表格 1 / 第 2 行 / 第 3 列，请人工确认。") };
                var vm = await Load(new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A") },
                    nav, new FileLauncher(), new TextClipboard());
                await vm.NavigateToSourceAsync();
                StringAssert.Contains(vm.NavigationMessage, "单元格定位：");
                StringAssert.Contains(vm.NavigationMessage, "人工确认");
                Assert.IsFalse(vm.NavigationMessage.Contains("精确定位："));
                Assert.IsFalse(vm.NavigationMessage.Contains("无法定位："));

                nav.Result = new SourceNavigationResult(SourceNavigationStatus.TableRow,
                    "已定位到表格 1 / 第 2 行，请人工确认具体单元格。");
                await vm.NavigateToSourceAsync();
                StringAssert.Contains(vm.NavigationMessage, "表格行定位：");
                Assert.IsFalse(vm.NavigationMessage.Contains("无法定位："));
            }
        }

        [TestMethod]
        public async Task SelectingAnotherFile_ClearsOldNavigationAndDetails()
        {
            using (var a = SyntheticDocxFile.Create(SourceNavigationTests.P("甲原文。")))
            {
                var vm = await Load(new[] { SourceNavigationTests.Capture(a.FilePath, "文档 A") }, new Navigation(), new FileLauncher(), new TextClipboard());
                vm.TrySelectDocumentA(@"C:\合成\另一份.docx");
                Assert.IsFalse(vm.HasSelectedIssue);
                Assert.IsFalse(vm.NavigateToSourceCommand.CanExecute(null));
                Assert.IsFalse(vm.OpenSourceCommand.CanExecute(null));
                Assert.AreEqual(string.Empty, vm.SelectedIssueDetails);
            }
        }

        private static async Task<MainWindowViewModel> Load(CheckedDocumentSource[] sources, Navigation nav,
            FileLauncher launcher, TextClipboard clipboard, bool unlabelled = false)
        {
            var occurrences = sources.Select(s => SourceNavigationTests.At(s, 0, s.Document.Elements[0].Text,
                "稳定身份：不应复制的内部身份", unlabelled ? string.Empty : s.Label));
            var issue = new ReportCheckIssue("合成问题", IssueType.BasicFieldParseFailed, IssueSeverity.Reminder,
                "合成字段", "合成摘要", "合成原因", "人工复核", occurrences, 0,
                category: IssueCategory.Language, authority: IssueAuthority.ReviewSuggested);
            var result = new ReportCheckResult(sources[0].FullPath, "合成.docx", DateTimeOffset.Now, DateTimeOffset.Now,
                TimeSpan.Zero, DocumentReadStatus.Success, 0, 0, new[] { issue }, true, "合成检查结果",
                documentBFilePath: sources.Length == 2 ? sources[1].FullPath : null, sourceDocuments: sources);
            var vm = new MainWindowViewModel(new ResultService(result), launcher, nav, clipboard);
            vm.TrySelectDocumentA(sources[0].FullPath);
            if (sources.Length == 2) vm.TrySelectDocumentB(sources[1].FullPath);
            await vm.StartCheckAsync();
            // This fixture has no controlled language rule identity. Calibration keeps it
            // in Detailed; navigation/copy must work there with the original evidence.
            Assert.AreEqual(1, vm.RetainedIssueCount);
            Assert.AreEqual(0, vm.Issues.Count);
            vm.SelectedDisplayMode = IssueDisplayMode.Detailed;
            Assert.IsTrue(vm.HasSelectedIssue);
            return vm;
        }

        private sealed class ResultService : IReportCheckService
        {
            private readonly ReportCheckResult _result;
            public ResultService(ReportCheckResult result) { _result = result; }
            public Task<ReportCheckResult> CheckAsync(string path, IProgress<string> progress = null) => Task.FromResult(_result);
            public Task<ReportCheckResult> CheckAsync(ReportCheckRequest request, IProgress<string> progress = null) => Task.FromResult(_result);
        }
        private sealed class Navigation : ISourceNavigationService
        {
            public CheckedDocumentSource LastSource { get; private set; }
            public ReportCheckOccurrence LastOccurrence { get; private set; }
            public SourceNavigationResult Result { get; set; } = new SourceNavigationResult(SourceNavigationStatus.Exact, "模拟结果");
            public Task<SourceNavigationResult> NavigateAsync(CheckedDocumentSource source, ReportCheckOccurrence occurrence)
            { LastSource = source; LastOccurrence = occurrence; return Task.FromResult(Result); }
        }
        private sealed class FileLauncher : IReportFileLauncher
        {
            public string LastPath { get; private set; }
            public ReportFileLaunchResult Open(string path) { LastPath = path; return new ReportFileLaunchResult(true, "已打开"); }
        }
        private sealed class TextClipboard : ITextClipboard
        {
            public string Text { get; private set; }
            public bool TryCopy(string text) { Text = text; return !string.IsNullOrEmpty(text); }
        }
    }
}
