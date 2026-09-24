namespace AssetAppraisalChecker.Desktop.Services
{
    public interface IReportFileLauncher
    {
        ReportFileLaunchResult Open(string filePath);
    }
}
