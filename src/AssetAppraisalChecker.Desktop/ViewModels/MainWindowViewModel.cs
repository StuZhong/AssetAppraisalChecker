using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Infrastructure;
using AssetAppraisalChecker.Desktop.Services;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly IReportCheckService _reportCheckService;
        private readonly IReportFileLauncher _reportFileLauncher;
        private readonly ISourceNavigationService _navigation;
        private readonly ITextClipboard _clipboard;
        private IReadOnlyList<CheckedDocumentSource> _sources = new CheckedDocumentSource[0];
        private OccurrenceViewModel _selectedNavigationOccurrence;
        private string _navigationMessage = string.Empty;
        private bool _isNavigating;
        private readonly List<IssueViewModel> _allIssues = new List<IssueViewModel>();
        private string _selectedFilePath = string.Empty;
        private string _selectedFileName = "尚未选择文档 A";
        private string _documentBPath = string.Empty;
        private string _documentBFileName = "尚未选择（可选）";
        private bool _isV3Mode;
        private string _excelPath = string.Empty;
        private string _excelFileName = "尚未选择 Excel 工作簿";
        private string _comparisonSummary = string.Empty;
        private string _statusMessage = "尚未选择文档 A";
        private string _resultMessage = "尚未执行校检。";
        private string _expectedProjectName = string.Empty;
        private bool _isBusy;
        private int _seriousCount;
        private int _normalCount;
        private int _reminderCount;
        private IssueSeverityFilter _selectedFilter = IssueSeverityFilter.All;
        private IssueDisplayMode _selectedDisplayMode = IssueDisplayMode.Focused;
        private string _lastCheckMessage = string.Empty;
        private IssueViewModel _selectedIssue;

        public MainWindowViewModel(
            IReportCheckService reportCheckService,
            IReportFileLauncher reportFileLauncher,
            ISourceNavigationService navigation = null,
            ITextClipboard clipboard = null)
        {
            _reportCheckService = reportCheckService ??
                                  throw new ArgumentNullException(nameof(reportCheckService));
            _reportFileLauncher = reportFileLauncher ??
                                  throw new ArgumentNullException(nameof(reportFileLauncher));
            _navigation = navigation ?? new SourceNavigationService();
            _clipboard = clipboard ?? new WindowsTextClipboard();
            Issues = new ObservableCollection<IssueViewModel>();
            FilterOptions = new ReadOnlyCollection<IssueFilterOption>(new[]
            {
                new IssueFilterOption(IssueSeverityFilter.All, "全部"),
                new IssueFilterOption(IssueSeverityFilter.Serious, "严重"),
                new IssueFilterOption(IssueSeverityFilter.Normal, "一般"),
                new IssueFilterOption(IssueSeverityFilter.Reminder, "提醒")
            });
            DisplayModeOptions = new ReadOnlyCollection<IssueDisplayModeOption>(new[]
            {
                new IssueDisplayModeOption(IssueDisplayMode.Focused, "重点质控"),
                new IssueDisplayModeOption(IssueDisplayMode.Detailed, "详细检查")
            });
            StartCheckCommand = new AsyncRelayCommand(StartCheckAsync, () => CanStartCheck);
            OpenDocumentACommand = new RelayCommand(
                _ => OpenDocument(SelectedFilePath),
                _ => CanOpenDocumentA);
            OpenDocumentBCommand = new RelayCommand(
                _ => OpenDocument(DocumentBPath),
                _ => CanOpenDocumentB);
            OpenReportCommand = OpenDocumentACommand;
            NavigateToSourceCommand = new AsyncRelayCommand(NavigateToSourceAsync,
                () => SelectedNavigationOccurrence != null && !IsBusy && !IsNavigating);
            CopyIssueDetailsCommand = new RelayCommand(_ => CopyText(SelectedIssueDetails, "问题详情"), _ => HasSelectedIssue);
            CopySourceSnippetCommand = new RelayCommand(_ => CopyText(SelectedNavigationOccurrence?.RawText, "原文片段"),
                _ => !string.IsNullOrEmpty(SelectedNavigationOccurrence?.RawText));
            OpenSourceCommand = new RelayCommand(_ => OpenSource(), _ => NavigationSource != null && !IsBusy && !IsNavigating);
        }

        public ObservableCollection<IssueViewModel> Issues { get; }

        public string AiStatusMessage => IsV3Mode ? "V3 使用确定性金额规则，无需离线 AI。"
            : IsBusy ? "本次校检进行中，AI 实际执行状态待确认……"
            : ((_reportCheckService as ILocalAiStatusProvider)?.AiStatusMessage
                ?? "规则校检模式；未接入本地 AI。");

        public bool IsV3Mode
        {
            get => _isV3Mode;
            set
            {
                if (IsBusy || IsNavigating || !SetProperty(ref _isV3Mode, value)) return;
                ClearResultState();
                OnPropertyChanged(nameof(IsWordMode));
                OnPropertyChanged(nameof(DocumentARole));
                OnPropertyChanged(nameof(DocumentBRole));
                OnPropertyChanged(nameof(DocumentAFileName));
                OnPropertyChanged(nameof(DocumentBFileName));
                OnPropertyChanged(nameof(SelectDocumentAText));
                OnPropertyChanged(nameof(SelectDocumentBText));
                OnPropertyChanged(nameof(OpenDocumentAText));
                OnPropertyChanged(nameof(OpenDocumentBText));
                OnPropertyChanged(nameof(AiStatusMessage));
                RefreshStartCheck();
                StatusMessage = value ? "V3：请选择资产评估报告、资产评估说明和 Excel 汇总工作簿。"
                    : "Word 校检：选择文档 A；文档 B 为可选项。";
                ResultMessage = "尚未执行校检。";
            }
        }

        public bool IsWordMode { get => !IsV3Mode; set => IsV3Mode = !value; }
        public string DocumentARole => IsV3Mode ? "资产评估报告：" : "文档 A：";
        public string DocumentBRole => IsV3Mode ? "资产评估说明：" : "文档 B：";
        public string SelectDocumentAText => IsV3Mode ? "选择评估报告" : "选择文档 A";
        public string SelectDocumentBText => IsV3Mode ? "选择评估说明" : "选择文档 B（可选）";
        public string OpenDocumentAText => IsV3Mode ? "打开评估报告" : "打开文档 A";
        public string OpenDocumentBText => IsV3Mode ? "打开评估说明" : "打开文档 B";
        public bool CanSelectFiles => !IsBusy && !IsNavigating;
        public string ExcelPath
        {
            get => _excelPath;
            private set { if (SetProperty(ref _excelPath, value)) RefreshStartCheck(); }
        }
        public string ExcelFileName { get => _excelFileName; private set => SetProperty(ref _excelFileName, value); }
        public string ComparisonSummary { get => _comparisonSummary; private set => SetProperty(ref _comparisonSummary, value); }

        private void RefreshStartCheck()
        {
            OnPropertyChanged(nameof(CanStartCheck));
            StartCheckCommand.RaiseCanExecuteChanged();
        }

        public IReadOnlyList<IssueFilterOption> FilterOptions { get; }
        public IReadOnlyList<IssueDisplayModeOption> DisplayModeOptions { get; }

        public AsyncRelayCommand StartCheckCommand { get; }

        public RelayCommand OpenReportCommand { get; }

        public RelayCommand OpenDocumentACommand { get; }

        public RelayCommand OpenDocumentBCommand { get; }

        public AsyncRelayCommand NavigateToSourceCommand { get; }
        public RelayCommand CopyIssueDetailsCommand { get; }
        public RelayCommand CopySourceSnippetCommand { get; }
        public RelayCommand OpenSourceCommand { get; }
        public string SelectedIssueDetails => IssueDetailsFormatter.Format(SelectedIssue, _sources);
        public string NavigationMessage { get => _navigationMessage; private set => SetProperty(ref _navigationMessage, value); }
        public bool IsNavigating
        {
            get => _isNavigating;
            private set
            {
                if (SetProperty(ref _isNavigating, value))
                {
                    RefreshNavigationCommands();
                    OnPropertyChanged(nameof(CanSelectFiles));
                    OnPropertyChanged(nameof(CanStartCheck));
                    StartCheckCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public OccurrenceViewModel SelectedNavigationOccurrence
        {
            get => _selectedNavigationOccurrence;
            set
            {
                if (value != null && (SelectedIssue == null || !SelectedIssue.Occurrences.Contains(value))) return;
                if (SetProperty(ref _selectedNavigationOccurrence, value))
                {
                    NavigationMessage = string.Empty;
                    RefreshNavigationCommands();
                }
            }
        }
        private CheckedDocumentSource NavigationSource =>
            SourceNavigationService.FindSource(_sources, SelectedNavigationOccurrence?.SourceOccurrence);

        private void RefreshNavigationCommands()
        {
            NavigateToSourceCommand?.RaiseCanExecuteChanged();
            CopyIssueDetailsCommand?.RaiseCanExecuteChanged();
            CopySourceSnippetCommand?.RaiseCanExecuteChanged();
            OpenSourceCommand?.RaiseCanExecuteChanged();
        }

        public async Task NavigateToSourceAsync()
        {
            if (SelectedNavigationOccurrence == null || IsBusy || IsNavigating) return;
            OccurrenceViewModel occurrence = SelectedNavigationOccurrence;
            IsNavigating = true;
            NavigationMessage = "正在核验原文并请求 WPS/Word 定位……";
            try
            {
                SourceNavigationResult result = await _navigation.NavigateAsync(NavigationSource, occurrence.SourceOccurrence);
                if (ReferenceEquals(occurrence, SelectedNavigationOccurrence))
                    NavigationMessage = (result.Status == SourceNavigationStatus.Exact ? "精确定位：" :
                        result.Status == SourceNavigationStatus.ParagraphOnly ? "段落定位：" :
                        result.Status == SourceNavigationStatus.TableCell ? "单元格定位：" :
                        result.Status == SourceNavigationStatus.TableRow ? "表格行定位：" : "无法定位：") + result.Message;
            }
            catch (Exception)
            {
                if (ReferenceEquals(occurrence, SelectedNavigationOccurrence))
                    NavigationMessage = "无法使用 WPS/Word 精确定位功能。可打开原文或复制片段手动查找。";
            }
            finally { IsNavigating = false; }
        }

        private void CopyText(string text, string label)
        {
            NavigationMessage = _clipboard.TryCopy(text) ? "已复制" + label + "。" : "剪贴板当前不可用，请稍后重试或选中文字按 Ctrl+C。";
        }

        private void OpenSource()
        {
            if (NavigationSource == null) return;
            ReportFileLaunchResult result = _reportFileLauncher.Open(NavigationSource.FullPath);
            NavigationMessage = result.Message + " 此操作仅打开文件，不代表已精确定位；可手动 Ctrl+F 查找。";
        }

        public string SelectedFilePath
        {
            get => _selectedFilePath;
            private set
            {
                if (SetProperty(ref _selectedFilePath, value))
                {
                    OnPropertyChanged(nameof(HasSelectedFile));
                    OnPropertyChanged(nameof(CanStartCheck));
                    OnPropertyChanged(nameof(CanOpenReport));
                    OnPropertyChanged(nameof(DocumentAPath));
                    OnPropertyChanged(nameof(HasDocumentA));
                    OnPropertyChanged(nameof(CanOpenDocumentA));
                    StartCheckCommand.RaiseCanExecuteChanged();
                    OpenDocumentACommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedFileName
        {
            get => _selectedFileName;
            private set
            {
                if (SetProperty(ref _selectedFileName, value))
                {
                    OnPropertyChanged(nameof(DocumentAFileName));
                }
            }
        }

        public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SelectedFilePath);

        public string DocumentAPath => SelectedFilePath;

        public string DocumentAFileName => IsV3Mode && !HasDocumentA ? "尚未选择资产评估报告（必选）" : SelectedFileName;

        public bool HasDocumentA => HasSelectedFile;

        public string DocumentBPath
        {
            get => _documentBPath;
            private set
            {
                if (SetProperty(ref _documentBPath, value))
                {
                    OnPropertyChanged(nameof(HasDocumentB));
                    RefreshStartCheck();
                    OnPropertyChanged(nameof(CanOpenDocumentB));
                    OpenDocumentBCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string DocumentBFileName
        {
            get => IsV3Mode && !HasDocumentB ? "尚未选择资产评估说明（必选）" : _documentBFileName;
            private set => SetProperty(ref _documentBFileName, value);
        }

        public bool HasDocumentB => !string.IsNullOrWhiteSpace(DocumentBPath);

        public bool CanStartCheck => HasSelectedFile && (!IsV3Mode ||
            (HasDocumentB && !string.IsNullOrWhiteSpace(ExcelPath))) && !IsBusy && !IsNavigating;

        public bool CanOpenReport => HasSelectedFile && !IsBusy;

        public bool CanOpenDocumentA => CanOpenReport;

        public bool CanOpenDocumentB => HasDocumentB && !IsBusy;

        public string ExpectedProjectName
        {
            get => _expectedProjectName;
            set => SetProperty(ref _expectedProjectName, value ?? string.Empty);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string ResultMessage
        {
            get => _resultMessage;
            private set => SetProperty(ref _resultMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(CanStartCheck));
                    OnPropertyChanged(nameof(CanSelectFiles));
                    OnPropertyChanged(nameof(CanOpenReport));
                    OnPropertyChanged(nameof(CanOpenDocumentA));
                    OnPropertyChanged(nameof(CanOpenDocumentB));
                    StartCheckCommand.RaiseCanExecuteChanged();
                    OpenDocumentACommand.RaiseCanExecuteChanged();
                    OpenDocumentBCommand.RaiseCanExecuteChanged();
                    RefreshNavigationCommands();
                }
            }
        }

        public int SeriousCount
        {
            get => _seriousCount;
            private set => SetProperty(ref _seriousCount, value);
        }

        public int NormalCount
        {
            get => _normalCount;
            private set => SetProperty(ref _normalCount, value);
        }

        public int ReminderCount
        {
            get => _reminderCount;
            private set => SetProperty(ref _reminderCount, value);
        }

        public int TotalCount => SeriousCount + NormalCount + ReminderCount;

        public int AuxiliaryCount => IsV3Mode ? 0 : _allIssues.Count(IssueAttentionPolicy.IsAuxiliary);
        public int RetainedIssueCount => _allIssues.Count;
        public string DisplayModeDescription => IsV3Mode
            ? "V3 仅核对三个评估结论的金额；不一致和无法判定均保留在问题列表中。"
            : SelectedDisplayMode == IssueDisplayMode.Focused
            ? "当前显示重点质控；另有 " + AuxiliaryCount + " 条辅助提示保留在详细检查中，并非已修复。"
            : "当前显示全部 " + RetainedIssueCount + " 条检查记录（含 " + AuxiliaryCount + " 条辅助提示）。";

        public IssueDisplayMode SelectedDisplayMode
        {
            get => _selectedDisplayMode;
            set
            {
                if (SetProperty(ref _selectedDisplayMode, value)) RefreshVisibleIssues();
            }
        }

        public IssueSeverityFilter SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    RefreshVisibleIssues();
                }
            }
        }

        public IssueViewModel SelectedIssue
        {
            get => _selectedIssue;
            set
            {
                if (SetProperty(ref _selectedIssue, value))
                {
                    OnPropertyChanged(nameof(HasSelectedIssue));
                    OnPropertyChanged(nameof(SelectedIssueDetails));
                    SelectedNavigationOccurrence = value?.Occurrences.FirstOrDefault();
                    RefreshNavigationCommands();
                }
            }
        }

        public bool HasSelectedIssue => SelectedIssue != null;

        public bool TrySelectFile(string filePath)
        {
            return TrySelectDocumentA(filePath);
        }

        public bool TrySelectDocumentA(string filePath)
        {
            return TrySelectDocument(filePath, false);
        }

        public bool TrySelectDocumentB(string filePath)
        {
            return TrySelectDocument(filePath, true);
        }

        public bool TrySelectExcel(string filePath)
        {
            if (!CanSelectFiles) return false;
            ClearResultState();
            string extension;
            try { extension = Path.GetExtension(filePath ?? string.Empty); }
            catch (ArgumentException) { extension = string.Empty; }
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ExcelPath = string.Empty;
                ExcelFileName = "尚未选择 Excel 工作簿";
                StatusMessage = "V3 当前仅支持 .xlsx 工作簿，请选择包含《资产评估汇总表》的文件。";
                ResultMessage = StatusMessage;
                return false;
            }
            ExcelPath = filePath;
            ExcelFileName = SafeFileName(filePath);
            StatusMessage = "已选择 Excel 工作簿；V3 只读取《资产评估汇总表》。";
            ResultMessage = "尚未执行校检。";
            return true;
        }

        public void ClearExcel()
        {
            if (!CanSelectFiles) return;
            ClearResultState();
            ExcelPath = string.Empty;
            ExcelFileName = "尚未选择 Excel 工作簿";
            StatusMessage = "请选择包含《资产评估汇总表》的 Excel 工作簿。";
            ResultMessage = "尚未执行校检。";
        }

        private bool TrySelectDocument(string filePath, bool isDocumentB)
        {
            if (IsBusy || IsNavigating)
            {
                return false;
            }

            ClearResultState();
            string extension;
            try
            {
                extension = Path.GetExtension(filePath ?? string.Empty);
            }
            catch (ArgumentException)
            {
                extension = string.Empty;
            }

            if (string.Equals(extension, ".doc", StringComparison.OrdinalIgnoreCase))
            {
                ClearDocument(isDocumentB);
                StatusMessage = "当前仅支持 DOCX 文件，请使用 WPS 或 Word 另存为 DOCX 后重新检查。";
                ResultMessage = StatusMessage;
                return false;
            }

            if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                ClearDocument(isDocumentB);
                StatusMessage = "暂不支持该文件格式，请选择 .docx 报告。";
                ResultMessage = StatusMessage;
                return false;
            }

            if (isDocumentB)
            {
                DocumentBPath = filePath;
                DocumentBFileName = SafeFileName(filePath);
                StatusMessage = "已选择文档 B，将执行双文档校检。";
            }
            else
            {
                SelectedFilePath = filePath;
                SelectedFileName = SafeFileName(filePath);
                ExpectedProjectName = string.Empty;
                StatusMessage = HasDocumentB
                    ? "已选择文档 A，将执行双文档校检。"
                    : "已选择文档 A，可以开始单文档校检。";
            }

            if (IsV3Mode)
                StatusMessage = "已选择" + (isDocumentB ? "资产评估说明" : "资产评估报告") + "；三个文件齐备后可开始 V3 校检。";
            ResultMessage = "尚未执行校检。";
            return true;
        }

        public async Task StartCheckAsync()
        {
            if (!CanStartCheck)
            {
                return;
            }

            string filePath = SelectedFilePath;
            string documentBPath = DocumentBPath;
            string expectedProjectName = ExpectedProjectName;
            ReportCheckMode mode = IsV3Mode ? ReportCheckMode.WordExcel : ReportCheckMode.WordDocuments;
            string excelPath = ExcelPath;
            ClearResultState();
            IsBusy = true;
            OnPropertyChanged(nameof(AiStatusMessage));
            StatusMessage = "正在读取报告……";
            SynchronizationContext uiContext = SynchronizationContext.Current;
            var progress = new CallbackProgress<string>(message =>
            {
                if (uiContext == null)
                {
                    StatusMessage = message;
                }
                else
                {
                    uiContext.Post(_ => StatusMessage = message, null);
                }
            });

            try
            {
                var request = new ReportCheckRequest(
                    filePath,
                    documentBPath,
                    expectedProjectName,
                    mode,
                    excelPath);
                ReportCheckResult result = await _reportCheckService.CheckAsync(request, progress);
                ApplyResult(result);
            }
            catch (Exception exception)
            {
                Trace.TraceError("Desktop check workflow failed: {0}", exception);
                StatusMessage = "校检过程中发生未预期错误，本次未修改原报告。";
                ResultMessage = StatusMessage;
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(AiStatusMessage));
            }
        }

        private void ApplyResult(ReportCheckResult result)
        {
            if (result == null)
            {
                StatusMessage = "校检过程中发生未预期错误，本次未修改原报告。";
                ResultMessage = StatusMessage;
                return;
            }

            _sources = result.SourceDocuments;
            _allIssues.Clear();
            _allIssues.AddRange(result.Issues
                .Select(item => new IssueViewModel(item))
                .OrderByDescending(item => (int)item.Severity)
                .ThenBy(item => item.SourceOrder));
            _lastCheckMessage = result.Message;
            ComparisonSummary = result.IsV3Check ? result.Message : string.Empty;
            RefreshVisibleIssues();
        }

        private void RefreshVisibleIssues()
        {
            IssueViewModel previouslySelected = SelectedIssue;
            Issues.Clear();
            foreach (IssueViewModel issue in _allIssues.Where(MatchesSelectedDisplayMode).Where(MatchesSelectedFilter))
            {
                Issues.Add(issue);
            }

            SelectedIssue = previouslySelected != null && Issues.Contains(previouslySelected)
                ? previouslySelected
                : Issues.FirstOrDefault();
            var inMode = _allIssues.Where(MatchesSelectedDisplayMode).ToList();
            SeriousCount = inMode.Count(issue => issue.Severity == IssueSeverity.Serious);
            NormalCount = inMode.Count(issue => issue.Severity == IssueSeverity.Normal);
            ReminderCount = inMode.Count(issue => issue.Severity == IssueSeverity.Reminder);
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(AuxiliaryCount));
            OnPropertyChanged(nameof(RetainedIssueCount));
            OnPropertyChanged(nameof(DisplayModeDescription));
            if (!string.IsNullOrEmpty(_lastCheckMessage))
            {
                ResultMessage = SelectedDisplayMode == IssueDisplayMode.Detailed || AuxiliaryCount == 0
                    ? _lastCheckMessage
                    : "重点质控显示 " + TotalCount + " 条问题；另有 " + AuxiliaryCount +
                      " 条辅助提示保留在详细检查中，未判定为已修复。";
                StatusMessage = ResultMessage;
            }
        }

        private bool MatchesSelectedDisplayMode(IssueViewModel issue) =>
            IsV3Mode || SelectedDisplayMode == IssueDisplayMode.Detailed || !IssueAttentionPolicy.IsAuxiliary(issue);

        private bool MatchesSelectedFilter(IssueViewModel issue)
        {
            switch (SelectedFilter)
            {
                case IssueSeverityFilter.Serious:
                    return issue.Severity == IssueSeverity.Serious;
                case IssueSeverityFilter.Normal:
                    return issue.Severity == IssueSeverity.Normal;
                case IssueSeverityFilter.Reminder:
                    return issue.Severity == IssueSeverity.Reminder;
                default:
                    return true;
            }
        }

        private void ClearResultState()
        {
            _sources = new CheckedDocumentSource[0];
            _allIssues.Clear();
            _lastCheckMessage = string.Empty;
            ComparisonSummary = string.Empty;
            Issues.Clear();
            SelectedIssue = null;
            SeriousCount = 0;
            NormalCount = 0;
            ReminderCount = 0;
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(AuxiliaryCount));
            OnPropertyChanged(nameof(RetainedIssueCount));
            OnPropertyChanged(nameof(DisplayModeDescription));
            ResultMessage = string.Empty;
            NavigationMessage = string.Empty;
        }

        private void ClearSelectedFile()
        {
            SelectedFilePath = string.Empty;
            SelectedFileName = "尚未选择文档 A";
            ExpectedProjectName = string.Empty;
        }

        public void ClearDocumentB()
        {
            if (IsBusy || IsNavigating)
            {
                return;
            }

            ClearResultState();
            ClearDocument(true);
            StatusMessage = IsV3Mode ? "已清除资产评估说明；V3 需要三个文件齐备后才能校检。" : HasDocumentA
                ? "已清除文档 B，将执行单文档校检。"
                : "尚未选择文档 A";
            ResultMessage = "尚未执行校检。";
        }

        private void ClearDocument(bool isDocumentB)
        {
            if (isDocumentB)
            {
                DocumentBPath = string.Empty;
                DocumentBFileName = "尚未选择（可选）";
            }
            else
            {
                ClearSelectedFile();
            }
        }

        private void OpenDocument(string filePath)
        {
            ReportFileLaunchResult result = _reportFileLauncher.Open(filePath);
            StatusMessage = result.Message;
        }

        private static string SafeFileName(string filePath)
        {
            try
            {
                string name = Path.GetFileName(filePath);
                return string.IsNullOrWhiteSpace(name) ? "已选择文档" : name;
            }
            catch (ArgumentException)
            {
                return "已选择文档";
            }
        }
    }
}
