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
            var checkService = DesktopLocalAiService.CreateConfigured();
            DataContext = new MainWindowViewModel(
                checkService,
                new WindowsReportFileLauncher());
            Closed += (sender, args) => checkService.Dispose();
        }

        private void SelectDocumentAButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDocument("选择文档 A", false);
        }

        private void SelectDocumentBButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDocument("选择文档 B（可选）", true);
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
