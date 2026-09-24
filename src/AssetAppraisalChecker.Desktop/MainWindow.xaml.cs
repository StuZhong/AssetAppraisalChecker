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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel(
                new ReportCheckService(),
                new WindowsReportFileLauncher());
        }

        private void SelectReportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择资产评估报告",
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
                viewModel?.TrySelectFile(dialog.FileName);
            }
        }
    }
}
