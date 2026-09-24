using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Tests.Consistency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class ReportIssueDateConsistencyTests
    {
        private readonly BasicFieldConsistencyEngine _engine =
            new BasicFieldConsistencyEngine();

        [TestMethod]
        public void Check_EquivalentReportIssueDates_ReturnsNoConflict()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ReportIssueDate,
                    "2026年03月22日",
                    2),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ReportIssueDate,
                    "二〇二六年三月二十二日",
                    7)
            });

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(2, result.ComparisonGroups[0].Occurrences.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_DifferentReportIssueDates_ReturnsSeriousBasicFieldConflictWithEvidence()
        {
            BasicFieldOccurrence first = BasicFieldTestFactory.Occurrence(
                BasicFieldType.ReportIssueDate,
                "2026年03月22日",
                2);
            BasicFieldOccurrence second = BasicFieldTestFactory.Occurrence(
                BasicFieldType.ReportIssueDate,
                "2026年01月30日",
                7);

            BasicFieldIssue issue = _engine.Check(new[] { first, second }).Issues.Single();

            Assert.AreEqual(IssueType.BasicFieldConflict, issue.IssueType);
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            Assert.AreEqual(BasicFieldType.ReportIssueDate, issue.FieldType);
            Assert.AreEqual("评估报告日", issue.FieldName);
            Assert.AreEqual(2, issue.Occurrences.Count);
            Assert.AreSame(first, issue.Occurrences[0]);
            Assert.AreSame(second, issue.Occurrences[1]);
            CollectionAssert.AreEqual(
                new[] { 2, 7 },
                issue.Occurrences
                    .Select(item => item.Location.ParagraphIndex.Value)
                    .ToArray());
            Assert.IsTrue(issue.Occurrences.All(item =>
                item.Evidence.Contains("自动化测试构造的明确字段证据。")));
            StringAssert.Contains(issue.Reason, "评估报告日");
        }

        [TestMethod]
        public void Check_ExistingBasicFieldsAndMoney_PreserveConflictBehavior()
        {
            BasicFieldConsistencyCheckResult basicResult = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ProjectName,
                    "XX测试项目A",
                    0),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ProjectName,
                    "XX测试项目B",
                    1),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ReportNumber,
                    "云测试评报字〔2026〕第001号",
                    2),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ReportNumber,
                    "云测试评报字〔2026〕第002号",
                    3),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ValuationDate,
                    "2026年03月21日",
                    4),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ValuationDate,
                    "2026年03月22日",
                    5),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ReportIssueDate,
                    "2026年03月23日",
                    6),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ReportIssueDate,
                    "2026年03月24日",
                    7)
            });

            Assert.AreEqual(4, basicResult.ComparisonGroups.Count);
            Assert.AreEqual(4, basicResult.Issues.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    BasicFieldType.ProjectName,
                    BasicFieldType.ReportNumber,
                    BasicFieldType.ValuationDate,
                    BasicFieldType.ReportIssueDate
                },
                basicResult.Issues.Select(item => item.FieldType).ToArray());
            Assert.IsTrue(basicResult.Issues.All(item =>
                item.IssueType == IssueType.BasicFieldConflict &&
                item.Severity == IssueSeverity.Serious));

            ConsistencyCheckResult moneyResult = new ConsistencyEngine().Check(new[]
            {
                ConsistencyTestOccurrenceFactory.Create(
                    1700000m,
                    rawText: "170万元",
                    order: 0),
                ConsistencyTestOccurrenceFactory.Create(
                    1600000m,
                    rawText: "160万元",
                    order: 1)
            });

            Assert.AreEqual(1, moneyResult.Issues.Count);
            Assert.AreEqual(IssueType.MoneyConflict, moneyResult.Issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Serious, moneyResult.Issues[0].Severity);
            Assert.AreEqual(100000m, moneyResult.Issues[0].Difference);
        }
    }
}
