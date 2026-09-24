using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class ReportIssueDateMatcherTests
    {
        [TestMethod]
        [DataRow("评估报告日")]
        [DataRow("评估报告日期")]
        [DataRow("报告日期")]
        public void ExplicitLabel_FollowedByValidDate_CreatesOccurrence(string label)
        {
            const string date = "2026年03月22日";
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText(label + "：" + date));

            BasicFieldOccurrence occurrence = occurrences.Single(
                item => item.FieldType == BasicFieldType.ReportIssueDate);
            Assert.AreEqual(date, occurrence.RawValue);
            Assert.AreEqual("2026-03-22", occurrence.NormalizedValue.CanonicalValue);
            Assert.AreEqual(date.Length, occurrence.TextLength);
            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("ExplicitLabel")));
        }

        [TestMethod]
        [DataRow("二○二五年十二月五日", "2025-12-05")]
        [DataRow("二〇二六年三月十八日", "2026-03-18")]
        public void ExplicitLabel_ChineseNumericDate_IsNormalized(
            string date,
            string expectedCanonical)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("评估报告日：" + date));

            BasicFieldOccurrence occurrence = occurrences.Single(
                item => item.FieldType == BasicFieldType.ReportIssueDate);
            Assert.IsTrue(occurrence.HasNormalizedValue);
            Assert.AreEqual(expectedCanonical, occurrence.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void CoverUniqueStandaloneDate_BeforeReliableBoundary_CreatesOccurrence()
        {
            const string date = "2026年03月22日";
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("虚构资产评估报告"),
                ParagraphWithText(date),
                PageSectionEnd("首页结束"),
                ParagraphWithText("次页正文"));

            BasicFieldOccurrence occurrence = occurrences.Single(
                item => item.FieldType == BasicFieldType.ReportIssueDate);
            Assert.AreEqual(date, occurrence.RawValue);
            Assert.AreEqual("2026-03-22", occurrence.NormalizedValue.CanonicalValue);
            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("CoverUniqueDate")));
        }

        [TestMethod]
        public void CoverMultipleDates_BeforeReliableBoundary_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("2026年03月22日"),
                ParagraphWithText("2026年03月23日"),
                PageSectionEnd("首页结束"),
                ParagraphWithText("次页正文"));

            Assert.AreEqual(
                0,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ReportIssueDate));
        }

        [TestMethod]
        public void CoverStandaloneDate_WithNarrativeSecondDate_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("2026年03月22日"),
                ParagraphWithText("其他事项记录于2026年03月23日。"),
                PageSectionEnd("首页结束"),
                ParagraphWithText("次页正文"));

            Assert.AreEqual(
                0,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ReportIssueDate));
        }

        [TestMethod]
        public void CoverValuationDateSemantic_IsNotReportIssueDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("评估基准日：2026年03月22日"),
                PageSectionEnd("首页结束"),
                ParagraphWithText("次页正文"));

            Assert.AreEqual(
                0,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ReportIssueDate));
            Assert.AreEqual(
                1,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void CoverValiditySemantic_IsNotReportIssueDate()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("有效期截至2026年03月22日"),
                PageSectionEnd("首页结束"),
                ParagraphWithText("次页正文"));

            Assert.AreEqual(
                0,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ReportIssueDate));
        }

        [TestMethod]
        public void CoverDate_WithoutReliableBoundary_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("2026年03月22日"),
                ParagraphWithText("正文"));

            Assert.AreEqual(
                0,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ReportIssueDate));
        }

        [TestMethod]
        public void ExplicitReportIssueDate_DoesNotChangeValuationDateExtraction()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText("评估报告日：2026年03月22日"),
                ParagraphWithText("评估基准日：2026年02月18日"));

            BasicFieldOccurrence reportIssueDate = occurrences.Single(
                item => item.FieldType == BasicFieldType.ReportIssueDate);
            BasicFieldOccurrence valuationDate = occurrences.Single(
                item => item.FieldType == BasicFieldType.ValuationDate);

            Assert.AreEqual("2026-03-22", reportIssueDate.NormalizedValue.CanonicalValue);
            Assert.AreEqual("2026-02-18", valuationDate.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void ExplicitLabel_WithSecondDateInSameClause_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                ParagraphWithText(
                    "报告日期：2026年03月22日，另有日期2026年03月23日"));

            Assert.AreEqual(
                0,
                occurrences.Count(item =>
                    item.FieldType == BasicFieldType.ReportIssueDate));
        }

        private static IReadOnlyList<BasicFieldOccurrence> Extract(
            params OpenXmlElement[] bodyElements)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(bodyElements))
            {
                DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
                Assert.IsTrue(document.IsSuccess, document.Message);
                return new BasicFieldExtractor().Extract(document);
            }
        }

        private static Paragraph ParagraphWithText(string text)
        {
            return new Paragraph(new Run(new Text(text)));
        }

        private static Paragraph PageSectionEnd(string text)
        {
            return new Paragraph(
                new ParagraphProperties(
                    new SectionProperties(
                        new SectionType { Val = SectionMarkValues.NextPage })),
                new Run(new Text(text)));
        }
    }
}
