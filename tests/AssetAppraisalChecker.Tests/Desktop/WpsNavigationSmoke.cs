using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Tests.Desktop
{
    /// <summary>
    /// Explicit interactive smoke, NOT an automated unit test. Connects real WPS only on request.
    /// Run in a dedicated console; creates synthetic fixtures in a fresh directory outside Git.
    /// Leaves read-only documents open for visual inspection. Never closes user documents or WPS.
    /// </summary>
    public static class WpsNavigationSmoke
    {
        public static string Run(string outputDirectory)
        {
            if (Directory.Exists(outputDirectory)) throw new ArgumentException("需要不存在的独立验收目录。");
            Directory.CreateDirectory(outputDirectory);
            string a = Path.Combine(outputDirectory, "文档A-合成定位.docx");
            string b = Path.Combine(outputDirectory, "文档B-合成定位.docx");
            Create(a, "甲");
            Create(b, "乙");
            var check = new ReportCheckService().CheckAsync(new ReportCheckRequest(a, b, null)).GetAwaiter().GetResult();
            if (check.SourceDocuments.Count != 2) throw new InvalidOperationException("生产检查未提供两份可验证来源。");
            var adapter = new DiagnosedWpsAdapter();
            var service = new SourceNavigationService(adapter);
            var report = new StringBuilder("真实 WPS 导航验收；仅合成数据；不是模拟测试。\n");
            foreach (var source in check.SourceDocuments)
            {
                Check(report, service, source, "普通正文", Element(source, 0), "评估价值");
                var cell = source.Document.Elements.Single(e => e.Location.TableIndex == 0 && e.Location.RowIndex == 1 && e.Location.CellIndex == 1);
                Check(report, service, source, "表格单元格", cell, "998.22万元");
                Check(report, service, source, "重复文字第二段第二处", Element(source, 2), "市场法", "原文范围：Start=6，Length=3");
                var spaces = check.Issues.SelectMany(i => i.Occurrences).First(o => o.DocumentLabel == source.Label &&
                    o.Evidence.Any(e => e.Contains("字符偏移=2；长度=2")));
                Check(report, service, source, "连续空格（生产问题）", spaces);
                var tab = check.Issues.SelectMany(i => i.Occurrences).First(o => o.DocumentLabel == source.Label &&
                    o.Evidence.Any(e => e.Contains("Unicode=U+0009")));
                Check(report, service, source, "制表符（生产问题）", tab);
                if (!source.IsUnchanged()) throw new InvalidOperationException("验收前后 SHA-256 不一致。");
                report.AppendLine(source.Label + " SHA-256 unchanged=" + source.Sha256);
            }
            string result = report.ToString();
            File.WriteAllText(Path.Combine(outputDirectory, "wps-navigation-result.txt"), result, Encoding.UTF8);
            return result;
        }

        private static DocumentElement Element(CheckedDocumentSource source, int index) =>
            source.Document.Elements.Single(e => e.Location.ParagraphIndex == index);

        private static void Check(StringBuilder report, SourceNavigationService service, CheckedDocumentSource source,
            string name, DocumentElement element, string raw, string evidence = null)
        {
            var occurrence = new ReportCheckOccurrence(raw, raw, element.Location, "结构定位", "正文/表格", "",
                evidence == null ? null : new[] { evidence }, null, element.Text, false, documentLabel: source.Label);
            Check(report, service, source, name, occurrence);
        }

        private static void Check(StringBuilder report, SourceNavigationService service, CheckedDocumentSource source,
            string name, ReportCheckOccurrence occurrence)
        {
            Console.WriteLine("WPS smoke: " + source.Label + " / " + name);
            var result = service.NavigateAsync(source, occurrence).GetAwaiter().GetResult();
            Console.WriteLine(result.Status + ": " + result.Message);
            report.AppendLine(source.Label + " / " + name + " = " + result.Status + " / " + result.Message);
            if (result.Status != SourceNavigationStatus.Exact)
                throw new InvalidOperationException("真实 WPS 未确认精确选区：" + name + "。" + result.Message);
        }

        private static void Create(string path, string suffix)
        {
            using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new Body(
                    P("合成" + suffix + "评估价值为998.22万元。"),
                    P("采用市场法和市场法。"), P("采用市场法和市场法。"),
                    P("正文  存在连续空格。"),
                    new Paragraph(new Run(new Text("正文"), new TabChar(), new Text("异常制表符。"))),
                    new Table(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Dxa, Width = "8000" }),
                        new TableGrid(new GridColumn { Width = "4000" }, new GridColumn { Width = "4000" }),
                        new TableRow(C("字段"), C("数值")), new TableRow(C("评估价值"), C("998.22万元"))),
                    P("合成定位验收结束。")));
            }
        }
        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        private static TableCell C(string text) => new TableCell(P(text));

        private sealed class DiagnosedWpsAdapter : IWordNavigationAdapter
        {
            private readonly WpsNavigationAdapter _adapter = new WpsNavigationAdapter();
            public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken token)
            {
                try { return _adapter.Navigate(target, token); }
                catch (Exception e)
                {
                    // Only type/HRESULT/stack, never an exception message containing document contents.
                    Console.WriteLine("WPS diagnostic: " + e.GetType().Name + " HRESULT=0x" + e.HResult.ToString("X8"));
                    if (e.InnerException != null) Console.WriteLine("Inner: " + e.InnerException.GetType().Name + " HRESULT=0x" + e.InnerException.HResult.ToString("X8"));
                    Console.WriteLine(e.StackTrace);
                    throw;
                }
            }
        }
    }
}
