using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Diagnostics;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Diagnostics
{
    [TestClass]
    public sealed class DiagnosticRunnerTests
    {
        private const string ProjectSecret = "虚构保密项目甲";
        private const string AlternateProjectSecret = "虚构保密项目乙";
        private const string ReportNumberSecret = "云虚构评报字〔2099〕第987号";
        private const string MoneySecret = "9876543.21";
        [TestMethod]
        public void Run_ValidDocx_ReturnsSuccess()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                DiagnosticExecution execution = Run(file.FilePath);

                Assert.AreEqual(0, execution.ExitCode);
                Assert.AreEqual(string.Empty, execution.Error);
            }
        }

        [TestMethod]
        public void Run_ValidDocx_ReportsOfficialSuccessStatus()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                StringAssert.Contains(Run(file.FilePath).Output, "DocumentReadStatus: Success");
            }
        }

        [TestMethod]
        public void Run_MissingFile_ReturnsNonZeroAndOfficialStatus()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "AssetAppraisalChecker-Missing-" + Guid.NewGuid().ToString("N") + ".docx");

            DiagnosticExecution execution = Run(path);

            Assert.AreNotEqual(0, execution.ExitCode);
            StringAssert.Contains(execution.Output, "DocumentReadStatus: FileNotFound");
        }

        [TestMethod]
        public void Run_NonDocx_ReturnsNonZeroAndUnsupportedStatus()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(new byte[] { 1 }, ".txt"))
            {
                DiagnosticExecution execution = Run(file.FilePath);

                Assert.AreNotEqual(0, execution.ExitCode);
                StringAssert.Contains(execution.Output, "DocumentReadStatus: UnsupportedFileType");
            }
        }

        [TestMethod]
        public void Run_NoArguments_ReturnsUsageError()
        {
            DiagnosticExecution execution = RunArguments(new string[0]);

            Assert.AreNotEqual(0, execution.ExitCode);
            StringAssert.Contains(execution.Error, "AssetAppraisalChecker.Diagnostics.exe");
        }

        [TestMethod]
        public void Run_MultipleArguments_ReturnsUsageErrorWithoutReadingEitherFile()
        {
            using (SyntheticDocxFile first = CreateConsistentDocument())
            using (SyntheticDocxFile second = CreateConsistentDocument())
            {
                DiagnosticExecution execution = RunArguments(
                    new[] { first.FilePath, second.FilePath });

                Assert.AreNotEqual(0, execution.ExitCode);
                Assert.AreEqual(string.Empty, execution.Output);
            }
        }

        [TestMethod]
        public void Run_InvalidDocx_ReturnsNonZeroAndInvalidDocxStatus()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(
                new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x53, 0x41, 0x46, 0x45 }))
            {
                DiagnosticExecution execution = Run(file.FilePath);

                Assert.AreNotEqual(0, execution.ExitCode);
                StringAssert.Contains(execution.Output, "DocumentReadStatus: InvalidDocx");
            }
        }

        [TestMethod]
        public void Run_ValidDocx_DoesNotChangeSha256()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string before = ComputeSha256(file.FilePath);

                DiagnosticExecution execution = Run(file.FilePath);

                Assert.AreEqual(before, ComputeSha256(file.FilePath));
                StringAssert.Contains(execution.Output, "SourceSha256Unchanged: True");
            }
        }

        [TestMethod]
        public void Run_ValidDocx_DoesNotChangeLengthOrLastWriteTime()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                DiagnosticExecution execution = Run(file.FilePath);

                StringAssert.Contains(execution.Output, "SourceLengthUnchanged: True");
                StringAssert.Contains(execution.Output, "SourceLastWriteTimeUnchanged: True");
            }
        }

        [TestMethod]
        public void Run_ValidDocx_CreatesNoFilesBesideSource()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "AssetAppraisalChecker-Diagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "synthetic.docx");
            try
            {
                using (SyntheticDocxFile source = CreateConsistentDocument())
                {
                    File.Copy(source.FilePath, path);
                }

                string[] before = Directory.GetFiles(directory);
                DiagnosticExecution execution = Run(path);
                string[] after = Directory.GetFiles(directory);

                Assert.AreEqual(0, execution.ExitCode);
                CollectionAssert.AreEqual(before, after);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Run_ValidDocx_CreatesNoLogJsonCsvOrTextArtifacts()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "AssetAppraisalChecker-NoArtifacts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "synthetic.docx");
            try
            {
                using (SyntheticDocxFile source = CreateConsistentDocument())
                {
                    File.Copy(source.FilePath, path);
                }

                Run(path);

                Assert.AreEqual(0, Directory.GetFiles(directory, "*.log").Length);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.json").Length);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.csv").Length);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.txt").Length);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void Run_OutputDoesNotContainProjectName()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                AssertDoesNotContain(Combined(Run(file.FilePath)), ProjectSecret);
            }
        }

        [TestMethod]
        public void Run_OutputDoesNotContainReportNumber()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                AssertDoesNotContain(Combined(Run(file.FilePath)), ReportNumberSecret);
            }
        }

        [TestMethod]
        public void Run_OutputDoesNotContainMoneyValue()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                AssertDoesNotContain(Combined(Run(file.FilePath)), MoneySecret);
            }
        }

        [TestMethod]
        public void Run_OutputDoesNotContainSourcePathOrFileName()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string result = Combined(Run(file.FilePath));

                AssertDoesNotContain(result, file.FilePath);
                AssertDoesNotContain(result, Path.GetFileName(file.FilePath));
            }
        }

        [TestMethod]
        public void Run_ReportsExactlyThreeV1BasicFieldTypes()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string output = Run(file.FilePath).Output;
                string[] fieldTypes =
                {
                    "ProjectName", "ReportNumber", "ValuationDate"
                };

                foreach (string fieldType in fieldTypes)
                {
                    StringAssert.Contains(output, "  " + fieldType + ": OccurrenceCount=");
                }

                foreach (string retiredType in new[]
                {
                    "ReportIssueDate", "Client", "PropertyHolder", "ValuationPurpose", "ValuationMethod",
                    "ValueType", "VatRate", "AssetCount"
                })
                {
                    AssertDoesNotContain(output, "  " + retiredType + ":");
                }
            }
        }

        [TestMethod]
        public void Run_ReportsProjectNameCountsWithoutCanonicalValue()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string output = Run(file.FilePath).Output;

                StringAssert.Contains(
                    output,
                    "ProjectName: OccurrenceCount=2; CanonicalDistinctCount=1; ConflictCount=0");
                AssertDoesNotContain(output, ProjectSecret);
            }
        }

        [TestMethod]
        public void Run_ReportsReportNumberCountsWithoutCanonicalValue()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string output = Run(file.FilePath).Output;

                StringAssert.Contains(
                    output,
                    "ReportNumber: OccurrenceCount=2; CanonicalDistinctCount=1; ConflictCount=0");
                AssertDoesNotContain(output, ReportNumberSecret);
            }
        }

        [TestMethod]
        public void Run_ReportsMoneyCountsAndSeverityCounts()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string output = Run(file.FilePath).Output;

                StringAssert.Contains(output, "Money:");
                StringAssert.Contains(output, "OccurrenceCount:");
                StringAssert.Contains(output, "ComparisonGroupCount:");
                StringAssert.Contains(output, "SkippedOccurrenceCount:");
                StringAssert.Contains(output, "SeriousIssueCount:");
                StringAssert.Contains(output, "NormalIssueCount:");
                StringAssert.Contains(output, "ReminderIssueCount:");
            }
        }

        [TestMethod]
        public void Run_ReportsUnifiedIssueSeverityCounts()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string output = Run(file.FilePath).Output;

                StringAssert.Contains(output, "Issues:");
                StringAssert.Contains(output, "  Serious:");
                StringAssert.Contains(output, "  Normal:");
                StringAssert.Contains(output, "  Reminder:");
                StringAssert.Contains(output, "  Total:");
            }
        }

        [TestMethod]
        public void Run_DetectedIssueStillReturnsSuccessExitCode()
        {
            using (SyntheticDocxFile file = CreateProjectConflictDocument())
            {
                DiagnosticExecution execution = Run(file.FilePath);

                Assert.AreEqual(0, execution.ExitCode);
                StringAssert.Contains(execution.Output, "ConflictCount=1");
            }
        }

        [TestMethod]
        public void Run_IssueOutputContainsOnlySafeMetadataAndAnonymousLocation()
        {
            using (SyntheticDocxFile file = CreateProjectConflictDocument())
            {
                string output = Run(file.FilePath).Output;

                StringAssert.Contains(output, "Severity=Serious; IssueType=BasicFieldConflict; Field=ProjectName");
                StringAssert.Contains(output, "IssueLocation[");
                StringAssert.Contains(output, "Part=MainBody");
                AssertDoesNotContain(output, ProjectSecret);
                AssertDoesNotContain(output, AlternateProjectSecret);
            }
        }

        [TestMethod]
        public void Run_LocationsContainIndexesButNoHeadingTextOrNearbyContext()
        {
            using (SyntheticDocxFile file = CreateConsistentDocument())
            {
                string output = Run(file.FilePath).Output;

                StringAssert.Contains(output, "ParagraphIndex=");
                StringAssert.Contains(output, "TableIndex=");
                StringAssert.Contains(output, "RowIndex=");
                StringAssert.Contains(output, "CellIndex=");
                StringAssert.Contains(output, "HeadingDepth=");
                AssertDoesNotContain(output, "HeadingPath=");
                AssertDoesNotContain(output, "NearbyContext=");
            }
        }

        [TestMethod]
        public void Run_InvalidDocx_DoesNotEchoEmbeddedBytesAsText()
        {
            byte[] content = System.Text.Encoding.UTF8.GetBytes(
                "PK\u0003\u0004" + ProjectSecret + ReportNumberSecret);
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateRaw(content))
            {
                string result = Combined(Run(file.FilePath));

                AssertDoesNotContain(result, ProjectSecret);
                AssertDoesNotContain(result, ReportNumberSecret);
            }
        }

        private static SyntheticDocxFile CreateConsistentDocument()
        {
            return SyntheticDocxFile.Create(
                Paragraph("项目名称：" + ProjectSecret),
                Paragraph("报告编号：" + ReportNumberSecret),
                Paragraph("评估基准日：2099年6月30日"),
                Paragraph("本项目不含税评估净值为" + MoneySecret + "元。"),
                new Table(
                    Row("项目名称", ProjectSecret),
                    Row("报告编号", "云虚构评报字（2099）第987号"),
                    Row("评估基准日", "2099/06/30")));
        }

        private static SyntheticDocxFile CreateProjectConflictDocument()
        {
            return SyntheticDocxFile.Create(
                Paragraph("项目名称：" + ProjectSecret),
                new Table(Row("项目名称", AlternateProjectSecret)));
        }

        private static DiagnosticExecution Run(string path)
        {
            return RunArguments(new[] { path });
        }

        private static DiagnosticExecution RunArguments(string[] args)
        {
            using (var output = new StringWriter())
            using (var error = new StringWriter())
            {
                int exitCode = new DiagnosticRunner().Run(args, output, error);
                return new DiagnosticExecution(exitCode, output.ToString(), error.ToString());
            }
        }

        private static string Combined(DiagnosticExecution execution)
        {
            return execution.Output + execution.Error;
        }

        private static void AssertDoesNotContain(string actual, string forbidden)
        {
            Assert.IsFalse(
                (actual ?? string.Empty).Contains(forbidden),
                "诊断输出不应包含受保护内容。Forbidden marker length: " + forbidden.Length);
        }

        private static Paragraph Paragraph(string text)
        {
            return new Paragraph(new Run(new Text(text)));
        }

        private static TableRow Row(params string[] values)
        {
            return new TableRow(values.Select(value => new TableCell(Paragraph(value))));
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private sealed class DiagnosticExecution
        {
            public DiagnosticExecution(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; }

            public string Output { get; }

            public string Error { get; }
        }
    }
}
