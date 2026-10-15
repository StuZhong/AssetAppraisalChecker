using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class AssessmentObjectRoleBoundaryTests
    {
        private const string ObjectText = "评估对象为合成标的公司转让的机器设备，涉及12台设备。";

        [TestMethod]
        [DataRow("委托人为合成委托公司拟购买机器设备，涉及12台设备。")]
        [DataRow("被评估单位为合成标的公司拟转让机器设备，涉及12台设备。")]
        [DataRow("项目名称：合成委托公司拟购买机器设备，涉及12台设备。")]
        public void OtherRoles_DoNotEstablishOwner(string text)
        {
            using (var file = Create(text))
                Assert.AreEqual(0, Extract(file).Count, "A role or project title is not an ownership assertion.");
        }

        [TestMethod]
        public void ExplicitObject_IsNotContaminatedByClientAction()
        {
            using (var file = Create("委托人为合成委托公司拟购买机器设备；" + ObjectText))
                Assert.AreEqual("合成标的公司", Extract(file).Single().Owner);
        }

        [TestMethod]
        public void AppraisalScope_IsNotObjectLocalScope()
        {
            using (var file = Create(ObjectText + "评估范围为合成标的公司全部资产及负债。"))
                Assert.AreEqual("12台设备", Extract(file).Single().Scope);
        }

        [TestMethod]
        public async Task DifferentClientAndAppraisedEntity_ObjectAndScope_DoNotConflict()
        {
            using (var a = Create("委托人为合成委托公司。", "被评估单位为合成标的公司。",
                ObjectText + "评估范围为合成标的公司全部资产及负债。"))
            using (var b = Create(ObjectText))
            {
                var result = await Check(a, b);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.Issues.Count(issue => issue.FieldName == "评估对象"));
            }
        }

        [TestMethod]
        public async Task ExplicitOwnerMutation_RemainsConflictWithBothLocationsAndReadOnlyInputs()
        {
            using (var a = Create("委托人为合成委托公司。", ObjectText))
            using (var b = Create("委托人为合成委托公司。", ObjectText.Replace("合成标的公司", "合成另一公司")))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var result = await Check(a, b);
                Assert.IsTrue(result.IsSuccess);
                var issue = result.Issues.Single(item => item.FieldName == "评估对象");
                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).ToArray());
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.ParagraphIndex == 1));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        [TestMethod]
        public void RunSplit_DoesNotChangeRoleBinding()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(
                new Run(new Text("委托人为合成委托公司拟购")), new Run(new Text("买机器设备，产权持有")),
                new Run(new Text("人为合成标的公司；" + ObjectText)))))
                Assert.AreEqual("合成标的公司", Extract(file).Single().Owner);
        }

        [TestMethod]
        public void StandaloneAppraisedEntity_DoesNotMasqueradeAsAssessmentObject()
        {
            using (var file = Create("被评估单位：合成标的公司。", "评估对象：合成标的公司股东全部权益价值。",
                "评估范围：合成标的公司全部资产及负债。"))
            {
                var fact = Extract(file).Single();
                Assert.IsTrue(fact.IsEquity);
                Assert.AreEqual(1, fact.SourceLocation.ParagraphIndex);
                Assert.IsNull(fact.Owner, "An explicit equity object is not an ownership assertion.");
                Assert.IsNull(fact.Quantity, "Never invent legacy asset fields to extract equity.");
            }
        }

        [TestMethod]
        public async Task ExplicitLocalAlias_DoesNotCreateFalseOwnerConflict()
        {
            const string definition = "合成标的有限责任公司（以下简称“合成标的公司”）。";
            using (var a = Create(definition, ObjectText.Replace("合成标的公司", "合成标的有限责任公司")))
            using (var b = Create(definition, ObjectText))
            {
                Assert.AreEqual("合成标的有限责任公司", Extract(b).Single().Owner);
                Assert.AreEqual(0, Extract(b).Single().OwnerAliasEvidence.Single().Location.ParagraphIndex);
                Assert.AreEqual(1, Extract(b).Single().SourceLocation.ParagraphIndex);
                Assert.AreEqual(ObjectText, Extract(b).Single().OriginalText);
                var result = await Check(a, b);
                Assert.AreEqual(0, result.Issues.Count(issue => issue.FieldName == "评估对象"));
            }
        }

        [TestMethod]
        public void CompetingAliasDefinitions_DoNotSelectAnEntity()
        {
            using (var file = Create("合成标的有限责任公司（以下简称“合成标的公司”）。",
                "合成另一有限责任公司（以下简称“合成标的公司”）。", ObjectText))
                Assert.AreEqual(0, Extract(file).Count);
        }

        [TestMethod]
        public async Task SimilarCompanyNames_AreNotMergedWithoutDefinition()
        {
            using (var a = Create(ObjectText.Replace("合成标的公司", "合成标的有限责任公司")))
            using (var b = Create(ObjectText))
            {
                Assert.AreNotEqual(Extract(a).Single().Owner, Extract(b).Single().Owner);
                var result = await Check(a, b);
                Assert.AreEqual(IssueSeverity.Serious,
                    result.Issues.Single(issue => issue.FieldName == "评估对象").Severity);
            }
        }

        private static System.Collections.Generic.IReadOnlyList<AssetAppraisalChecker.Facts.Models.AssessmentObjectFact>
            Extract(SyntheticDocxFile file)
        {
            return new AssessmentObjectFactExtractor().Extract(new DocxDocumentReader().Read(file.FilePath), "DocumentA");
        }

        private static Task<ReportCheckResult> Check(SyntheticDocxFile a, SyntheticDocxFile b)
        {
            return new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
        }

        private static SyntheticDocxFile Create(params string[] paragraphs)
        {
            return SyntheticDocxFile.Create(paragraphs.Select(text => new Paragraph(new Run(new Text(text)))).ToArray());
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
