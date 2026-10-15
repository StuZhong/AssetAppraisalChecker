using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.QualityRegression
{
    [TestClass]
    public sealed class ApprovedBaselineMutationTests
    {
        private static readonly string[] PositiveKinds = { "MoneyConflict", "BasicFieldConflict", "BasicFieldConflict",
            "BasicFieldConflict", "UnitConsistency", "DuplicateWord", "UnpairedChineseBracket", "UnpairedBookTitle", "Spell.ConfusableProfessionalTerm" };
        private static readonly string[] Mutations = { "评估价值为200万元", "评估基准日：2026年7月31日",
            "报告编号：合成评报字〔2026〕第002号", "项目名称：合成乙资产评估项目", "设备数量为1 kg，单价为30元/台。",
            "本次评估评估工作以合成资料为依据。", "本次评估涉及（合成设备。", "本次评估依据《合成技术说明。", "本次评估基准曰为合成日期。" };

        [TestMethod]
        [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)]
        [DataRow(5)] [DataRow(6)] [DataRow(7)] [DataRow(8)]
        public async Task ControlledMutation_ProductionChainDetectsAndLocatesSingleError(int index)
        {
            using (SyntheticDocxFile clean = Clean())
            {
                string cleanHash = Hash(clean.FilePath);
                string copy = Copy(clean.FilePath);
                try
                {
                    int injectedLocation;
                    if (index < 4)
                    {
                        using (var doc = WordprocessingDocument.Open(copy, true))
                        {
                            Paragraph paragraph = doc.MainDocumentPart.Document.Body.Elements<Paragraph>().ElementAt(index);
                            paragraph.RemoveAllChildren();
                            paragraph.Append(new Run(new Text(Mutations[index])));
                            doc.MainDocumentPart.Document.Save();
                        }
                        injectedLocation = index;
                    }
                    else injectedLocation = Append(copy, Mutations[index]);
                    string copyHash = Hash(copy);
                    var service = new ReportCheckService();
                    foreach (bool swapped in new[] { false, true })
                    {
                        ReportCheckResult result = await service.CheckAsync(new ReportCheckRequest(
                            swapped ? clean.FilePath : copy, swapped ? copy : clean.FilePath, null));
                        Assert.IsTrue(result.IsSuccess);
                        ReportCheckIssue hit = result.Issues.FirstOrDefault(item => IsTarget(item, index) &&
                            item.Occurrences.Any(o => o.DocumentLabel == (swapped ? "文档 B" : "文档 A") &&
                                o.Location.ParagraphIndex == injectedLocation));
                        Assert.IsNotNull(hit, "Mutation must be detected at its actual injected location.");
                        Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(hit)), "Hard evidence stays focused.");
                        Assert.IsFalse(string.IsNullOrWhiteSpace(hit.Reason));
                    }
                    ReportCheckResult single = await service.CheckAsync(copy);
                    Assert.IsTrue(single.IsSuccess);
                    if (index >= 4) Assert.IsTrue(single.Issues.Any(item => IsTarget(item, index)));
                    Assert.AreEqual(copyHash, Hash(copy));
                    Assert.AreEqual(cleanHash, Hash(clean.FilePath));
                }
                finally { File.Delete(copy); }
            }
        }

        [TestMethod]
        public async Task CleanSyntheticBaseline_HasNoFocusedFalsePositives()
        {
            using (SyntheticDocxFile a = Clean())
            using (SyntheticDocxFile b = Clean())
            {
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, Focused(result).Length);
            }
        }

        [TestMethod]
        [DataRow("本项目依据2018年版合成法规确定费率。", "2018年版", "300万元/km")]
        [DataRow("本项目依据2020年修订合成法规。", "2020年修订", "300万元/km")]
        [DataRow("法规名称清单仅用于说明。", "法规依据", "30元/台")]
        public async Task LegalVersionAndComplexTable_NoQuantityRelationFalsePositive(string prose, string version, string rate)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(prose),
                new Table(Row("法规依据", "费率依据"), Row(version, rate))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.AreEqual(0, result.Issues.Count(item => item.GovernanceKind == "UnitConsistency"));
            }
        }

        [TestMethod]
        public async Task UnboundSameRowAndPartialTableSymbols_StayDetailed()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(new Table(
                Row("说明", "计价参考"), Row("1 kg", "30元/台"), Row("《合成法规", "（备注"))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var hits = result.Issues.Where(item => new[] { "UnitConsistency", "UnpairedBookTitle", "UnpairedChineseBracket" }
                    .Contains(item.GovernanceKind)).ToArray();
                Assert.AreEqual(3, hits.Length);
                Assert.IsTrue(hits.All(item => IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(item))));
            }
        }

        [TestMethod]
        public async Task ExplanatoryLabelsAndVersionInQuantityColumn_DoNotProveRelation()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                P("数量另见附表，2018年版法规所附参数1 kg；费率依据中的参考单价30元/台。"),
                new Table(Row("数量", "单位", "单价"), Row("2018年版", "kg", "30元/台")),
                new Table(Row("数量调整依据", "单价说明"), Row("1 kg", "30元/台"))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, Focused(result).Length);
                Assert.AreEqual(1, result.Issues.Count(item => item.GovernanceKind == "UnitConsistency"));
            }
        }

        [TestMethod]
        [DataRow("2026年6月15日")]
        [DataRow("2018年版")]
        [DataRow("2020年修订")]
        public async Task CalendarValueCannotBecomeQuantityViaHeader(string date)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(new Table(Row("数量", "单价"), Row(date, "30元/kg"))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.AreEqual(0, result.Issues.Count(item => item.GovernanceKind == "UnitConsistency"));
            }
        }

        [TestMethod]
        public void DurationIsStillAUnit_NotGloballyDisabled()
        {
            var expressions = new AssetAppraisalChecker.Mechanical.Units.UnitParser().FindExpressions("服务数量为3年，单价为100元/年。");
            Assert.AreEqual(2, expressions.Count);
            Assert.AreEqual(AssetAppraisalChecker.Mechanical.Units.UnitFamily.Time, expressions[0].Family);
        }

        [TestMethod]
        public async Task SplitRunsHyperlinkAndParagraphPairs_NoFalseFocusedError()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("依据《合成")), new Hyperlink(new Run(new Text("法规》执行。")))),
                P("本次涉及（合成"), P("设备），按规范执行。"),
                P("法规清单：相关相关规定。"), P("本次依据《合成资产资产管理办法》执行。")))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.AreEqual(0, Focused(result).Length);
            }
        }

        [TestMethod]
        [DataRow("评估评估")]
        [DataRow("根据根据")]
        public async Task DirectControlledRepeatedTerm_RemainsFocused(string term)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P("本次" + term + "开展工作。")))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                ReportCheckIssue issue = result.Issues.Single(item => item.GovernanceKind == "DuplicateWord");
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(issue)));
                Assert.AreEqual(term, issue.Occurrences[0].RawText);
            }
        }

        [TestMethod]
        public async Task CitationCandidate_DoesNotHideLaterDirectRepeat()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P("依据《合成资产资产规则》，本次评估评估工作。")))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                ReportCheckIssue issue = result.Issues.Single(item => item.GovernanceKind == "DuplicateWord");
                Assert.AreEqual("评估评估", issue.Occurrences[0].RawText);
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(issue)));
            }
        }

        [TestMethod]
        public async Task FieldAndMergeBoundaries_DoNotClaimClosedTextEvidence()
        {
            var field = new SimpleField(new Run(new Text("《合成字段。"))) { Instruction = "DOCPROPERTY Synthetic" };
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(new Paragraph(field),
                new Table(new TableRow(new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("涉及（合成设备。"))))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                Assert.AreEqual(0, Focused(result).Length);
                Assert.AreEqual(2, result.Issues.Count(item => item.GovernanceKind.StartsWith("Unpaired", StringComparison.Ordinal)));
            }
        }

        // Explicit opt-in local audit: never store real paths/text or run private assets in ordinary CI.
        // Only anonymous counts and case outcomes are printed. The caller supplies authorized local paths.
        public static void RunApprovedReports(string pathA, string pathB)
        {
            string hashA = Hash(pathA), hashB = Hash(pathB);
            var baseline = new ReportCheckService().CheckAsync(new ReportCheckRequest(pathA, pathB, null)).GetAwaiter().GetResult();
            if (!baseline.IsSuccess) throw new InvalidOperationException("Approved baseline could not be read.");
            Console.WriteLine("ApprovedBaseline Focused={0}; Detailed={1}", Focused(baseline).Length, baseline.Issues.Count);
            foreach (var group in baseline.Issues.GroupBy(item => item.GovernanceKind))
                Console.WriteLine("BaselineKind={0}; Detailed={1}; Focused={2}", group.Key, group.Count(), group.Count(item => !IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(item))));
            for (int index = 0; index < Mutations.Length; index++)
            {
                string copy = Copy(pathA);
                try
                {
                    int location;
                    if (index == 3)
                    {
                        // This real report has no extractable ProjectName occurrence. Establish an
                        // explicit synthetic label on a private control copy; change only its value.
                        location = Append(copy, "项目名称：合成甲资产评估项目");
                        string control = Copy(pathB);
                        try
                        {
                            Append(control, "项目名称：合成甲资产评估项目");
                            var prepared = new ReportCheckService().CheckAsync(new ReportCheckRequest(copy, control, null)).GetAwaiter().GetResult();
                            if (!prepared.IsSuccess || Focused(prepared).Length != Focused(baseline).Length)
                                throw new InvalidOperationException("Synthetic project control is not clean.");
                            ReplaceSyntheticProject(copy, location);
                            string beforeProject = Hash(copy);
                            var projectResult = new ReportCheckService().CheckAsync(new ReportCheckRequest(copy, control, null)).GetAwaiter().GetResult();
                            var projectHits = projectResult.Issues.Where(item => IsTarget(item, index) && item.Occurrences.Any(o =>
                                o.DocumentLabel == "文档 A" && o.Location.ParagraphIndex == location)).ToArray();
                            Console.WriteLine("Mutation=4; Source=SyntheticLabelOnApprovedCopies; Detected={0}; Focused={1}; Located={0}; Description={2}; TechnicalSuccess={3}; CopyHashUnchanged={4}",
                                projectHits.Length > 0, projectHits.Any(hit => !IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(hit))),
                                projectHits.Any(hit => !string.IsNullOrWhiteSpace(hit.Reason)), projectResult.IsSuccess, beforeProject == Hash(copy));
                            Console.WriteLine("Mutation=4; UnexpectedFocusedKinds={0}", Focused(projectResult).Count(hit => !IsTarget(hit, index)));
                        }
                        finally { File.Delete(control); }
                        continue;
                    }
                    location = index < 3 ? ReplaceBusinessValue(copy, index) : Append(copy, Mutations[index]);
                    string before = Hash(copy);
                    var result = new ReportCheckService().CheckAsync(new ReportCheckRequest(copy, pathB, null)).GetAwaiter().GetResult();
                    var hits = result.Issues.Where(item => IsTarget(item, index) && item.Occurrences.Any(o =>
                        o.DocumentLabel == "文档 A" && o.Location.ParagraphIndex == location)).ToArray();
                    Console.WriteLine("Mutation={0}; Detected={1}; Focused={2}; Located={3}; Description={4}; TechnicalSuccess={5}; CopyHashUnchanged={6}",
                        index + 1, hits.Length > 0, hits.Any(hit => !IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(hit))),
                        hits.Length > 0, hits.Any(hit => !string.IsNullOrWhiteSpace(hit.Reason)), result.IsSuccess, before == Hash(copy));
                    Console.WriteLine("Mutation={0}; UnexpectedFocusedKinds={1}", index + 1, Focused(result).Count(hit => !IsTarget(hit, index)));
                }
                finally { File.Delete(copy); }
            }
            for (int index = 0; index < 3; index++)
            {
                string copy = Copy(pathA);
                try
                {
                    int location = Append(copy, new[] { "本次依据2018年版合成法规。", "法规清单：相关相关规定。", "本次依据2020年修订合成法规，费用参考300万元/km。" }[index]);
                    if (index == 2)
                    {
                        using (var doc = WordprocessingDocument.Open(copy, true))
                        {
                            Body body = doc.MainDocumentPart.Document.Body;
                            var merged = new TableRow(new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("合成法规说明")));
                            body.InsertBefore(new Table(merged, Row("法规依据", "费率参考"), Row("2018年版", "300万元/km")),
                                body.GetFirstChild<SectionProperties>());
                            doc.MainDocumentPart.Document.Save();
                        }
                    }
                    string before = Hash(copy);
                    var result = new ReportCheckService().CheckAsync(new ReportCheckRequest(copy, pathB, null)).GetAwaiter().GetResult();
                    int count = Focused(result).Length - Focused(baseline).Length;
                    Console.WriteLine("LegalControl={0}; FocusedFalsePositives={1}; HashUnchanged={2}", index + 1, count, before == Hash(copy));
                }
                finally { File.Delete(copy); }
            }
            Console.WriteLine("OriginalAHashUnchanged={0}; OriginalBHashUnchanged={1}", hashA == Hash(pathA), hashB == Hash(pathB));
        }

        private static void ReplaceSyntheticProject(string copy, int index)
        {
            using (var doc = WordprocessingDocument.Open(copy, true))
            {
                Paragraph paragraph = doc.MainDocumentPart.Document.Body.Elements<Paragraph>().ElementAt(index);
                paragraph.RemoveAllChildren(); paragraph.Append(new Run(new Text("项目名称：合成乙资产评估项目")));
                doc.MainDocumentPart.Document.Save();
            }
        }

        private static int ReplaceBusinessValue(string copy, int index)
        {
            var read = new DocxDocumentReader().Read(copy);
            string oldValue, newValue;
            int paragraphIndex;
            if (index == 0)
            {
                var occurrence = new MoneyOccurrenceExtractor().Extract(read).First(item => item.Location.ParagraphIndex.HasValue &&
                    item.FieldIdentity.ValueCategory == ValueCategory.AppraisedValue);
                paragraphIndex = occurrence.Location.ParagraphIndex.Value;
                oldValue = occurrence.RawText; newValue = "人民币壹元整";
            }
            else
            {
                BasicFieldType type = index == 1 ? BasicFieldType.ValuationDate : index == 2 ? BasicFieldType.ReportNumber : BasicFieldType.ProjectName;
                var occurrence = new BasicFieldExtractor().Extract(read).First(item => item.FieldType == type && item.Location.ParagraphIndex.HasValue);
                paragraphIndex = occurrence.Location.ParagraphIndex.Value;
                oldValue = occurrence.RawValue;
                newValue = index == 1 ? "2099年12月31日" : index == 2 ? "合成评报字〔2099〕第999号" : "合成独立资产评估项目";
            }
            using (var doc = WordprocessingDocument.Open(copy, true))
            {
                Paragraph paragraph = doc.MainDocumentPart.Document.Body.Elements<Paragraph>().ElementAt(paragraphIndex);
                string text = read.Elements.First(item => item.Location.ParagraphIndex == paragraphIndex).Text;
                if (!text.Contains(oldValue)) throw new InvalidOperationException("Mutation source could not be uniquely mapped.");
                string changed = text.Replace(oldValue, newValue);
                foreach (OpenXmlElement node in paragraph.ChildElements.Where(node => !(node is ParagraphProperties)).ToArray()) node.Remove();
                paragraph.Append(new Run(new Text(changed)));
                doc.MainDocumentPart.Document.Save();
            }
            return paragraphIndex;
        }

        private static bool IsTarget(ReportCheckIssue item, int index) => index < 4
            ? item.IssueType.ToString() == PositiveKinds[index] && item.Severity == IssueSeverity.Serious
            : item.GovernanceKind == PositiveKinds[index];
        private static ReportCheckIssue[] Focused(ReportCheckResult result) => result.Issues.Where(item =>
            !IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(item))).ToArray();
        private static SyntheticDocxFile Clean() => SyntheticDocxFile.Create(P("评估价值为100万元"), P("评估基准日：2026年6月15日"),
            P("报告编号：合成评报字〔2026〕第001号"), P("项目名称：合成甲资产评估项目"));
        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));
        private static TableRow Row(params string[] text) => new TableRow(text.Select(value => new TableCell(P(value))));
        private static string Copy(string path)
        {
            string copy = Path.Combine(Path.GetTempPath(), "AssetAppraisalChecker-Mutation-" + Guid.NewGuid().ToString("N") + ".docx");
            File.Copy(path, copy, false); return copy;
        }
        private static int Append(string copy, string text)
        {
            using (var doc = WordprocessingDocument.Open(copy, true))
            {
                Body body = doc.MainDocumentPart.Document.Body;
                int index = body.Elements<Paragraph>().Count();
                body.InsertBefore(P(text), body.GetFirstChild<SectionProperties>());
                doc.MainDocumentPart.Document.Save(); return index;
            }
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
