using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class FontStyleTablePeerTests
    {
        [TestMethod]
        [DataRow(1, 0)]
        [DataRow(2, 0)]
        [DataRow(1, 8)]
        public async Task SingleRecord_SerialAndAmountColumns_DoNotCreateFontOutlier(int tablesA, int tablesB)
        {
            using (SyntheticDocxFile a = SyntheticDocxFile.Create(Tables(tablesA)))
            using (SyntheticDocxFile b = SyntheticDocxFile.Create(Tables(tablesB)))
            {
                byte[] hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(
                    a.FilePath, tablesB == 0 ? null : b.FilePath, null), null);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.Issues.Count(issue => issue.GovernanceKind == "FontStyleOutlier"));
                CollectionAssert.AreEqual(hashA, Hash(a.FilePath));
                CollectionAssert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        [TestMethod]
        public void MultipleRecords_DifferentColumnSizes_AreNotCompared()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(Detail(i));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void SameDetailColumn_IsolatedSizeDifference_RetainsIndependentPeersAndSource()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(Detail(i, i == 2 ? "21" : "22"));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                byte[] before = Hash(file.FilePath);
                foreach (string source in new[] { "文档 A", "文档 B" })
                {
                    MechanicalIssue issue = Check(file, source).Single();
                    Assert.AreEqual(MechanicalIssueKind.FontStyleOutlier, issue.Kind);
                    Assert.AreEqual(3, issue.Occurrences[0].Location.RowIndex);
                    Assert.IsTrue(issue.Occurrences.All(item => item.SourceDocument == source));
                    Assert.IsTrue(issue.Occurrences.All(item => item.Location.TableIndex == 0 && item.Location.CellIndex == 2));
                    Assert.IsTrue(issue.Occurrences.Select(item => item.Location.RowIndex).Distinct().Count() >= 3);
                    Assert.IsTrue(issue.Occurrences[0].Evidence.Any(item => item.Contains("字号")));
                }
                CollectionAssert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public void HeaderDetailAndTotal_AreSeparateRoles()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(Detail(i));
            table.Append(Total("26"));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void SameTotalColumn_StablePeersStillDetectOutlier()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(Total(i == 2 ? "21" : "22"));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.CellIndex == 2 && item.Location.RowIndex >= 1));
            }
        }

        [TestMethod]
        public void DifferentMergeLayouts_AreNotBorrowedToReachMinimumPeers()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(Detail(i));
            table.Append(new TableRow(new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("合成合并内容", "22")),
                new TableCell(P("100.00", "21"))));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void StableHorizontalMerge_PreservesSameLogicalColumnOutlier()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(new TableRow(
                new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("合成项目", "22")),
                new TableCell(P("100.00", i == 2 ? "21" : "22"))));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
                Assert.IsTrue(Check(file).Single().Occurrences.All(item => item.Location.CellIndex == 1));
        }

        [TestMethod]
        public void VerticalMergeAndUncertainGrid_FailClosed()
        {
            Table table = Header();
            for (int i = 0; i < 5; i++) table.Append(Detail(i));
            TableRow vertical = Detail(6, "21");
            vertical.Elements<TableCell>().Last().PrependChild(new TableCellProperties(new VerticalMerge { Val = MergedCellValues.Restart }));
            table.Append(vertical);
            TableRow uncertain = Detail(7, "21");
            uncertain.PrependChild(new TableRowProperties(new GridBefore { Val = 1 }));
            table.Append(uncertain);
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void NestedTable_UsesOwnIdentityAndPreservesInnerOutlier()
        {
            Table inner = Header();
            for (int i = 0; i < 5; i++) inner.Append(Detail(i, i == 2 ? "21" : "22"));
            Table outer = new Table(new TableRow(new TableCell(inner, P("合成外层", "22"))));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(outer, Header()))
                Assert.IsTrue(Check(file).Single().Occurrences.All(item => item.Location.TableIndex == 1 && item.Location.CellIndex == 2));
        }

        [TestMethod]
        public void ManyParagraphsInOneCell_DoNotManufactureIndependentRowPeers()
        {
            Table table = Header();
            table.Append(new TableRow(new TableCell(Enumerable.Range(0, 6).Select(i => P("100.00", i == 2 ? "21" : "22")).ToArray())));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        private static OpenXmlElement[] Tables(int count)
        {
            if (count == 0) return new OpenXmlElement[] { P("合成普通正文。", "22") };
            return Enumerable.Range(0, count).Select(index =>
            {
                var table = new Table(new TableRow(new TableRowProperties(new TableHeader()),
                    new TableCell(P("序号", "24")), new TableCell(P("项目名称", "24")),
                    new TableCell(P("账面原值", "24")), new TableCell(P("账面净值", "24")),
                    new TableCell(P("评估原值", "24")), new TableCell(P("评估净值", "24"))));
                table.Append(new TableRow(new TableCell(P("1", "21")), new TableCell(P("合成设备", "22")),
                    new TableCell(P("100.00", "22")), new TableCell(P("90.00", "22")),
                    new TableCell(P("110.00", "22")), new TableCell(P("95.00", "22"))));
                table.Append(new TableRow(new TableCell(P("合计", "24")), new TableCell(P("100.00", "22")),
                    new TableCell(P("90.00", "22")), new TableCell(P("110.00", "22")), new TableCell(P("95.00", "22"))));
                return (OpenXmlElement)table;
            }).ToArray();
        }

        private static Table Header() => new Table(new TableRow(new TableRowProperties(new TableHeader()),
            new TableCell(P("序号", "24")), new TableCell(P("项目名称", "24")), new TableCell(P("金额", "24"))));
        private static TableRow Detail(int index, string size = "22") => new TableRow(
            new TableCell(P((index + 1).ToString(CultureInfo.InvariantCulture), "21")),
            new TableCell(P("合成项目", "22")), new TableCell(P("100.00", size)));
        private static TableRow Total(string size) => new TableRow(new TableCell(P("合计", "24")),
            new TableCell(P("合成项目", "24")), new TableCell(P("100.00", size)));
        private static Paragraph P(string text, string size) => new Paragraph(new Run(new RunProperties(
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial", EastAsia = "宋体" }, new FontSize { Val = size }), new Text(text)));
        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file, string source = "文档 A") =>
            new FontStyleChecker().Check(new MechanicalCheckContext(file.FilePath, new DocxDocumentReader().Read(file.FilePath), source));
        private static byte[] Hash(string path)
        {
            using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream);
        }
    }
}
