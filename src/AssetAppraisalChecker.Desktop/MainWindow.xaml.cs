using Microsoft.Win32;
using System.Windows;
using AssetAppraisalChecker.Desktop.Services;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop
{
    public partial class MainWindow : Window
    {
        public const string ReportFileDialogFilter =
            "Word 文档 (*.docx;*.doc)|*.docx;*.doc";
        public const string ExcelFileDialogFilter = "Excel 工作簿 (*.xlsx)|*.xlsx";

        public MainWindow()
        {
            InitializeComponent();
            var checkService = DesktopLocalAiService.CreateConfigured();
            DataContext = new MainWindowViewModel(
                checkService,
                new WindowsReportFileLauncher());
            Closed += (sender, args) => checkService.Dispose();
        }

        private void SelectDocumentAButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDocument((DataContext as MainWindowViewModel)?.IsV3Mode == true ? "选择资产评估报告" : "选择文档 A", false);
        }

        private void SelectDocumentBButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDocument((DataContext as MainWindowViewModel)?.IsV3Mode == true ? "选择资产评估说明" : "选择文档 B（可选）", true);
        }

        private void SelectExcelButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择包含《资产评估汇总表》的 Excel 工作簿",
                Filter = ExcelFileDialogFilter,
                DefaultExt = ".xlsx",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) == true)
                (DataContext as MainWindowViewModel)?.TrySelectExcel(dialog.FileName);
        }

        private void ClearExcelButton_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as MainWindowViewModel)?.ClearExcel();
        }

        private void ClearDocumentBButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            viewModel?.ClearDocumentB();
        }

        private void SelectDocument(string title, bool isDocumentB)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = ReportFileDialogFilter,
                DefaultExt = ".docx",
                AddExtension = true,
                CheckFileExists = true,
                Multiselect = false
            };

            bool? selected = dialog.ShowDialog(this);
            if (selected == true)
            {
                var viewModel = DataContext as MainWindowViewModel;
                if (isDocumentB)
                {
                    viewModel?.TrySelectDocumentB(dialog.FileName);
                }
                else
                {
                    viewModel?.TrySelectDocumentA(dialog.FileName);
                }
            }
        }
    }
}
