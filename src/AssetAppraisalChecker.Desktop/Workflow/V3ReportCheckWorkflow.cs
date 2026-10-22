using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using AssetAppraisalChecker.Application.V3;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Document.Excel;
using AssetAppraisalChecker.Facts.Extraction.Conclusions;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    /// <summary>Desktop mapping only; existing V3 components own extraction and comparison.</summary>
    internal sealed class V3ReportCheckWorkflow
    {
        public ReportCheckResult Check(ReportCheckRequest request, IProgress<string> progress)
        {
            DateTimeOffset startedAt = DateTimeOffset.Now;
            var stopwatch = Stopwatch.StartNew();
            if (string.IsNullOrWhiteSpace(request.DocumentAPath)
                || string.IsNullOrWhiteSpace(request.DocumentBPath)
                || string.IsNullOrWhiteSpace(request.ExcelPath))
                return Failed(request, startedAt, stopwatch,
                    "V3 需要分别选择资产评估报告、资产评估说明和 Excel 汇总工作簿。");

            try
            {
                // Each check captures fresh identities. Navigation must never use a later
                // version of the Word file with positions from this check.
                string reportHash = TryHash(request.DocumentAPath);
                string explanationHash = TryHash(request.DocumentBPath);
                string excelHash = TryHash(request.ExcelPath);
                var reader = new DocxDocumentReader();
                var extractor = new WordConclusionExtractor();

                progress?.Report("V3：正在读取资产评估报告的两处结论……");
                DocumentReadResult reportDocument = ReadWordSnapshot(reader, request.DocumentAPath, reportHash);
                WordConclusionExtractionResult report = extractor.Extract(reportDocument,
                    request.DocumentAPath, WordConclusionDocumentKind.Report);
                progress?.Report("V3：正在读取资产评估说明第四章结论……");
                DocumentReadResult explanationDocument = ReadWordSnapshot(reader, request.DocumentBPath, explanationHash);
                WordConclusionExtractionResult explanation = extractor.Extract(explanationDocument,
                    request.DocumentBPath, WordConclusionDocumentKind.Explanation);
                progress?.Report("V3：正在读取 Excel 资产评估汇总表……");
                ExcelSummaryReadResult excel = new ExcelSummaryReader().Read(request.ExcelPath);

                progress?.Report("V3：正在按字段、税务口径及来源行比较金额……");
                V3MoneyComparisonResult comparison = new V3MoneyComparisonService()
                    .Compare(report, explanation, excel, request.ExcelPath);
                var issues = comparison.Items
                    .Where(item => item.Status != V3MoneyComparisonStatus.Consistent)
                    .Select((item, index) => MapIssue(item, request, index)).ToList();
                var sources = new List<CheckedDocumentSource>();
                if (reportDocument.IsSuccess)
                    sources.Add(new CheckedDocumentSource("文档 A", request.DocumentAPath,
                        RequireHash(reportHash, "资产评估报告"), reportDocument));
                if (explanationDocument.IsSuccess)
                    sources.Add(new CheckedDocumentSource("文档 B", request.DocumentBPath,
                        RequireHash(explanationHash, "资产评估说明"), explanationDocument));

                // No mapped result survives a change while the three inputs were read.
                if (reportDocument.IsSuccess) EnsureUnchanged(request.DocumentAPath, reportHash, "资产评估报告");
                if (explanationDocument.IsSuccess) EnsureUnchanged(request.DocumentBPath, explanationHash, "资产评估说明");
                if (excel.IsSuccess) EnsureUnchanged(request.ExcelPath, excelHash, "Excel 汇总工作簿");
                stopwatch.Stop();
                bool readable = reportDocument.IsSuccess && explanationDocument.IsSuccess && excel.IsSuccess;
                string state = !readable ? "读取未全部成功"
                    : comparison.IndeterminateCount > 0 ? "校检完成，存在无法判定项"
                    : comparison.InconsistentCount > 0 ? "校检完成，发现金额不一致"
                    : "校检成功";
                string message = string.Format(CultureInfo.InvariantCulture,
                    "V3 Word–Excel {0}：共 {1} 项，一致 {2} 项、不一致 {3} 项、无法判定 {4} 项。",
                    state, comparison.TotalCount, comparison.ConsistentCount,
                    comparison.InconsistentCount, comparison.IndeterminateCount);
                return new ReportCheckResult(request.DocumentAPath, FileName(request.DocumentAPath),
                    startedAt, DateTimeOffset.Now, stopwatch.Elapsed, reportDocument.Status,
                    report.AmountCount + explanation.AmountCount, 0, issues, readable, message,
                    documentBFilePath: request.DocumentBPath,
                    documentBFileName: FileName(request.DocumentBPath),
                    documentBReadStatus: explanationDocument.Status, sourceDocuments: sources,
                    v3Comparison: comparison, excelFilePath: request.ExcelPath);
            }
            catch (InputIdentityException exception)
            {
                Trace.TraceWarning("V3 input identity could not be confirmed.");
                return Failed(request, startedAt, stopwatch, exception.Message);
            }
            catch (Exception exception)
            {
                Trace.TraceError("V3 Desktop check failed: {0}", exception.GetType().FullName);
                return Failed(request, startedAt, stopwatch,
                    "V3 校检未完成。请确认三个文件已保存且能够读取，必要时关闭文档后重试。原文件未被修改。");
            }
        }

        private static ReportCheckIssue MapIssue(V3MoneyComparisonItem item, ReportCheckRequest request, int index)
        {
            bool conflict = item.Status == V3MoneyComparisonStatus.Inconsistent;
            bool explanation = item.Target == WordConclusionTarget.ExplanationChapterFour;
            string wordPath = explanation ? request.DocumentBPath : request.DocumentAPath;
            string targetName = TargetName(item.Target);
            string fieldName = (item.ValueCategory == ValueCategory.AppraisedOriginalValue ? "评估原值" : "评估净值")
                + (item.TaxBasis == TaxBasis.TaxExcluded ? "（不含税）" : "（含税）");
            string sourceName = SourceName(item.SourceKind);
            WordConclusionAmount word = item.WordAmount;
            var evidence = new List<string>
            {
                "Word 文件：" + wordPath,
                "评估结论位置：" + targetName,
                "金额字段：" + fieldName,
                "来源类型：" + sourceName,
                "Excel 文件：" + request.ExcelPath
            };
            var warnings = new List<string>();
            if (word != null)
            {
                evidence.Add("Word 标准金额：" + Yuan(word.NormalizedAmountYuan));
                evidence.Add(word.Evidence);
                evidence.Add(string.Format(CultureInfo.InvariantCulture,
                    "原文范围：Start={0}，Length={1}", word.TextStartIndex, word.TextLength));
                warnings.AddRange(word.ParseWarnings);
            }
            else
                warnings.Add("未取得可靠的 Word 金额位置，无法精确定位；请根据结论名称人工复核。");

            if (item.ExcelAmount != null)
            {
                ExcelSummaryAmount reference = item.ExcelAmount;
                evidence.Add("Excel 来源：" + reference.WorksheetName + "!" + reference.CellAddress);
                evidence.Add("Excel 来源行：" + (reference.RowKind == ExcelSummaryRowKind.Asset ? "资产行" : "合计行"));
                evidence.Add("Excel 原始金额：" + reference.RawAmountTenThousandYuan.ToString("G29", CultureInfo.InvariantCulture) + " 万元");
                evidence.Add("Excel 比较金额：" + reference.RoundedAmountTenThousandYuan.ToString("F2", CultureInfo.InvariantCulture)
                    + " 万元" + (item.ExcelComparisonAmountYuan.HasValue ? "（" + Yuan(item.ExcelComparisonAmountYuan.Value) + "）" : string.Empty));
            }
            else evidence.Add("Excel 对应金额未可靠取得；原因见问题说明。");
            if (item.DifferenceYuan.HasValue) evidence.Add("金额差额：" + Yuan(item.DifferenceYuan.Value));

            DocumentLocation location = word?.Location
                ?? new DocumentLocation(DocumentPart.MainBody, null, null, null, null, new string[0]);
            string locationText = targetName + " > " + (word == null ? "未取得可靠原文位置" : DocumentLocationFormatter.Format(location));
            var occurrence = new ReportCheckOccurrence(word?.RawText,
                word == null ? "未可靠提取" : Yuan(word.NormalizedAmountYuan), location, locationText,
                targetName, fieldName + " / " + sourceName, evidence, warnings,
                Context(word?.NearbyContext), false, documentLabel: explanation ? "文档 B" : "文档 A");
            string summary = targetName + " / " + sourceName + " / " + fieldName + "："
                + (conflict ? "Word " + word.RawText + "；Excel "
                    + item.ExcelAmount.RoundedAmountTenThousandYuan.ToString("F2", CultureInfo.InvariantCulture) + " 万元"
                    : "无法判定");
            return new ReportCheckIssue("V3-Money-" + (index + 1).ToString("D3", CultureInfo.InvariantCulture),
                conflict ? IssueType.MoneyConflict : IssueType.MoneyComparisonIndeterminate,
                conflict ? IssueSeverity.Serious : IssueSeverity.Reminder,
                fieldName, summary, item.Reason,
                conflict ? "请核对双方证据，在 WPS 中人工修改并保存后重新校检。"
                    : "请检查读取或对应关系失败原因，补充可靠金额证据后重新校检。",
                new[] { occurrence }, index,
                issueTypeDisplayOverride: conflict ? "Word–Excel 金额不一致" : "Word–Excel 无法判定",
                category: IssueCategory.Business,
                authority: conflict ? IssueAuthority.Confirmed : IssueAuthority.ReviewSuggested,
                governanceKind: conflict ? "V3MoneyInconsistent" : "V3MoneyIndeterminate");
        }

        private static ReportCheckResult Failed(ReportCheckRequest request, DateTimeOffset startedAt,
            Stopwatch stopwatch, string message)
        {
            stopwatch.Stop();
            return new ReportCheckResult(request.DocumentAPath, FileName(request.DocumentAPath),
                startedAt, DateTimeOffset.Now, stopwatch.Elapsed, DocumentReadStatus.UnexpectedError,
                0, 0, new ReportCheckIssue[0], false, message,
                documentBFilePath: request.DocumentBPath, documentBFileName: FileName(request.DocumentBPath),
                excelFilePath: request.ExcelPath ?? string.Empty);
        }

        private static string TryHash(string path)
        {
            if (!File.Exists(path)) return null;
            try { return CheckedDocumentSource.ComputeSha256(path); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (SecurityException) { return null; }
        }

        private static DocumentReadResult ReadWordSnapshot(DocxDocumentReader reader, string path, string baselineHash)
        {
            if (string.IsNullOrEmpty(baselineHash)
                || !string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase))
                return reader.Read(path);

            string snapshotPath = Path.Combine(Path.GetTempPath(),
                "AssetAppraisalChecker-V3-" + Guid.NewGuid().ToString("N") + ".docx");
            bool snapshotCreated = false;
            try
            {
                // WPS retains a write-capable handle after saving. Read a private snapshot
                // and verify its identity rather than relaxing the existing Word reader.
                using (var source = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var snapshot = new FileStream(snapshotPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    snapshotCreated = true;
                    source.CopyTo(snapshot);
                }
                if (!string.Equals(baselineHash, TryHash(snapshotPath), StringComparison.Ordinal))
                    throw new InputIdentityException("Word 文件在读取快照时发生改变或快照身份无法确认，请保存后重新校检。");
                return reader.Read(snapshotPath);
            }
            finally
            {
                if (snapshotCreated)
                {
                    try { File.Delete(snapshotPath); }
                    catch (IOException) { Trace.TraceWarning("V3 temporary Word snapshot cleanup failed."); }
                    catch (UnauthorizedAccessException) { Trace.TraceWarning("V3 temporary Word snapshot cleanup was denied."); }
                }
            }
        }

        private static string RequireHash(string hash, string role)
        {
            if (string.IsNullOrEmpty(hash)) throw new InputIdentityException(role + "的文件身份无法确认，请保存文件后重新校检。");
            return hash;
        }

        private static void EnsureUnchanged(string path, string hash, string role)
        {
            RequireHash(hash, role);
            if (!string.Equals(hash, TryHash(path), StringComparison.Ordinal))
                throw new InputIdentityException(role + "在校检期间发生改变或无法再次读取，请保存后重新校检。");
        }

        private static string FileName(string path)
        {
            try { return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path); }
            catch (ArgumentException) { return string.Empty; }
        }

        private static string Yuan(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture) + " 元";

        private static string TargetName(WordConclusionTarget target) => target == WordConclusionTarget.ReportSummary
            ? "资产评估报告摘要" : target == WordConclusionTarget.ReportChapterTen
            ? "资产评估报告正文第十章评估结论" : "资产评估说明第四章评估结论";

        private static string SourceName(WordConclusionSourceKind source) => source == WordConclusionSourceKind.BodyArabic
            ? "正文阿拉伯金额" : source == WordConclusionSourceKind.BodyChineseFinancial ? "正文中文大写金额"
            : source == WordConclusionSourceKind.TableAssetRow ? "表格资产行" : "表格合计行";

        private static string Context(DocumentNearbyContext context) => context == null ? string.Empty
            : string.Join(Environment.NewLine, new[] { context.PreviousText, context.CurrentText, context.NextText }
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        private sealed class InputIdentityException : Exception
        {
            public InputIdentityException(string message) : base(message) { }
        }
    }
}
