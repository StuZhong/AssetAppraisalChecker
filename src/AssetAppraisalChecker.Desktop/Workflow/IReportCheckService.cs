using System;
using System.Threading.Tasks;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public interface IReportCheckService
    {
        Task<ReportCheckResult> CheckAsync(
            string filePath,
            IProgress<string> progress = null);

        Task<ReportCheckResult> CheckAsync(
            ReportCheckRequest request,
            IProgress<string> progress = null);
    }
}
