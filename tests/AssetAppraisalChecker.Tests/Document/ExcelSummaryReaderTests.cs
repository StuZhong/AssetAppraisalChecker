using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Document.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Document
{
    [TestClass]
    public sealed class ExcelSummaryReaderTests
    {
        private const string SummarySheetName = "资产评估汇总表";

        private static readonly ExcelSummaryField[] Fields =
        {
            ExcelSummaryField.AppraisedOriginalValueTaxExcluded,
            ExcelSummaryField.AppraisedNetValueTaxExcluded,
            ExcelSummaryField.AppraisedOriginalValueTaxIncluded,
            ExcelSummaryField.AppraisedNetValueTaxIncluded
        };

        private static readonly string[] Headers =
        {
            "评估原值（不含税）", "评估净值（不含税）", "评估原值（含税）", "评估净值（含税）"
        };

        private static readonly decimal[] RawAmounts =
        {
            199.171234m, 149.384567m, 215.7444m, 161.814321m
        };

        private static readonly decimal[] RoundedAmounts =
        {
            199.17m, 149.38m, 215.74m, 161.81m
        };

        private readonly ExcelSummaryReader _reader = new ExcelSummaryReader();

        [TestMethod]
        public void Read_SingleAssetAndTotal_ReturnsEightAmountsWithSourcesAndPreservesFile()
        {
            using (SyntheticWorkbook file = CreateWorkbook())
            {
                byte[] originalHash = ComputeHash(file.FilePath);

                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(8, result.Amounts.Count);
                for (int fieldIndex = 0; fieldIndex < Fields.Length; fieldIndex++)
                {
                    foreach (ExcelSummaryRowKind rowKind in new[] { ExcelSummaryRowKind.Asset, ExcelSummaryRowKind.Total })
                    {
                        ExcelSummaryAmount amount = result.Amounts.Single(
                            value => value.Field == Fields[fieldIndex] && value.RowKind == rowKind);
                        Assert.AreEqual(RawAmounts[fieldIndex], amount.RawAmountTenThousandYuan);
                        Assert.AreEqual(RoundedAmounts[fieldIndex], amount.RoundedAmountTenThousandYuan);
                        Assert.AreEqual(SummarySheetName, amount.WorksheetName);
                        Assert.AreEqual(ColumnName(3 + fieldIndex) + (rowKind == ExcelSummaryRowKind.Asset ? "6" : "7"), amount.CellAddress);
                    }
                }

                CollectionAssert.AreEqual(originalHash, ComputeHash(file.FilePath));
            }
        }

        [TestMethod]
        public void Read_OtherWorksheetContainsDifferentAmounts_UsesOnlyNamedSummarySheet()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, target) =>
            {
                WorksheetPart other = AddWorksheet(workbook, "评估结果汇总表（按省公司要求添加）", BuildWorksheet(3, false));
                foreach (Cell cell in other.Worksheet.Descendants<Cell>().Where(value => value.CellFormula != null))
                {
                    cell.CellValue = new CellValue("999.99");
                }
                AddWorksheet(workbook, "评估明细表", new Worksheet(new SheetData()));
            }))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(8, result.Amounts.Count);
                Assert.IsTrue(result.Amounts.All(amount => amount.WorksheetName == SummarySheetName));
                Assert.AreEqual(199.171234m, result.Amounts.Single(amount =>
                    amount.Field == Fields[0] && amount.RowKind == ExcelSummaryRowKind.Asset).RawAmountTenThousandYuan);
            }
        }

        [TestMethod]
        public void Read_AmountsMovedToDifferentColumns_UsesHeadersInsteadOfFixedAddresses()
        {
            using (SyntheticWorkbook file = CreateWorkbook(firstAmountColumn: 6))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(8, result.Amounts.Count);
                for (int index = 0; index < Fields.Length; index++)
                {
                    ExcelSummaryAmount amount = result.Amounts.Single(value =>
                        value.Field == Fields[index] && value.RowKind == ExcelSummaryRowKind.Asset);
                    Assert.AreEqual(ColumnName(6 + index) + "6", amount.CellAddress);
                    Assert.AreEqual(RawAmounts[index], amount.RawAmountTenThousandYuan);
                }
            }
        }

        [TestMethod]
        public void Read_MergedTaxHeadersAboveValueHeaders_IdentifiesAllFourFields()
        {
            using (SyntheticWorkbook file = CreateWorkbook(groupedHeaders: true))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(8, result.Amounts.Count);
                for (int index = 0; index < Fields.Length; index++)
                {
                    ExcelSummaryAmount amount = result.Amounts.Single(value =>
                        value.Field == Fields[index] && value.RowKind == ExcelSummaryRowKind.Asset);
                    Assert.AreEqual(ColumnName(3 + index) + "6", amount.CellAddress);
                    Assert.AreEqual(RoundedAmounts[index], amount.RoundedAmountTenThousandYuan);
                }
            }
        }

        [TestMethod]
        public void Read_LabelsStoredAsSharedStrings_ReadsHeadersUnitAndTotalRow()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                SharedStringTablePart stringsPart = workbook.AddNewPart<SharedStringTablePart>();
                stringsPart.SharedStringTable = new SharedStringTable();
                int stringIndex = 0;
                foreach (Cell cell in sheet.Worksheet.Descendants<Cell>().Where(value => value.InlineString != null))
                {
                    stringsPart.SharedStringTable.Append(new SharedStringItem(new Text(cell.InlineString.InnerText)));
                    cell.InlineString = null;
                    cell.DataType = CellValues.SharedString;
                    cell.CellValue = new CellValue(stringIndex.ToString(CultureInfo.InvariantCulture));
                    stringIndex++;
                }
                stringsPart.SharedStringTable.Save();
            }))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(8, result.Amounts.Count);
                Assert.AreEqual(4, result.Amounts.Count(amount => amount.RowKind == ExcelSummaryRowKind.Asset));
                Assert.AreEqual(4, result.Amounts.Count(amount => amount.RowKind == ExcelSummaryRowKind.Total));
                Assert.AreEqual(199.171234m, result.Amounts.Single(amount =>
                    amount.Field == Fields[0] && amount.RowKind == ExcelSummaryRowKind.Asset).RawAmountTenThousandYuan);
            }
        }

        [TestMethod]
        [DataRow("0.00000000000000000000000000001")]
        [DataRow("0.123456789012345678901234567891")]
        public void Read_CacheExceedsDecimalPrecision_FailsWithoutUnderflowOrSilentRounding(string raw)
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                Cell cell = FindCell(sheet, "C6");
                cell.CellFormula = new CellFormula(raw);
                cell.CellValue = new CellValue(raw);
            }))
            {
                AssertFailed(_reader.Read(file.FilePath));
            }
        }

        [TestMethod]
        [DataRow("#,##0.00;[Red]-#,##0.00;\"-\";@", true)]
        [DataRow("#,##0,,.00", false)]
        [DataRow("0.00%", false)]
        public void Read_NumberFormat_RejectsScalingAndPercentButAcceptsTextSection(string formatCode, bool shouldSucceed)
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                WorkbookStylesPart styles = workbook.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = new Stylesheet(
                    new NumberingFormats(new NumberingFormat { NumberFormatId = 164, FormatCode = formatCode }) { Count = 1 },
                    new CellFormats(new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true }) { Count = 1 });
                styles.Stylesheet.Save();
                FindCell(sheet, "C6").StyleIndex = 0;
            }))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                if (shouldSucceed)
                {
                    Assert.IsTrue(result.IsSuccess, result.Message);
                    Assert.AreEqual(8, result.Amounts.Count);
                    Assert.AreEqual(199.171234m, result.Amounts.Single(amount =>
                        amount.Field == Fields[0] && amount.RowKind == ExcelSummaryRowKind.Asset).RawAmountTenThousandYuan);
                }
                else
                {
                    AssertFailed(result);
                }
            }
        }

        [TestMethod]
        [DataRow("1.005", "1.01")]
        [DataRow("-1.005", "-1.01")]
        public void Read_MidpointAmount_RoundsAwayFromZeroWithoutLosingRawPrecision(string raw, string rounded)
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                FindCell(sheet, "C6").CellValue = new CellValue(raw);
                FindCell(sheet, "C7").CellValue = new CellValue(raw);
            }))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                Assert.IsTrue(result.IsSuccess, result.Message);
                foreach (ExcelSummaryAmount amount in result.Amounts.Where(value => value.Field == Fields[0]))
                {
                    Assert.AreEqual(decimal.Parse(raw, CultureInfo.InvariantCulture), amount.RawAmountTenThousandYuan);
                    Assert.AreEqual(decimal.Parse(rounded, CultureInfo.InvariantCulture), amount.RoundedAmountTenThousandYuan);
                }
            }
        }

        [TestMethod]
        public void Read_FormulaHasNoCachedResult_FailsWithoutSubstitutingZero()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
                FindCell(sheet, "C6").CellValue = null))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                AssertFailed(result, ExcelSummaryReadStatus.FormulaResultUnavailable);
                StringAssert.Contains(result.Message, "C6");
            }
        }

        [TestMethod]
        public void Read_WorkbookRequiresFullRecalculation_RejectsUnconfirmedFormulaCache()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
                workbook.Workbook.CalculationProperties = new CalculationProperties { FullCalculationOnLoad = true }))
            {
                AssertFailed(_reader.Read(file.FilePath), ExcelSummaryReadStatus.FormulaResultUnavailable);
            }
        }

        [TestMethod]
        [DataRow("error", "#VALUE!")]
        [DataRow("string", "199.17")]
        public void Read_FormulaCacheHasNonNumericType_FailsWithoutInterpretingTextAsMoney(string type, string value)
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                Cell cell = FindCell(sheet, "C6");
                cell.DataType = type == "error" ? CellValues.Error : CellValues.String;
                cell.CellValue = new CellValue(value);
            }))
            {
                AssertFailed(_reader.Read(file.FilePath));
            }
        }

        [TestMethod]
        public void Read_NumericCellContainsUnparseableValue_FailsWithNoPartialAmounts()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                Cell cell = FindCell(sheet, "F7");
                cell.CellFormula = null;
                cell.CellValue = new CellValue("无法确认金额");
            }))
            {
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);

                AssertFailed(result, ExcelSummaryReadStatus.InvalidAmount);
                StringAssert.Contains(result.Message, "F7");
            }
        }

        [TestMethod]
        public void Read_RequiredWorksheetMissing_DoesNotFallBackToAnotherSheet()
        {
            using (SyntheticWorkbook file = CreateWorkbook(worksheetName: "评估明细表"))
            {
                AssertFailed(_reader.Read(file.FilePath), ExcelSummaryReadStatus.MissingWorksheet);
            }
        }

        [TestMethod]
        public void Read_RequiredHeaderMissing_FailsInsteadOfGuessingFromColumnPosition()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
                FindCell(sheet, "F5").InlineString = new InlineString(new Text("备注"))))
            {
                AssertFailed(_reader.Read(file.FilePath), ExcelSummaryReadStatus.MissingRequiredField);
            }
        }

        [TestMethod]
        public void Read_MultipleAssetRows_FailsWithoutGuessingAssetCorrespondence()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                SheetData data = sheet.Worksheet.GetFirstChild<SheetData>();
                Row total = data.Elements<Row>().Single(row => row.RowIndex.Value == 7);
                total.RowIndex = 8;
                foreach (Cell cell in total.Elements<Cell>())
                {
                    cell.CellReference = new string(cell.CellReference.Value.TakeWhile(char.IsLetter).ToArray()) + "8";
                }
                data.InsertBefore(BuildAmountRow(7, 3, false), total);
            }))
            {
                AssertFailed(_reader.Read(file.FilePath), ExcelSummaryReadStatus.UnsupportedAssetStructure);
            }
        }

        [TestMethod]
        public void Read_UnitMissing_FailsInsteadOfAssumingTenThousandYuan()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
                FindCell(sheet, "A3").InlineString = new InlineString(new Text("虚构测试表"))))
            {
                AssertFailed(_reader.Read(file.FilePath), ExcelSummaryReadStatus.UnitNotSupported);
            }
        }

        [TestMethod]
        public void Read_NumericOrdinalFormulaAssetCaptionAndInstitutionFooter_ReadsEightAmounts()
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
            {
                Cell ordinal = FindCell(sheet, "A6");
                ordinal.DataType = CellValues.Number;
                ordinal.InlineString = null;
                ordinal.CellValue = new CellValue("1");
                Cell caption = FindCell(sheet, "B6");
                caption.DataType = CellValues.String;
                caption.InlineString = null;
                caption.CellFormula = new CellFormula("'虚构明细表'!B2");
                caption.CellValue = new CellValue("虚构设备A");
                SharedStringTablePart strings = workbook.AddNewPart<SharedStringTablePart>();
                strings.SharedStringTable = new SharedStringTable(
                    new SharedStringItem(new Text("评估机构：虚构评估机构")));
                sheet.Worksheet.GetFirstChild<SheetData>().Append(new Row(new Cell
                {
                    CellReference = "E8", DataType = CellValues.SharedString, CellValue = new CellValue("0")
                }) { RowIndex = 8 });
                sheet.Worksheet.Append(new MergeCells(new MergeCell { Reference = "A7:B7" }));
            }))
            {
                byte[] originalHash = ComputeHash(file.FilePath);
                ExcelSummaryReadResult result = _reader.Read(file.FilePath);
                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(8, result.Amounts.Count);
                Assert.IsTrue(result.Amounts.All(amount => amount.CellAddress.EndsWith("6", StringComparison.Ordinal)
                    || amount.CellAddress.EndsWith("7", StringComparison.Ordinal)));
                CollectionAssert.AreEqual(originalHash, ComputeHash(file.FilePath));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Read_ExtraAmountAlongsideInstitutionFooter_StillFailsClosed(bool formula)
        {
            using (SyntheticWorkbook file = CreateWorkbook((workbook, sheet) =>
                sheet.Worksheet.GetFirstChild<SheetData>().Append(new Row(
                    new Cell
                    {
                        CellReference = "C8", DataType = CellValues.Number,
                        CellFormula = formula ? new CellFormula("1+1") : null,
                        CellValue = new CellValue("2")
                    },
                    TextCell("E8", "评估机构：虚构评估机构")) { RowIndex = 8 })))
            {
                AssertFailed(_reader.Read(file.FilePath), ExcelSummaryReadStatus.UnsupportedAssetStructure);
            }
        }

        private static void AssertFailed(ExcelSummaryReadResult result, ExcelSummaryReadStatus? expectedStatus = null)
        {
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(0, result.Amounts.Count, "失败时不得向后续比较提供部分或伪造金额。");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Message), "读取失败必须给出可解释原因。");
            if (expectedStatus.HasValue)
            {
                Assert.AreEqual(expectedStatus.Value, result.Status, result.Message);
            }
        }

        private static SyntheticWorkbook CreateWorkbook(
            Action<WorkbookPart, WorksheetPart> customize = null,
            int firstAmountColumn = 3,
            bool groupedHeaders = false,
            string worksheetName = SummarySheetName)
        {
            string path = Path.Combine(Path.GetTempPath(), "AssetAppraisalChecker-Synthetic-" + Guid.NewGuid().ToString("N") + ".xlsx");
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbook = document.AddWorkbookPart();
                workbook.Workbook = new Workbook(new Sheets());
                WorksheetPart summary = AddWorksheet(workbook, worksheetName, BuildWorksheet(firstAmountColumn, groupedHeaders));
                customize?.Invoke(workbook, summary);
                foreach (WorksheetPart worksheet in workbook.WorksheetParts)
                {
                    worksheet.Worksheet.Save();
                }
                workbook.Workbook.Save();
            }
            return new SyntheticWorkbook(path);
        }

        private static WorksheetPart AddWorksheet(WorkbookPart workbook, string name, Worksheet worksheet)
        {
            WorksheetPart part = workbook.AddNewPart<WorksheetPart>();
            part.Worksheet = worksheet;
            Sheets sheets = workbook.Workbook.GetFirstChild<Sheets>();
            sheets.Append(new Sheet
            {
                Name = name,
                Id = workbook.GetIdOfPart(part),
                SheetId = (uint)sheets.Elements<Sheet>().Count() + 1U
            });
            return part;
        }

        private static Worksheet BuildWorksheet(int firstAmountColumn, bool groupedHeaders)
        {
            var data = new SheetData(
                new Row(TextCell("A1", "资产评估汇总表")) { RowIndex = 1 },
                new Row(TextCell("A3", "金额单位：万元")) { RowIndex = 3 });
            if (groupedHeaders)
            {
                data.Append(new Row(
                    TextCell(ColumnName(firstAmountColumn) + "4", "评估值（不含税）"),
                    TextCell(ColumnName(firstAmountColumn + 2) + "4", "评估值（含税）")) { RowIndex = 4 });
            }
            var header = new Row(TextCell("A5", "序号"), TextCell("B5", "资产名称")) { RowIndex = 5 };
            for (int index = 0; index < Headers.Length; index++)
            {
                string text = groupedHeaders ? (index % 2 == 0 ? "评估原值" : "评估净值") : Headers[index];
                header.Append(TextCell(ColumnName(firstAmountColumn + index) + "5", text));
            }
            data.Append(header, BuildAmountRow(6, firstAmountColumn, false), BuildAmountRow(7, firstAmountColumn, true));
            var worksheet = new Worksheet(data);
            if (groupedHeaders)
            {
                worksheet.Append(new MergeCells(
                    new MergeCell { Reference = ColumnName(firstAmountColumn) + "4:" + ColumnName(firstAmountColumn + 1) + "4" },
                    new MergeCell { Reference = ColumnName(firstAmountColumn + 2) + "4:" + ColumnName(firstAmountColumn + 3) + "4" }));
            }
            return worksheet;
        }

        private static Row BuildAmountRow(uint rowIndex, int firstAmountColumn, bool isTotal)
        {
            var row = new Row { RowIndex = rowIndex };
            row.Append(TextCell("A" + rowIndex, isTotal ? "合计" : "1"), TextCell("B" + rowIndex, isTotal ? string.Empty : "虚构设备A"));
            for (int index = 0; index < RawAmounts.Length; index++)
            {
                string column = ColumnName(firstAmountColumn + index);
                string raw = RawAmounts[index].ToString(CultureInfo.InvariantCulture);
                row.Append(new Cell
                {
                    CellReference = column + rowIndex,
                    DataType = CellValues.Number,
                    CellFormula = new CellFormula(isTotal ? "SUM(" + column + "6:" + column + "6)" : raw),
                    CellValue = new CellValue(raw)
                });
            }
            return row;
        }

        private static Cell TextCell(string address, string text)
        {
            return new Cell
            {
                CellReference = address,
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(text))
            };
        }

        private static Cell FindCell(WorksheetPart worksheet, string address)
        {
            return worksheet.Worksheet.Descendants<Cell>().Single(cell => cell.CellReference.Value == address);
        }

        private static string ColumnName(int index)
        {
            string name = string.Empty;
            while (index > 0)
            {
                index--;
                name = (char)('A' + index % 26) + name;
                index /= 26;
            }
            return name;
        }

        private static byte[] ComputeHash(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return hash.ComputeHash(stream);
            }
        }

        private sealed class SyntheticWorkbook : IDisposable
        {
            public SyntheticWorkbook(string path)
            {
                FilePath = path;
            }

            public string FilePath { get; }

            public void Dispose()
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
        }
    }
}
