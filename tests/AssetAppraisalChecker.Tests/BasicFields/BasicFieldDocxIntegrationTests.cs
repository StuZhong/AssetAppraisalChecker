using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class BasicFieldDocxIntegrationTests
    {
        [TestMethod]
        public void FullPipeline_ConsistentSyntheticDocx_ExtractsAllFieldsWithoutIssueOrFileChange()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("XX测试资产评估报告（完全虚构）"),
                ParagraphWithText("项目名称：XX测试公司拟资产处置资产评估项目"),
                ParagraphWithText("报告编号：云测试评报字〔2026〕第001号"),
                ParagraphWithText("评估基准日：2026年6月30日"),
                new Table(
                    Row("项目名称", "XX测试公司拟资产处置资产评估项目"),
                    Row("报告编号", "云测试评报字（2026）第001号"),
                    Row("评估基准日", "2026/06/30"))))
            {
                string before = ComputeSha256(file.FilePath);

                IReadOnlyList<BasicFieldOccurrence> occurrences = ReadOccurrences(file);
                BasicFieldConsistencyCheckResult result =
                    new BasicFieldConsistencyEngine().Check(occurrences);

                string after = ComputeSha256(file.FilePath);
                Assert.AreEqual(6, occurrences.Count);
                Assert.AreEqual(3, occurrences.Select(item => item.FieldType).Distinct().Count());
                Assert.AreEqual(3, result.ComparisonGroups.Count);
                Assert.AreEqual(0, result.Issues.Count);
                Assert.AreEqual(before, after);
            }
        }

        [TestMethod]
        public void FullPipeline_ConflictingSyntheticDocx_ReturnsThreeSeparateSeriousIssues()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("项目名称：XX测试公司拟资产处置项目"),
                ParagraphWithText("报告编号：云测试评报字〔2026〕第001号"),
                ParagraphWithText("评估基准日：2026年6月30日"),
                new Table(
                    Row("项目名称", "XX测试公司拟股权转让项目"),
                    Row("报告编号", "云测试评报字〔2026〕第002号"),
                    Row("评估基准日", "2026年6月29日"))))
            {
                IReadOnlyList<BasicFieldOccurrence> occurrences = ReadOccurrences(file);

                BasicFieldConsistencyCheckResult result =
                    new BasicFieldConsistencyEngine().Check(occurrences);

                Assert.AreEqual(6, occurrences.Count);
                Assert.AreEqual(3, result.Issues.Count);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        BasicFieldType.ProjectName,
                        BasicFieldType.ReportNumber,
                        BasicFieldType.ValuationDate
                    },
                    result.Issues.Select(item => item.FieldType).ToArray());
                Assert.IsTrue(result.Issues.All(item => item.IssueType == IssueType.BasicFieldConflict));
                Assert.IsTrue(result.Issues.All(item => item.Severity == IssueSeverity.Serious));
            }
        }

        [TestMethod]
        public void FullPipeline_TableLabelValueRows_CreateDistinctOccurrencesAtValueCells()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(
                    Row("评估基准日", "2026年6月30日"),
                    Row("报告编号", "云测试评报字〔2026〕第001号"))))
            {
                IReadOnlyList<BasicFieldOccurrence> occurrences = ReadOccurrences(file);

                Assert.AreEqual(2, occurrences.Count);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        BasicFieldType.ValuationDate,
                        BasicFieldType.ReportNumber
                    },
                    occurrences.Select(item => item.FieldType).ToArray());
                Assert.IsTrue(occurrences.All(item => item.Location.CellIndex == 1));
                Assert.IsTrue(occurrences.All(item => item.RawText == item.RawValue));
                Assert.IsTrue(occurrences.All(item => item.Evidence.Any(text => text.Contains("右侧相邻单元格"))));
            }
        }

        [TestMethod]
        public void FullPipeline_InvalidDateInSyntheticDocx_ReturnsParseReminder()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("评估基准日：待定")))
            {
                BasicFieldConsistencyCheckResult result = new BasicFieldConsistencyEngine().Check(
                    ReadOccurrences(file));

                Assert.AreEqual(1, result.Issues.Count);
                Assert.AreEqual(IssueType.BasicFieldParseFailed, result.Issues[0].IssueType);
                Assert.AreEqual(IssueSeverity.Reminder, result.Issues[0].Severity);
                StringAssert.Contains(result.Issues[0].Reason, "待定");
            }
        }

        [TestMethod]
        public void FullPipeline_MultiColumnLabelValueRow_ExtractsEachAdjacentPair()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(Row(
                    "评估基准日",
                    "2026年6月30日",
                    "报告编号",
                    "云测试评报字〔2026〕第001号"))))
            {
                IReadOnlyList<BasicFieldOccurrence> occurrences = ReadOccurrences(file);

                Assert.AreEqual(2, occurrences.Count);
                Assert.AreEqual("2026-06-30", occurrences.Single(
                    item => item.FieldType == BasicFieldType.ValuationDate).NormalizedValue.CanonicalValue);
                Assert.AreEqual("云测试评报字〔2026〕第001号", occurrences.Single(
                    item => item.FieldType == BasicFieldType.ReportNumber).NormalizedValue.CanonicalValue);
            }
        }

        private static IReadOnlyList<BasicFieldOccurrence> ReadOccurrences(SyntheticDocxFile file)
        {
            DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(document.IsSuccess, document.Message);
            return new BasicFieldExtractor().Extract(document);
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
    }
}
