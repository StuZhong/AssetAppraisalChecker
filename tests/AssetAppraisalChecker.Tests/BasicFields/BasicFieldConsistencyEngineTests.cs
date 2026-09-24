using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class BasicFieldConsistencyEngineTests
    {
        private readonly BasicFieldConsistencyEngine _engine = new BasicFieldConsistencyEngine();

        [TestMethod]
        public void Check_EmptyCollection_ReturnsNoGroupOrIssue()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new BasicFieldOccurrence[0]);

            Assert.AreEqual(0, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_NullCollection_Throws()
        {
            Assert.ThrowsExactly<System.ArgumentNullException>(() => _engine.Check(null));
        }

        [TestMethod]
        public void Check_SingleOccurrence_ReturnsGroupButNoIssue()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ProjectName, "XX测试项目", 0)
            });

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_ConsistentProjectName_ReturnsNoIssue()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ProjectName, "XX测试资产评估项目", 0),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ProjectName, " XX测试资产评估项目 ", 1)
            });

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_DifferentProjectNames_ReturnsSeriousConflict()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ProjectName, "XX测试公司拟资产处置项目", 0),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ProjectName, "XX测试公司拟股权转让项目", 1)
            });

            BasicFieldIssue issue = result.Issues.Single();
            Assert.AreEqual(IssueType.BasicFieldConflict, issue.IssueType);
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            Assert.AreEqual(BasicFieldType.ProjectName, issue.FieldType);
            StringAssert.Contains(issue.Reason, "同一字段存在多个不同标准值");
        }

        [TestMethod]
        public void Check_ReportNumberBracketVariants_ReturnNoIssue()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ReportNumber, "云测试评报字〔2026〕第001号", 0),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ReportNumber, "云测试评报字（2026）第001号", 1)
            });

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_ReportNumber001Versus002_ReturnsSeriousConflict()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ReportNumber, "云测试评报字〔2026〕第001号", 0),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ReportNumber, "云测试评报字〔2026〕第002号", 1)
            });

            BasicFieldIssue issue = result.Issues.Single();
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            StringAssert.Contains(issue.Reason, "第001号");
            StringAssert.Contains(issue.Reason, "第002号");
        }

        [TestMethod]
        public void Check_FourValuationDateFormats_ReturnNoIssue()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "2026年7月31日", 0),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "2026-07-31", 1),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "2026/7/31", 2),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "2026.07.31", 3)
            });

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_DifferentValuationDates_ReturnsSeriousConflictWithoutChoosingWinner()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "2026年6月30日", 0),
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "2026年6月29日", 1)
            });

            BasicFieldIssue issue = result.Issues.Single();
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            Assert.AreEqual(0, issue.SuspectedOutlierOccurrences.Count);
            StringAssert.Contains(issue.SuggestedAction, "人工核对");
            Assert.IsFalse(issue.Reason.Contains("正确"));
        }

        [TestMethod]
        public void Check_StrictMajority_MarksOnlyMinorityAsSuspectedOutlier()
        {
            BasicFieldOccurrence first = BasicFieldTestFactory.Occurrence(
                BasicFieldType.ReportNumber,
                "云测试评报字〔2026〕第001号",
                0);
            BasicFieldOccurrence second = BasicFieldTestFactory.Occurrence(
                BasicFieldType.ReportNumber,
                "云测试评报字〔2026〕第001号",
                1);
            BasicFieldOccurrence third = BasicFieldTestFactory.Occurrence(
                BasicFieldType.ReportNumber,
                "云测试评报字〔2026〕第002号",
                2);

            BasicFieldIssue issue = _engine.Check(new[] { first, second, third }).Issues.Single();

            Assert.AreEqual(1, issue.SuspectedOutlierOccurrences.Count);
            Assert.AreSame(third, issue.SuspectedOutlierOccurrences.Single());
            StringAssert.Contains(issue.Reason, "2处标准化为");
            StringAssert.Contains(issue.Reason, "1处标准化为");
        }

        [TestMethod]
        public void Check_LowConfidenceConflict_IsAtMostReminder()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ProjectName,
                    "XX测试项目A",
                    0,
                    ClassificationConfidence.Low),
                BasicFieldTestFactory.Occurrence(
                    BasicFieldType.ProjectName,
                    "XX测试项目B",
                    1,
                    ClassificationConfidence.High)
            });

            Assert.AreEqual(IssueSeverity.Reminder, result.Issues.Single().Severity);
        }

        [TestMethod]
        public void Check_UnparseableLabeledDate_ReturnsReminderNotConflict()
        {
            BasicFieldConsistencyCheckResult result = _engine.Check(new[]
            {
                BasicFieldTestFactory.Occurrence(BasicFieldType.ValuationDate, "待定", 0)
            });

            BasicFieldIssue issue = result.Issues.Single();
            Assert.AreEqual(IssueType.BasicFieldParseFailed, issue.IssueType);
            Assert.AreEqual(IssueSeverity.Reminder, issue.Severity);
            Assert.AreEqual(1, result.UnparsedOccurrenceCount);
            StringAssert.Contains(issue.Reason, "无法解析");
        }
    }
}
