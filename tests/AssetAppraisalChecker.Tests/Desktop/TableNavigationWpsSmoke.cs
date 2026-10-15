using System;
using System.IO;
using System.Linq;
using System.Text;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Tests.Desktop
{
    /// <summary>
    /// Explicit interactive WPS check, not a unit test. Only synthetic DOCX are
    /// created outside Git. Neither the editor nor the documents are closed/saved.
    /// </summary>
    public static class TableNavigationWpsSmoke
    {
        public static string Run()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "AssetAppraisalChecker-TableNavigation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var output = new StringBuilder();
            foreach (string label in new[] { "文档 A", "文档 B" })
            {
                string path = Path.Combine(directory, label + "-合成表格.docx");
                Create(path, label == "文档 A" ? "甲" : "乙");
                CheckedDocumentSource source = SourceNavigationTests.Capture(path, label);
                var service = new SourceNavigationService(new WpsNavigationAdapter());
                Check(output, service, source, "普通单元格", 1, 0, null, "单元格正文", SourceNavigationStatus.Exact);
                Check(output, service, source, "单元格第二段", 1, 1, 1, "第二段内容", SourceNavigationStatus.ParagraphOnly,
                    SourceNavigationStatus.TableCell);
                Check(output, service, source, "空文字格式", 1, 2, 0, "", SourceNavigationStatus.TableCell);
                Check(output, service, source, "旁有横向合并的空格", 2, 1, 0, "", SourceNavigationStatus.TableCell);
                Check(output, service, source, "合并目标", 3, 0, null, "合并目标", SourceNavigationStatus.TableRow);
                Check(output, service, source, "嵌套目标", 4, 0, null, "外层", SourceNavigationStatus.Unavailable);
                if (!source.IsUnchanged()) throw new InvalidOperationException("合成 DOCX 的 SHA-256 发生变化。");
                output.AppendLine(label + " SHA-256 unchanged=True");
            }
            return output.ToString();
        }

        private static void Check(StringBuilder output, SourceNavigationService service,
            CheckedDocumentSource source, string scenario, int row, int cell, int? paragraph,
            string raw, params SourceNavigationStatus[] allowed)
        {
            var location = new DocumentLocation(DocumentPart.MainBody, paragraph, 0, row, cell, new string[0]);
            var occurrence = new ReportCheckOccurrence(raw, raw, location, "合成结构位置", "表格", "",
                null, null, "", false, documentLabel: source.Label);
            SourceNavigationResult result = service.NavigateAsync(source, occurrence).GetAwaiter().GetResult();
            output.AppendLine(source.Label + " / " + scenario + " = " + result.Status);
            if (!allowed.Contains(result.Status))
                throw new InvalidOperationException("合成表格导航未达到预期级别：" + scenario + " / " + result.Status +
                    " / " + result.Message + Environment.NewLine + output);
        }

        private static void Create(string path, string suffix)
        {
            using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                var table = new Table(new TableGrid(new GridColumn(), new GridColumn(), new GridColumn(), new GridColumn()),
                        new TableRow(C("字段"), C("说明"), C("空列"), C("尾列")),
                        new TableRow(C("单元格正文"), new TableCell(P("第一段内容"), P("第二段内容")),
                            C(""), C("相邻证据")),
                        new TableRow(C("左侧证据"), C(""),
                            new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("横向合并"))),
                        new TableRow(new TableCell(new TableCellProperties(new GridSpan { Val = 2 }),
                            P("合并目标")), C("邻列"), C("末列")),
                        new TableRow(new TableCell(P("外层"),
                            new Table(new TableRow(C("内层")))), C("邻列"), C("次列"), C("末列")));
                main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                    new Body(P("合成" + suffix + "定位验收。"), table));
            }
        }

        private static Paragraph P(string text) =>
            new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        private static TableCell C(string text) => new TableCell(P(text));
    }
}
