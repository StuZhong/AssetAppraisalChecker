using System;
using System.Diagnostics;
using System.IO;

namespace AssetAppraisalChecker.Desktop.Services
{
    public sealed class WindowsReportFileLauncher : IReportFileLauncher
    {
        public ReportFileLaunchResult Open(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) ||
                    !string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase))
                {
                    return new ReportFileLaunchResult(false, "当前没有可打开的 .docx 报告。");
                }

                if (!File.Exists(filePath))
                {
                    return new ReportFileLaunchResult(false, "原报告文件不存在，请重新选择。");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
                return new ReportFileLaunchResult(true, "已使用 Windows 默认关联应用打开原报告。");
            }
            catch (Exception exception)
            {
                Trace.TraceError("Unable to open selected report: {0}", exception);
                return new ReportFileLaunchResult(
                    false,
                    "无法打开原报告，请确认文件存在且 Windows 已配置 .docx 默认打开程序。");
            }
        }
    }
}
