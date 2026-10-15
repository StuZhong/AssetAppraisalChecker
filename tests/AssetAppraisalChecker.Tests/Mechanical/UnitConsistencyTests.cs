using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Mechanical.Units;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class UnitConsistencyTests
    {
        [TestMethod]
        [DataRow("台", UnitFamily.Count)]
        [DataRow("kg", UnitFamily.Mass)]
        [DataRow("公里", UnitFamily.Length)]
        [DataRow("㎡", UnitFamily.Area)]
        [DataRow("立方米", UnitFamily.Volume)]
        [DataRow("小时", UnitFamily.Time)]
        [DataRow("人民币万元", UnitFamily.Currency)]
        [DataRow("％", UnitFamily.Percentage)]
        public void Parser_SupportsFrozenUnitFamilies(string text, UnitFamily expected)
        {
            Assert.IsTrue(new UnitParser().TryParse(text, out UnitExpression expression));
            Assert.AreEqual(expected, expression.Family);
            Assert.AreEqual(text, expression.OriginalText);
        }

        [TestMethod]
        public void Normalizer_RetainsCurrencyScale()
        {
            var parser = new UnitParser();
            Assert.IsTrue(parser.TryParse("元", out UnitExpression yuan));
            Assert.IsTrue(parser.TryParse("人民币元", out UnitExpression cnyYuan));
            Assert.IsTrue(parser.TryParse("万元", out UnitExpression tenThousand));

            Assert.AreEqual(UnitFamily.Currency, yuan.Family);
            Assert.AreEqual(yuan.CanonicalUnit, cnyYuan.CanonicalUnit);
            Assert.AreEqual(1m, yuan.Scale);
            Assert.AreEqual(10000m, tenThousand.Scale);
        }

        [TestMethod]
        [DataRow("元/台", UnitFamily.Count)]
        [DataRow("元／个", UnitFamily.Count)]
        [DataRow("元每件", UnitFamily.Count)]
        [DataRow("元/kg", UnitFamily.Mass)]
        [DataRow("元/吨", UnitFamily.Mass)]
        [DataRow("元/㎡", UnitFamily.Area)]
        [DataRow("万元/m²", UnitFamily.Area)]
        [DataRow("人民币元/平方米", UnitFamily.Area)]
        [DataRow("元/平米", UnitFamily.Area)]
        [DataRow("元/m³", UnitFamily.Volume)]
        [DataRow("元/月", UnitFamily.Time)]
        [DataRow("元/年", UnitFamily.Time)]
        public void Parser_SupportsCompoundUnitPrice(string text, UnitFamily denominator)
        {
            Assert.IsTrue(new UnitParser().TryParse(text, out UnitExpression expression));
            Assert.IsTrue(expression.IsCompound);
            Assert.AreEqual(UnitFamily.Currency, expression.NumeratorFamily);
            Assert.AreEqual(denominator, expression.DenominatorFamily);
            Assert.IsFalse(string.IsNullOrWhiteSpace(expression.CanonicalExpression));
        }

        [TestMethod]
        public void Parser_NormalizesMassAndAreaAliases()
        {
            var parser = new UnitParser();
            string[] mass = { "kg", "公斤", "千克" };
            string[] area = { "㎡", "m²", "平方米" };

            CollectionAssert.AreEqual(
                new[] { "Kilogram", "Kilogram", "Kilogram" },
                mass.Select(ParseCanonical).ToArray());
            CollectionAssert.AreEqual(
                new[] { "SquareMeter", "SquareMeter", "SquareMeter" },
                area.Select(ParseCanonical).ToArray());

            string ParseCanonical(string value)
            {
                parser.TryParse(value, out UnitExpression expression);
                return expression.CanonicalUnit;
            }
        }

        [TestMethod]
        public void Parser_DoesNotTreatLettersInsideEnglishWordsAsUnits()
        {
            IReadOnlyList<UnitExpression> expressions = new UnitParser().FindExpressions(
                "The market target remains stable and the model is offline.");

            Assert.AreEqual(0, expressions.Count);
        }

        [TestMethod]
        [DataRow("10 台", "3000 元/台")]
        [DataRow("100 ㎡", "8000 元/㎡")]
        [DataRow("5 t", "1200 元/t")]
        [DataRow("20 m³", "100 元/m³")]
        [DataRow("5 公斤", "1200 元/kg")]
        [DataRow("100 平方米", "8000 元/m²")]
        public void Checker_CompatibleQuantityAndPrice_DoNotReport(string quantity, string price)
        {
            Assert.AreEqual(0, Check(TableContext(new[] { quantity, price })).Count);
        }

        [TestMethod]
        [DataRow("1 kg", "30 元/台", UnitFamily.Mass, UnitFamily.Count)]
        [DataRow("100 ㎡", "5000 元/台", UnitFamily.Area, UnitFamily.Count)]
        [DataRow("10 台", "200 元/kg", UnitFamily.Count, UnitFamily.Mass)]
        [DataRow("20 m³", "100 元/㎡", UnitFamily.Volume, UnitFamily.Area)]
        public void Checker_ClearDimensionMismatch_ReportsOneNormalIssue(
            string quantity,
            string price,
            UnitFamily ignoredQuantity,
            UnitFamily ignoredPrice)
        {
            MechanicalIssue issue = Check(TableContext(new[] { quantity, price })).Single();

            Assert.AreEqual(MechanicalIssueKind.UnitConsistency, issue.Kind);
            Assert.AreEqual(MechanicalIssueSeverity.Normal, issue.Severity);
            Assert.AreEqual("数量单位与单价分母不一致", issue.Summary);
            Assert.AreEqual(2, issue.Occurrences.Count);
            Assert.IsTrue(issue.Occurrences.All(item => item.Evidence.Count >= 3));
            StringAssert.Contains(issue.SuggestedAction, "人工核对");
            Assert.IsFalse(issue.SuggestedAction.Contains("应该改成"));
        }

        [TestMethod]
        public void Checker_SeparateUnitColumn_UsesHeaderBoundRelation()
        {
            MechanicalCheckContext context = TableContext(
                new[] { "名称", "数量", "单位", "单价", "金额" },
                new[] { "合成设备", "1.00", "kg", "30.00 元/台", "30.00元" });

            MechanicalIssue issue = Check(context).Single();
            Assert.AreEqual(0, issue.Occurrences[0].Location.TableIndex);
            Assert.AreEqual(1, issue.Occurrences[0].Location.RowIndex);
            Assert.AreEqual(2, issue.Occurrences[0].Location.CellIndex);
        }

        [TestMethod]
        public void Checker_UnknownQuantityUnit_DoesNotReport()
        {
            MechanicalCheckContext context = TableContext(
                new[] { "数量", "单位", "单价" },
                new[] { "1", "箱", "30 元/台" });

            Assert.AreEqual(0, Check(context).Count);
        }

        [TestMethod]
        public void Checker_UnknownPriceDenominator_DoesNotReport()
        {
            Assert.AreEqual(0, Check(TableContext(new[] { "10 台", "30 元/箱" })).Count);
        }

        [TestMethod]
        public void Checker_UnrelatedParagraphs_AreNotCombined()
        {
            MechanicalCheckContext context = ParagraphContext(
                "本项目设备数量为 1 kg。",
                "另一章节讨论单价为 30 元/台。");

            Assert.AreEqual(0, Check(context).Count);
        }

        [TestMethod]
        public void Checker_ExplicitSentenceRelation_IsSupported()
        {
            MechanicalCheckContext context = ParagraphContext(
                "设备数量为 1 kg，单价为 30 元/台。");

            Assert.AreEqual(1, Check(context).Count);
        }

        [TestMethod]
        public void Checker_MixedRealEstateAndEquipmentRow_IsConservativelySkipped()
        {
            MechanicalCheckContext context = TableContext(new[]
            {
                "建筑面积 100 ㎡",
                "房屋单价 8000 元/㎡",
                "设备数量 3 台",
                "设备单价 5000 元/台",
                "土地面积 2 亩",
                "成新率 80%"
            });

            Assert.AreEqual(0, Check(context).Count);
        }

        [TestMethod]
        public void Checker_DocumentAAndB_AreIndependentAndPreserveLabels()
        {
            MechanicalIssue issueA = Check(TableContext("文档 A", new[] { "1 kg", "30 元/台" })).Single();
            MechanicalIssue issueB = Check(TableContext("文档 B", new[] { "100 ㎡", "5000 元/台" })).Single();

            Assert.IsTrue(issueA.Occurrences.All(item => item.SourceDocument == "文档 A"));
            Assert.IsTrue(issueB.Occurrences.All(item => item.SourceDocument == "文档 B"));
        }

        [TestMethod]
        public void DesktopMapper_UsesReadableUnitIssueType()
        {
            MechanicalIssue issue = Check(TableContext(new[] { "1 kg", "30 元/台" })).Single();
            ReportCheckIssue mapped = new MechanicalIssueMapper().Map(new[] { issue }, 0).Single();

            Assert.AreEqual("单位问题", mapped.IssueTypeDisplayOverride);
            Assert.AreEqual("文档 A", mapped.Occurrences[0].DocumentLabel);
        }

        [TestMethod]
        public void SyntheticDocxSmoke_IsReadOnlyAndFindsUnitIssue()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(
                    Row("名称", "数量", "单位", "单价", "金额"),
                    Row("合成设备", "1.00", "kg", "30.00 元/台", "30.00元"))))
            {
                string before = Hash(file.FilePath);
                DocumentReadResult read = new AssetAppraisalChecker.Document.DocxDocumentReader()
                    .Read(file.FilePath);
                var context = new MechanicalCheckContext(file.FilePath, read, "文档 A");

                Assert.AreEqual(1, Check(context).Count);
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public async Task DesktopService_SingleDocument_ShowsUnitIssue()
        {
            using (SyntheticDocxFile file = CreateUnitDocument("kg", "30.00 元/台"))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(file.FilePath, null, null));
                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.IssueTypeDisplayOverride == "单位问题");

                Assert.AreEqual(IssueSeverity.Normal, issue.Severity);
                Assert.IsTrue(issue.Occurrences.All(item => item.DocumentLabel == "文档 A"));
            }
        }

        [TestMethod]
        public async Task DesktopService_DualDocument_ChecksUnitsIndependently()
        {
            using (SyntheticDocxFile documentA = CreateUnitDocument("kg", "30.00 元/台"))
            using (SyntheticDocxFile documentB = CreateUnitDocument("㎡", "5000.00 元/台"))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null));
                ReportCheckIssue[] issues = result.Issues
                    .Where(item => item.IssueTypeDisplayOverride == "单位问题")
                    .ToArray();

                Assert.AreEqual(2, issues.Length);
                CollectionAssert.AreEquivalent(
                    new[] { "文档 A", "文档 B" },
                    issues.SelectMany(item => item.Occurrences)
                        .Select(item => item.DocumentLabel)
                        .Distinct()
                        .ToArray());
            }
        }

        private static IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            return new UnitConsistencyChecker().Check(context);
        }

        private static MechanicalCheckContext TableContext(params string[][] rows)
        {
            return TableContext("文档 A", rows);
        }

        private static MechanicalCheckContext TableContext(string source, params string[][] rows)
        {
            var elements = new List<DocumentElement>();
            int order = 0;
            for (int row = 0; row < rows.Length; row++)
            {
                for (int cell = 0; cell < rows[row].Length; cell++)
                {
                    elements.Add(new DocumentElement(
                        DocumentElementType.TableCell,
                        rows[row][cell],
                        new DocumentLocation(
                            DocumentPart.MainBody,
                            null,
                            0,
                            row,
                            cell,
                            new string[0]),
                        order++,
                        string.Empty,
                        null));
                }
            }

            return new MechanicalCheckContext(
                "synthetic.docx",
                DocumentReadResult.Succeeded(elements, 0, 1),
                source);
        }

        private static MechanicalCheckContext ParagraphContext(params string[] values)
        {
            List<DocumentElement> elements = values.Select((value, index) => new DocumentElement(
                DocumentElementType.Paragraph,
                value,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    index,
                    null,
                    null,
                    null,
                    new string[0]),
                index,
                string.Empty,
                null)).ToList();
            return new MechanicalCheckContext(
                "synthetic.docx",
                DocumentReadResult.Succeeded(elements, values.Length, 0),
                "文档 A");
        }

        private static TableRow Row(params string[] values)
        {
            return new TableRow(values.Select(value =>
                new TableCell(new Paragraph(new Run(new Text(value))))));
        }

        private static SyntheticDocxFile CreateUnitDocument(string unit, string price)
        {
            return SyntheticDocxFile.Create(
                new Table(
                    Row("名称", "数量", "单位", "单价", "金额"),
                    Row("合成资产", "1.00", unit, price, "30.00元")));
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }
}
