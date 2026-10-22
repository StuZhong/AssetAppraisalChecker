using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AssetAppraisalChecker.Document.Excel
{
    /// <summary>Reads the named XLSX summary only. Does not calculate formulas or save the workbook.</summary>
    public sealed class ExcelSummaryReader
    {
        public const string SummaryWorksheetName = "资产评估汇总表";

        public ExcelSummaryReadResult Read(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    return Fail(ExcelSummaryReadStatus.InvalidPath, "请选择有效的 .xlsx 文件路径。");
                if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    return Fail(ExcelSummaryReadStatus.UnsupportedFileType, "当前汇总金额读取仅支持 .xlsx，不支持 .xls。");
                if (!File.Exists(filePath))
                    return Fail(ExcelSummaryReadStatus.FileNotFound, "找不到指定的 Excel 文件，请确认本机路径。");

                // Reject active writers rather than reading an inconsistent package.
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(stream, false))
                {
                    WorkbookPart workbook = document.WorkbookPart;
                    if (workbook?.Workbook?.Sheets == null)
                        return Fail(ExcelSummaryReadStatus.InvalidWorkbook, "文件不是有效的 Excel 工作簿，或缺少工作表信息。");

                    Sheet[] matches = workbook.Workbook.Sheets.Elements<Sheet>()
                        .Where(sheet => sheet.Name?.Value == SummaryWorksheetName).ToArray();
                    if (matches.Length == 0)
                        return Fail(ExcelSummaryReadStatus.MissingWorksheet, "缺少指定工作表“资产评估汇总表”；不会从其他工作表猜测金额。");
                    if (matches.Length != 1 || string.IsNullOrEmpty(matches[0].Id?.Value))
                        return Fail(ExcelSummaryReadStatus.AmbiguousLayout, "资产评估汇总表的工作表标识不唯一或无效。");

                    var worksheet = workbook.GetPartById(matches[0].Id.Value) as WorksheetPart;
                    if (worksheet?.Worksheet == null)
                        return Fail(ExcelSummaryReadStatus.InvalidWorkbook, "指定工作表缺少有效的工作表内容。");
                    return ExcelSummaryExtractor.Extract(workbook, worksheet);
                }
            }
            catch (ExcelSummaryReadException exception)
            {
                return Fail(exception.Status, exception.Message);
            }
            catch (UnauthorizedAccessException) { return Fail(ExcelSummaryReadStatus.AccessDenied, "没有读取 Excel 文件的权限。"); }
            catch (SecurityException) { return Fail(ExcelSummaryReadStatus.AccessDenied, "没有读取 Excel 文件的权限。"); }
            catch (FileNotFoundException) { return Fail(ExcelSummaryReadStatus.FileNotFound, "Excel 文件已不存在，请重新选择。"); }
            catch (OpenXmlPackageException) { return InvalidWorkbook(); }
            catch (InvalidDataException) { return InvalidWorkbook(); }
            catch (IOException exception)
            {
                int error = exception.HResult & 0xffff;
                return error == 32 || error == 33
                    ? Fail(ExcelSummaryReadStatus.FileInUse, "无法安全读取 Excel 文件，请先保存，必要时关闭文件后重试。")
                    : Fail(ExcelSummaryReadStatus.IoError, "读取 Excel 文件时发生文件系统错误，请确认文件已保存且可访问。");
            }
            catch (XmlException) { return InvalidWorkbook(); }
            catch (FormatException) { return InvalidWorkbook(); }
            catch (ArgumentException) { return InvalidWorkbook(); }
            catch (Exception exception)
            {
                // Technical diagnostics contain no workbook text, business values or file path.
                Trace.TraceError("Excel summary read failed: {0}", exception.GetType().FullName);
                return Fail(ExcelSummaryReadStatus.UnexpectedError, "无法读取 Excel 汇总金额，请确认文件未加密且为有效的 .xlsx 文件。");
            }
        }

        private static ExcelSummaryReadResult InvalidWorkbook() =>
            Fail(ExcelSummaryReadStatus.InvalidWorkbook, "Excel 文件已损坏、已加密或不是有效的 .xlsx 工作簿。");

        private static ExcelSummaryReadResult Fail(ExcelSummaryReadStatus status, string message) =>
            ExcelSummaryReadResult.Failed(status, message + " 原文件未被修改。");
    }

    internal sealed class ExcelSummaryReadException : Exception
    {
        public ExcelSummaryReadException(ExcelSummaryReadStatus status, string message) : base(message)
        {
            Status = status;
        }

        public ExcelSummaryReadStatus Status { get; }
    }
}
