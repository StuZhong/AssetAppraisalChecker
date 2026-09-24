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
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly IReportCheckService _reportCheckService;
        private readonly IReportFileLauncher _reportFileLauncher;
        private readonly List<IssueViewModel> _allIssues = new List<IssueViewModel>();
        private string _selectedFilePath = string.Empty;
        private string _selectedFileName = "尚未选择报告";
        private string _statusMessage = "尚未选择报告";
        private string _resultMessage = "尚未执行校检。";
        private string _expectedProjectName = string.Empty;
        private bool _isBusy;
        private int _seriousCount;
        private int _normalCount;
        private int _reminderCount;
        private IssueSeverityFilter _selectedFilter = IssueSeverityFilter.All;
        private IssueViewModel _selectedIssue;

        public MainWindowViewModel(
            IReportCheckService reportCheckService,
            IReportFileLauncher reportFileLauncher)
        {
            _reportCheckService = reportCheckService ??
                                  throw new ArgumentNullException(nameof(reportCheckService));
            _reportFileLauncher = reportFileLauncher ??
                                  throw new ArgumentNullException(nameof(reportFileLauncher));
            Issues = new ObservableCollection<IssueViewModel>();
            FilterOptions = new ReadOnlyCollection<IssueFilterOption>(new[]
            {
                new IssueFilterOption(IssueSeverityFilter.All, "全部"),
                new IssueFilterOption(IssueSeverityFilter.Serious, "严重"),
                new IssueFilterOption(IssueSeverityFilter.Normal, "一般"),
                new IssueFilterOption(IssueSeverityFilter.Reminder, "提醒")
            });
            StartCheckCommand = new AsyncRelayCommand(StartCheckAsync, () => CanStartCheck);
            OpenReportCommand = new RelayCommand(
                _ => OpenSelectedReport(),
                _ => CanOpenReport);
        }

        public ObservableCollection<IssueViewModel> Issues { get; }

        public IReadOnlyList<IssueFilterOption> FilterOptions { get; }

        public AsyncRelayCommand StartCheckCommand { get; }

        public RelayCommand OpenReportCommand { get; }

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
                    StartCheckCommand.RaiseCanExecuteChanged();
                    OpenReportCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedFileName
        {
            get => _selectedFileName;
            private set => SetProperty(ref _selectedFileName, value);
        }

        public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SelectedFilePath);

        public bool CanStartCheck => HasSelectedFile && !IsBusy;

        public bool CanOpenReport => HasSelectedFile && !IsBusy;

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
                    OnPropertyChanged(nameof(CanOpenReport));
                    StartCheckCommand.RaiseCanExecuteChanged();
                    OpenReportCommand.RaiseCanExecuteChanged();
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
                }
            }
        }

        public bool HasSelectedIssue => SelectedIssue != null;

        public bool TrySelectFile(string filePath)
        {
            if (IsBusy)
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
                ClearSelectedFile();
                StatusMessage = "当前仅支持 DOCX 文件，请使用 WPS 或 Word 另存为 DOCX 后重新检查。";
                ResultMessage = StatusMessage;
                return false;
            }

            if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                ClearSelectedFile();
                StatusMessage = "暂不支持该文件格式，请选择 .docx 报告。";
                ResultMessage = StatusMessage;
                return false;
            }

            SelectedFilePath = filePath;
            SelectedFileName = SafeFileName(filePath);
            ExpectedProjectName = string.Empty;
            StatusMessage = "已选择报告，可以开始校检。";
            ResultMessage = "尚未执行校检。";
            return true;
        }

        public async Task StartCheckAsync()
        {
            if (!CanStartCheck)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ExpectedProjectName))
            {
                StatusMessage = "请输入正确项目名称后再开始校检。";
                ResultMessage = StatusMessage;
                return;
            }

            string filePath = SelectedFilePath;
            string expectedProjectName = ExpectedProjectName;
            ClearResultState();
            IsBusy = true;
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
                var request = new ReportCheckRequest(filePath, expectedProjectName);
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

            _allIssues.Clear();
            _allIssues.AddRange(result.Issues
                .Select(item => new IssueViewModel(item))
                .OrderByDescending(item => (int)item.Severity)
                .ThenBy(item => item.SourceOrder));
            SeriousCount = result.SeriousCount;
            NormalCount = result.NormalCount;
            ReminderCount = result.ReminderCount;
            OnPropertyChanged(nameof(TotalCount));
            ResultMessage = result.Message;
            StatusMessage = result.Message;
            RefreshVisibleIssues();
        }

        private void RefreshVisibleIssues()
        {
            IssueViewModel previouslySelected = SelectedIssue;
            Issues.Clear();
            foreach (IssueViewModel issue in _allIssues.Where(MatchesSelectedFilter))
            {
                Issues.Add(issue);
            }

            SelectedIssue = previouslySelected != null && Issues.Contains(previouslySelected)
                ? previouslySelected
                : Issues.FirstOrDefault();
        }

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
            _allIssues.Clear();
            Issues.Clear();
            SelectedIssue = null;
            SeriousCount = 0;
            NormalCount = 0;
            ReminderCount = 0;
            OnPropertyChanged(nameof(TotalCount));
            ResultMessage = string.Empty;
        }

        private void ClearSelectedFile()
        {
            SelectedFilePath = string.Empty;
            SelectedFileName = "尚未选择报告";
            ExpectedProjectName = string.Empty;
        }

        private void OpenSelectedReport()
        {
            ReportFileLaunchResult result = _reportFileLauncher.Open(SelectedFilePath);
            StatusMessage = result.Message;
        }

        private static string SafeFileName(string filePath)
        {
            try
            {
                string name = Path.GetFileName(filePath);
                return string.IsNullOrWhiteSpace(name) ? "已选择报告" : name;
            }
            catch (ArgumentException)
            {
                return "已选择报告";
            }
        }
    }
}
