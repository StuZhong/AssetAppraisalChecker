using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Document
{
    [TestClass]
    public sealed class DocxDocumentReaderTests
    {
        private readonly DocxDocumentReader _reader = new DocxDocumentReader();

        [TestMethod]
        public void Read_OrdinaryDocx_ReturnsBodyParagraph()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(ParagraphWithText("本文件仅用于自动化测试。")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(1, result.ParagraphCount);
                Assert.AreEqual("本文件仅用于自动化测试。", result.Elements.Single().Text);
                Assert.AreEqual(DocumentElementType.Paragraph, result.Elements.Single().ElementType);
            }
        }

        [TestMethod]
        public void Read_MultipleParagraphs_PreservesParagraphAndOrderIndexes()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("虚构段落A"),
                ParagraphWithText("虚构段落B"),
                ParagraphWithText("虚构段落C")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                CollectionAssert.AreEqual(
                    new[] { "虚构段落A", "虚构段落B", "虚构段落C" },
                    result.Elements.Select(element => element.Text).ToArray());
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2 },
                    result.Elements.Select(element => element.Location.ParagraphIndex.Value).ToArray());
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2 },
                    result.Elements.Select(element => element.DocumentOrderIndex).ToArray());
            }
        }

        [TestMethod]
        public void Read_TextSplitAcrossRuns_MergesVisibleText()
        {
            var paragraph = new Paragraph(
                new Run(new Text("评估")),
                new Run(new Text("净值为")),
                new Run(new Text("170")),
                new Run(new Text("万元")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(paragraph))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual("评估净值为170万元", result.Elements.Single().Text);
            }
        }

        [TestMethod]
        public void Read_TabAndLineBreak_PreservesVisibleSeparators()
        {
            var paragraph = new Paragraph(
                new Run(new Text("项目"), new TabChar(), new Text("金额"), new Break(), new Text("170万元")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(paragraph))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual("项目\t金额\n170万元", result.Elements.Single().Text);
            }
        }

        [TestMethod]
        public void Read_Table_RecordsTableRowAndCellIndexes()
        {
            var table = new Table(
                Row("项目", "评估原值", "评估净值"),
                Row("测试设备A", "38.58", "38.19"));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(1, result.TableCount);
                Assert.AreEqual(6, result.Elements.Count);
                DocumentElement valueCell = result.Elements.Single(element => element.Text == "38.19");
                Assert.AreEqual(DocumentElementType.TableCell, valueCell.ElementType);
                Assert.AreEqual(0, valueCell.Location.TableIndex);
                Assert.AreEqual(1, valueCell.Location.RowIndex);
                Assert.AreEqual(2, valueCell.Location.CellIndex);
                Assert.IsNull(valueCell.Location.ParagraphIndex);
            }
        }

        [TestMethod]
        public void Read_MultipleTables_AssignsDistinctTableIndexes()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(Row("虚构表格一")),
                new Table(Row("虚构表格二"))))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(2, result.TableCount);
                CollectionAssert.AreEqual(
                    new[] { 0, 1 },
                    result.Elements.Select(element => element.Location.TableIndex.Value).ToArray());
            }
        }

        [TestMethod]
        public void Read_InterleavedParagraphsAndTables_PreservesDocumentOrder()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("正文A"),
                new Table(Row("表格一")),
                ParagraphWithText("正文B"),
                new Table(Row("表格二")),
                ParagraphWithText("正文C")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                CollectionAssert.AreEqual(
                    new[] { "正文A", "表格一", "正文B", "表格二", "正文C" },
                    result.Elements.Select(element => element.Text).ToArray());
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2, 3, 4 },
                    result.Elements.Select(element => element.DocumentOrderIndex).ToArray());
                Assert.AreEqual("正文A", result.Elements[1].NearbyContext.PreviousText);
                Assert.AreEqual("正文B", result.Elements[1].NearbyContext.NextText);
            }
        }

        [TestMethod]
        public void Read_TableAfterUnitParagraph_PreservesUnitTextAsNearbyContextOnly()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("金额单位：万元（虚构测试）"),
                new Table(Row("评估净值", "38.19"))))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                DocumentElement firstCell = result.Elements.Single(element => element.Text == "评估净值");
                Assert.AreEqual("金额单位：万元（虚构测试）", firstCell.NearbyContext.PreviousText);
                Assert.AreEqual("评估净值", firstCell.Text);
                Assert.AreEqual("38.19", result.Elements.Single(element => element.Text == "38.19").Text);
            }
        }

        [TestMethod]
        public void Read_CellWithMultipleParagraphs_JoinsWithLineFeed()
        {
            var cell = new TableCell(
                ParagraphWithText("第一段虚构文字"),
                ParagraphWithText("第二段虚构文字"));
            var table = new Table(new TableRow(cell));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual("第一段虚构文字\n第二段虚构文字", result.Elements.Single().Text);
            }
        }

        [TestMethod]
        public void Read_ExplicitPageBreak_RecordsBoundaryAndFirstPageRelations()
        {
            var boundaryParagraph = new Paragraph(
                new Run(
                    new Text("首页末段"),
                    new Break { Type = BreakValues.Page },
                    new Text("次页开头")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("首页正文"),
                boundaryParagraph,
                ParagraphWithText("次页正文")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(3, result.Elements.Count);
                Assert.IsTrue(result.Elements[0].BoundaryMetadata.IsBeforeFirstPageBoundary);
                Assert.IsTrue(result.Elements[1].BoundaryMetadata.ContainsExplicitPageBreak);
                Assert.AreEqual(1, result.Elements[1].BoundaryMetadata.ExplicitPageBreakCount);
                Assert.IsTrue(result.Elements[1].BoundaryMetadata.ContainsFirstPageBoundary);
                Assert.IsTrue(result.Elements[2].BoundaryMetadata.IsAfterFirstPageBoundary);
                Assert.AreEqual("首页末段\n次页开头", result.Elements[1].Text);
            }
        }

        [TestMethod]
        public void Read_NextPageSectionBreak_RecordsBoundaryAfterParagraph()
        {
            var sectionEnd = new Paragraph(
                new ParagraphProperties(
                    new SectionProperties(
                        new SectionType { Val = SectionMarkValues.NextPage })),
                new Run(new Text("首页末段")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("首页正文"),
                sectionEnd,
                ParagraphWithText("次页正文")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                DocumentBoundaryMetadata boundary = result.Elements[1].BoundaryMetadata;
                Assert.AreEqual(DocumentSectionBreakKind.NextPage, boundary.SectionBreakKind);
                Assert.IsTrue(boundary.HasReliablePageBoundaryAfter);
                Assert.IsTrue(boundary.IsBeforeFirstPageBoundary);
                Assert.IsFalse(boundary.ContainsFirstPageBoundary);
                Assert.IsTrue(result.Elements[2].BoundaryMetadata.IsAfterFirstPageBoundary);
            }
        }

        [TestMethod]
        public void Read_SectionBreakWithoutType_DefaultsToNextPageBoundary()
        {
            var sectionEnd = new Paragraph(
                new ParagraphProperties(new SectionProperties()),
                new Run(new Text("首页末段")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                sectionEnd,
                ParagraphWithText("次页正文")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                DocumentBoundaryMetadata boundary = result.Elements[0].BoundaryMetadata;
                Assert.AreEqual(DocumentSectionBreakKind.NextPage, boundary.SectionBreakKind);
                Assert.IsTrue(boundary.HasReliablePageBoundaryAfter);
                Assert.IsTrue(boundary.IsBeforeFirstPageBoundary);
                Assert.IsTrue(result.Elements[1].BoundaryMetadata.IsAfterFirstPageBoundary);
            }
        }

        [TestMethod]
        public void Read_OrdinaryLineBreak_DoesNotBecomePageBoundary()
        {
            var paragraph = new Paragraph(
                new Run(new Text("第一行"), new Break(), new Text("第二行")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                paragraph,
                ParagraphWithText("后续正文")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual("第一行\n第二行", result.Elements[0].Text);
                Assert.AreEqual(0, result.Elements[0].BoundaryMetadata.ExplicitPageBreakCount);
                Assert.IsFalse(result.Elements[0].BoundaryMetadata.ContainsExplicitPageBreak);
                Assert.IsFalse(result.Elements[0].BoundaryMetadata.DocumentHasReliableFirstPageBoundary);
                Assert.AreEqual(
                    FirstPageBoundaryRelation.NoReliableBoundary,
                    result.Elements[1].BoundaryMetadata.FirstPageRelation);
            }
        }

        [TestMethod]
        public void Read_ContinuousSectionBreak_IsRecordedButDoesNotCreatePageBoundary()
        {
            var sectionEnd = new Paragraph(
                new ParagraphProperties(
                    new SectionProperties(
                        new SectionType { Val = SectionMarkValues.Continuous })),
                new Run(new Text("连续分节前正文")));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                sectionEnd,
                ParagraphWithText("连续分节后正文")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                DocumentBoundaryMetadata metadata = result.Elements[0].BoundaryMetadata;
                Assert.AreEqual(DocumentSectionBreakKind.Continuous, metadata.SectionBreakKind);
                Assert.IsFalse(metadata.HasReliablePageBoundaryAfter);
                Assert.AreEqual(
                    FirstPageBoundaryRelation.NoReliableBoundary,
                    metadata.FirstPageRelation);
                Assert.IsFalse(result.Elements[1].BoundaryMetadata.IsAfterFirstPageBoundary);
            }
        }

        [TestMethod]
        public void Read_DocumentWithoutBoundary_PreservesExistingTextIndexesAndContext()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("正文A"),
                new Table(Row("表格内容")),
                ParagraphWithText("正文B")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                CollectionAssert.AreEqual(
                    new[] { "正文A", "表格内容", "正文B" },
                    result.Elements.Select(item => item.Text).ToArray());
                CollectionAssert.AreEqual(
                    new[] { 0, 1, 2 },
                    result.Elements.Select(item => item.DocumentOrderIndex).ToArray());
                Assert.AreEqual("正文A", result.Elements[1].NearbyContext.PreviousText);
                Assert.AreEqual("正文B", result.Elements[1].NearbyContext.NextText);
                Assert.IsTrue(result.Elements.All(item =>
                    item.BoundaryMetadata.FirstPageRelation ==
                        FirstPageBoundaryRelation.NoReliableBoundary));
            }
        }

        [TestMethod]
        public void Read_HeadingStyle_RecognizesHeadingElement()
        {
            using (SyntheticDocxFile file = CreateHeadingDocument(
                StyledParagraph("Heading1", "评估说明"),
                ParagraphWithText("虚构正文")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(DocumentElementType.Heading, result.Elements[0].ElementType);
                Assert.AreEqual("Heading1", result.Elements[0].StyleId);
                CollectionAssert.AreEqual(new[] { "评估说明" }, result.Elements[0].HeadingPath.ToArray());
            }
        }

        [TestMethod]
        public void Read_NestedHeadings_MaintainsHeadingPathForFollowingContent()
        {
            using (SyntheticDocxFile file = CreateHeadingDocument(
                StyledParagraph("Heading1", "评估说明"),
                StyledParagraph("Heading2", "评估结论"),
                ParagraphWithText("评估净值为170万元。"),
                new Table(Row("虚构表格内容"))))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                DocumentElement paragraph = result.Elements.Single(element => element.Text == "评估净值为170万元。");
                DocumentElement cell = result.Elements.Single(element => element.Text == "虚构表格内容");
                CollectionAssert.AreEqual(new[] { "评估说明", "评估结论" }, paragraph.HeadingPath.ToArray());
                CollectionAssert.AreEqual(new[] { "评估说明", "评估结论" }, cell.HeadingPath.ToArray());
            }
        }

        [TestMethod]
        public void Read_LocalizedHeadingStyleName_RecognizesHeading()
        {
            var localizedStyle = new Style(
                new StyleName { Val = "标题 1" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "LocalizedHeadingOne"
            };

            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(
                new[] { localizedStyle },
                StyledParagraph("LocalizedHeadingOne", "虚构标题")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(DocumentElementType.Heading, result.Elements.Single().ElementType);
            }
        }

        [TestMethod]
        public void Read_EmptyParagraph_KeepsStructuralIndexesAndUsesEffectiveContext()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("前一有效段落"),
                new Paragraph(),
                ParagraphWithText("后一有效段落")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(3, result.ParagraphCount);
                Assert.AreEqual(3, result.Elements.Count);
                Assert.AreEqual(string.Empty, result.Elements[1].Text);
                Assert.AreEqual(1, result.Elements[1].Location.ParagraphIndex);
                Assert.AreEqual("前一有效段落", result.Elements[1].NearbyContext.PreviousText);
                Assert.AreEqual("后一有效段落", result.Elements[1].NearbyContext.NextText);
            }
        }

        [TestMethod]
        public void Read_EmptyTableAndCell_DoesNotFail()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(),
                new Table(new TableRow(new TableCell(new Paragraph())))))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(2, result.TableCount);
                Assert.AreEqual(1, result.Elements.Count);
                Assert.AreEqual(string.Empty, result.Elements[0].Text);
                Assert.AreEqual(1, result.Elements[0].Location.TableIndex);
            }
        }

        [TestMethod]
        public void Read_MergedCells_PreservesUnderlyingCellsWithoutSynthesizingText()
        {
            var horizontalCell = new TableCell(
                new TableCellProperties(new GridSpan { Val = 2 }),
                ParagraphWithText("横向合并虚构标题"));
            var verticalStart = new TableCell(
                new TableCellProperties(new VerticalMerge { Val = MergedCellValues.Restart }),
                ParagraphWithText("纵向合并虚构内容"));
            var verticalContinuation = new TableCell(
                new TableCellProperties(new VerticalMerge()),
                new Paragraph());
            var table = new Table(
                new TableRow(horizontalCell),
                new TableRow(verticalStart),
                new TableRow(verticalContinuation));

            using (SyntheticDocxFile file = SyntheticDocxFile.Create(table))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(3, result.Elements.Count);
                Assert.AreEqual(1, result.Elements.Count(element => element.Text == "横向合并虚构标题"));
                Assert.AreEqual(1, result.Elements.Count(element => element.Text == "纵向合并虚构内容"));
            }
        }

        [TestMethod]
        public void Read_DocExtension_IsSafelyRejectedWithoutConversion()
        {
            DocumentReadResult result = _reader.Read("example.doc");

            Assert.AreEqual(DocumentReadStatus.UnsupportedFileType, result.Status);
            StringAssert.Contains(result.Message, "请先使用 WPS 将文件另存为 .docx");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void Read_EmptyPath_IsSafelyRejected(string filePath)
        {
            DocumentReadResult result = _reader.Read(filePath);

            Assert.AreEqual(DocumentReadStatus.InvalidPath, result.Status);
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public void Read_MissingDocx_ReturnsFileNotFound()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".docx");

            DocumentReadResult result = _reader.Read(path);

            Assert.AreEqual(DocumentReadStatus.FileNotFound, result.Status);
        }

        [TestMethod]
        public void Read_FakeDocx_ReturnsInvalidDocx()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(Encoding.UTF8.GetBytes("这不是 DOCX，仅为虚构错误测试。")))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(DocumentReadStatus.InvalidDocx, result.Status);
                StringAssert.Contains(result.Message, "文件可能已损坏或不是有效的 DOCX 文件");
            }
        }

        [TestMethod]
        public void Read_EmptyDocx_ReturnsInvalidDocx()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(new byte[0]))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(DocumentReadStatus.InvalidDocx, result.Status);
            }
        }

        [TestMethod]
        public void Read_TruncatedDocx_ReturnsInvalidDocx()
        {
            byte[] complete;
            using (SyntheticDocxFile validFile = SyntheticDocxFile.Create(ParagraphWithText("虚构损坏测试")))
            {
                complete = File.ReadAllBytes(validFile.FilePath);
            }

            using (SyntheticDocxFile corruptFile = SyntheticDocxFile.CreateRaw(complete.Take(complete.Length / 2).ToArray()))
            {
                DocumentReadResult result = _reader.Read(corruptFile.FilePath);

                Assert.AreEqual(DocumentReadStatus.InvalidDocx, result.Status);
            }
        }

        [TestMethod]
        [DataRow("0Table")]
        [DataRow("1Table")]
        public void Read_LegacyWordBinaryDisguisedAsDocx_ReturnsUnsupportedWithoutChangingFile(
            string tableStreamName)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(
                SyntheticCompoundFile.Create("WordDocument", tableStreamName)))
            {
                string before = ComputeSha256(file.FilePath);
                DocumentReadResult result = _reader.Read(file.FilePath);
                string after = ComputeSha256(file.FilePath);

                Assert.AreEqual(DocumentReadStatus.UnsupportedFileType, result.Status);
                Assert.AreEqual(
                    "文件内容实际为旧版 .doc，请使用 WPS 另存为标准 .docx 后再校检。",
                    result.Message);
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public void Read_SyntheticEncryptedOfficeContainer_ReturnsEncryptedWithoutChangingFile()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(
                SyntheticCompoundFile.Create("EncryptedPackage", "EncryptionInfo")))
            {
                string before = ComputeSha256(file.FilePath);
                DocumentReadResult result = _reader.Read(file.FilePath);
                string after = ComputeSha256(file.FilePath);

                Assert.AreEqual(DocumentReadStatus.EncryptedOrProtected, result.Status);
                StringAssert.Contains(result.Message, "不会尝试破解密码");
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public void Read_UnknownCompoundFile_ReturnsInvalidDocxWithoutChangingFile()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(
                SyntheticCompoundFile.Create("UnknownTestStream")))
            {
                string before = ComputeSha256(file.FilePath);
                DocumentReadResult result = _reader.Read(file.FilePath);
                string after = ComputeSha256(file.FilePath);

                Assert.AreEqual(DocumentReadStatus.InvalidDocx, result.Status);
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public void Read_FileLockedForWriting_ReturnsFileInUse()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(ParagraphWithText("虚构占用测试")))
            using (new FileStream(file.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(DocumentReadStatus.FileInUse, result.Status);
            }
        }

        [TestMethod]
        public void Read_ValidDocx_DoesNotChangeSha256()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("XX测试资产评估项目"),
                new Table(Row("测试设备A", "170.00"))))
            {
                string before = ComputeSha256(file.FilePath);

                DocumentReadResult result = _reader.Read(file.FilePath);

                string after = ComputeSha256(file.FilePath);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public void Read_SameDocxTwice_ReturnsEquivalentResults()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("虚构重复读取正文"),
                new Table(Row("测试设备A", "170.00"))))
            {
                DocumentReadResult first = _reader.Read(file.FilePath);
                DocumentReadResult second = _reader.Read(file.FilePath);

                Assert.AreEqual(first.Status, second.Status);
                Assert.AreEqual(first.ParagraphCount, second.ParagraphCount);
                Assert.AreEqual(first.TableCount, second.TableCount);
                CollectionAssert.AreEqual(Snapshot(first), Snapshot(second));
            }
        }

        [TestMethod]
        public void Read_ChineseParagraph_PreservesUnicodeText()
        {
            const string text = "评估结论：人民币壹佰柒拾万元整。本信息完全虚构。";
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(ParagraphWithText(text)))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                Assert.AreEqual(text, result.Elements.Single().Text);
            }
        }

        [TestMethod]
        public void Read_ChineseTable_PreservesUnicodeText()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(Row("序号", "资产名称", "评估净值"), Row("1", "测试设备A", "170.00"))))
            {
                DocumentReadResult result = _reader.Read(file.FilePath);

                CollectionAssert.AreEqual(
                    new[] { "序号", "资产名称", "评估净值", "1", "测试设备A", "170.00" },
                    result.Elements.Select(element => element.Text).ToArray());
            }
        }

        private static SyntheticDocxFile CreateHeadingDocument(params OpenXmlElement[] bodyElements)
        {
            var styles = new List<Style>
            {
                new Style(new StyleName { Val = "heading 1" })
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Heading1"
                },
                new Style(new StyleName { Val = "heading 2" })
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Heading2"
                }
            };

            return SyntheticDocxFile.CreateWithStyles(styles, bodyElements);
        }

        private static Paragraph StyledParagraph(string styleId, string text)
        {
            return new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
                new Run(new Text(text)));
        }

        private static Paragraph ParagraphWithText(string text)
        {
            return new Paragraph(new Run(new Text(text)));
        }

        private static TableRow Row(params string[] cellTexts)
        {
            return new TableRow(cellTexts.Select(text => new TableCell(ParagraphWithText(text))));
        }

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string[] Snapshot(DocumentReadResult result)
        {
            return result.Elements.Select(element => string.Join(
                "|",
                element.DocumentOrderIndex,
                element.ElementType,
                element.Text,
                element.Location.ParagraphIndex,
                element.Location.TableIndex,
                element.Location.RowIndex,
                element.Location.CellIndex,
                string.Join(">", element.HeadingPath),
                element.NearbyContext.PreviousText,
                element.NearbyContext.CurrentText,
                element.NearbyContext.NextText)).ToArray();
        }
    }
}
