using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class ParagraphFormatTablePeerTests
    {
        [TestMethod]
        public void ColumnsWithDifferentAlignment_HeaderDetailAndTotal_DoNotReport()
        {
            var table = Header();
            for (int index = 0; index < 6; index++) table.Append(Detail(index));
            TableRow total = Total(JustificationValues.Center);
            foreach (Run run in total.Descendants<Run>()) run.RunProperties = new RunProperties(new Bold());
            total.Descendants<Paragraph>().Last().ParagraphProperties.Append(new SpacingBetweenLines { Before = "240" });
            table.Append(total);
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                byte[] before = Hash(file.FilePath);
                Assert.AreEqual(0, Check(file, "文档 A").Count);
                Assert.AreEqual(0, Check(file, "文档 B").Count);
                CollectionAssert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public void DifferentColumnsCannotFillInsufficientPeers()
        {
            var table = Header();
            table.Append(Detail(0, JustificationValues.Center), Detail(1), Detail(2), Detail(3));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void DifferentTablesCannotFillInsufficientPeers()
        {
            var left = Header();
            left.Append(Detail(0, JustificationValues.Center), Detail(1), Detail(2));
            var right = Header();
            right.Append(Detail(3), Detail(4), Detail(5));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(left, right)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void DetailOutlier_UsesOnlySameColumnIndependentDetailRows()
        {
            var table = Header();
            for (int index = 0; index < 5; index++)
                table.Append(Detail(index, index == 2 ? JustificationValues.Center : JustificationValues.Right));
            table.Append(Total(JustificationValues.Center));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                MechanicalIssue issue = Check(file, "文档 B").Single();
                Assert.AreEqual(MechanicalIssueKind.ParagraphFormatOutlier, issue.Kind);
                Assert.AreEqual(3, issue.Occurrences[0].Location.RowIndex);
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.TableIndex == 0 && item.Location.CellIndex == 2));
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.RowIndex >= 1 && item.Location.RowIndex <= 5));
                Assert.IsTrue(issue.Occurrences.All(item => item.SourceDocument == "文档 B"));
                StringAssert.Contains(issue.Reason, "同一逻辑列");
            }
        }

        [TestMethod]
        public void SameClassTotalOutlier_IsStillDetected()
        {
            var table = Header();
            for (int index = 0; index < 5; index++)
                table.Append(Total(index == 2 ? JustificationValues.Center : JustificationValues.Right));
            table.Append(Detail(0, JustificationValues.Center));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.CellIndex == 2 && item.Location.RowIndex <= 5));
            }
        }

        [TestMethod]
        public void SubtotalAndTotal_AreSeparateRoles()
        {
            var table = Header();
            for (int index = 0; index < 5; index++) table.Append(Total(JustificationValues.Right));
            TableRow subtotal = Total(JustificationValues.Center);
            subtotal.GetFirstChild<TableCell>().RemoveAllChildren<Paragraph>();
            subtotal.GetFirstChild<TableCell>().Append(P("小计", JustificationValues.Left));
            table.Append(subtotal);
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void MultipleParagraphsInOneCell_DoNotManufactureIndependentPeers()
        {
            var table = Header();
            TableRow row = Detail(0);
            TableCell cell = row.Elements<TableCell>().Last();
            for (int index = 0; index < 6; index++) cell.Append(P("100.00", index == 2 ? JustificationValues.Center : JustificationValues.Right));
            table.Append(row);
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void LogicalColumnAndMergeLayout_DoNotBorrowOrdinaryCellPeers()
        {
            var table = Header();
            for (int index = 0; index < 5; index++) table.Append(Detail(index));
            table.Append(new TableRow(
                new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("合成项目", JustificationValues.Left)),
                new TableCell(P("100.00", JustificationValues.Center))));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void VerticalMergeAndNestedCells_FailClosedWhenPeersUncertain()
        {
            var table = Header();
            for (int index = 0; index < 5; index++) table.Append(Detail(index));
            TableRow merged = Detail(6, JustificationValues.Center);
            merged.Elements<TableCell>().Last().PrependChild(new TableCellProperties(new VerticalMerge { Val = MergedCellValues.Restart }));
            table.Append(merged);
            TableRow nested = Detail(7, JustificationValues.Center);
            nested.Elements<TableCell>().Last().Append(new Table(new TableRow(new TableCell(P("合成嵌套", JustificationValues.Left)))));
            table.Append(nested);
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void StableHorizontalMergeLayout_RetainsSameLogicalColumnOutlier()
        {
            var table = Header();
            for (int index = 0; index < 5; index++)
                table.Append(new TableRow(
                    new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("合成项目", JustificationValues.Left)),
                    new TableCell(P("100.00", index == 2 ? JustificationValues.Center : JustificationValues.Right))));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.CellIndex == 1 && item.Location.RowIndex >= 1));
            }
        }

        [TestMethod]
        public void UncertainLeadingGridOffset_DoesNotBorrowPeers()
        {
            var table = Header();
            for (int index = 0; index < 5; index++) table.Append(Detail(index));
            TableRow uncertain = Detail(6, JustificationValues.Center);
            uncertain.PrependChild(new TableRowProperties(new GridBefore { Val = 1 }));
            table.Append(uncertain);
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void NestedTableIdentity_MatchesSnapshotPreorder_AndRetainsInnerOutlier()
        {
            var inner = Header();
            for (int index = 0; index < 5; index++) inner.Append(Detail(index, index == 2 ? JustificationValues.Center : JustificationValues.Right));
            var outer = new Table(new TableRow(new TableCell(inner, P("合成外层", JustificationValues.Left))));
            var subsequent = Header();
            for (int index = 0; index < 5; index++) subsequent.Append(Detail(index));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(outer, subsequent))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.TableIndex == 1 && item.Location.CellIndex == 2));
            }
        }

        private static Table Header() => new Table(new TableRow(new TableRowProperties(new TableHeader()),
            new TableCell(P("序号", JustificationValues.Center)), new TableCell(P("项目名称", JustificationValues.Center)),
            new TableCell(P("金额", JustificationValues.Center))));

        private static TableRow Detail(int index) => Detail(index, JustificationValues.Right);

        private static TableRow Detail(int index, JustificationValues amount) => new TableRow(
            new TableCell(P((index + 1).ToString(), JustificationValues.Center)),
            new TableCell(P("合成项目", JustificationValues.Left)), new TableCell(P("100.00", amount)));

        private static TableRow Total(JustificationValues amount) => new TableRow(
            new TableCell(P("合计", JustificationValues.Left)), new TableCell(P("", JustificationValues.Left)),
            new TableCell(P("100.00", amount)));

        private static Paragraph P(string text, JustificationValues alignment) => new Paragraph(
            new ParagraphProperties(new Justification { Val = alignment }), new Run(new Text(text)));

        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file, string source = "文档 A")
        {
            var document = new DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(document.IsSuccess);
            return new ParagraphFormatChecker().Check(new MechanicalCheckContext(file.FilePath, document, source));
        }

        private static byte[] Hash(string path)
        {
            using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return sha.ComputeHash(stream);
        }
    }
}
