namespace AssetAppraisalChecker.Desktop.Services
{
    public sealed class ReportFileLaunchResult
    {
        public ReportFileLaunchResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public string Message { get; }
    }
}
