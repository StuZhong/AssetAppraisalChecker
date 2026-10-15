using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class MechanicalCheckerTests
    {
        [TestMethod]
        public void DuplicateContentChecker_FindsRepeatedWord()
        {
            IReadOnlyList<MechanicalIssue> issues = CheckText(
                new DuplicateContentChecker(), "本项目采用评估评估方法。");

            Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.DuplicateWord));
        }

        [TestMethod]
        public void DuplicateContentChecker_FindsAdjacentRepeatedParagraphWithTwoLocations()
        {
            MechanicalCheckContext context = Context(
                "这是用于合成测试并验证连续重复检测能力的完整评估说明段落。",
                "这是用于合成测试并验证连续重复检测能力的完整评估说明段落。");

            MechanicalIssue issue = new DuplicateContentChecker().Check(context)
                .Single(item => item.Kind == MechanicalIssueKind.DuplicateParagraph);

            Assert.AreEqual(2, issue.Occurrences.Count);
            Assert.AreNotEqual(
                issue.Occurrences[0].Location.ParagraphIndex,
                issue.Occurrences[1].Location.ParagraphIndex);
        }

        [TestMethod]
        public void WhitespaceChecker_FindsConsecutiveSpaces_ButAllowsSingleEnglishSpace()
        {
            IReadOnlyList<MechanicalIssue> abnormal = CheckText(
                new WhitespaceAndCharacterChecker(), "评估价值  为一百万元。");
            IReadOnlyList<MechanicalIssue> normal = CheckText(
                new WhitespaceAndCharacterChecker(), "Local model runs offline.");

            Assert.IsTrue(abnormal.Any(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces));
            Assert.AreEqual(0, normal.Count);
        }

        [TestMethod]
        public void PunctuationChecker_FindsRepeatedPunctuation()
        {
            IReadOnlyList<MechanicalIssue> issues = CheckText(
                new PunctuationChecker(), "本项目评估完成。。");

            Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.RepeatedPunctuation));
        }

        [TestMethod]
        public void PunctuationChecker_DistinguishesPairedAndUnpairedSymbols()
        {
            IReadOnlyList<MechanicalIssue> valid = CheckText(
                new PunctuationChecker(), "本次采用《资产评估准则》（试行）“相关规定”。");
            IReadOnlyList<MechanicalIssue> invalid = CheckText(
                new PunctuationChecker(), "本次采用《资产评估准则（试行“相关规定。");

            Assert.IsFalse(valid.Any(item =>
                item.Kind == MechanicalIssueKind.UnpairedChineseBracket ||
                item.Kind == MechanicalIssueKind.UnpairedChineseQuote ||
                item.Kind == MechanicalIssueKind.UnpairedBookTitle));
            Assert.IsTrue(invalid.Any(item => item.Kind == MechanicalIssueKind.UnpairedChineseBracket));
            Assert.IsTrue(invalid.Any(item => item.Kind == MechanicalIssueKind.UnpairedChineseQuote));
            Assert.IsTrue(invalid.Any(item => item.Kind == MechanicalIssueKind.UnpairedBookTitle));
        }

        [TestMethod]
        public void SequenceChecker_FindsJumpAndDuplicate()
        {
            MechanicalCheckContext jumpContext = Context("一、范围", "三、结论");
            MechanicalCheckContext duplicateContext = Context("1. 范围", "2. 方法", "2. 结论");

            Assert.IsTrue(new SequenceChecker().Check(jumpContext)
                .Any(item => item.Kind == MechanicalIssueKind.SequenceJump));
            Assert.IsTrue(new SequenceChecker().Check(duplicateContext)
                .Any(item => item.Kind == MechanicalIssueKind.SequenceDuplicate));
        }

        [TestMethod]
        public void SequenceChecker_NormalSequence_DoesNotReportIssue()
        {
            MechanicalCheckContext context = Context("（一）范围", "（二）方法", "（三）结论");

            Assert.AreEqual(0, new SequenceChecker().Check(context).Count);
        }

        [TestMethod]
        public void DocumentResidueChecker_FindsCommentRevisionHighlightAndStrikethrough()
        {
            using (SyntheticDocxFile file = CreateResidueDocument())
            {
                DocumentReadResult read = new AssetAppraisalChecker.Document.DocxDocumentReader()
                    .Read(file.FilePath);
                var context = new MechanicalCheckContext(file.FilePath, read, "文档 A");

                IReadOnlyList<MechanicalIssue> issues = new DocumentResidueChecker().Check(context);

                Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.CommentResidue));
                Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.InsertedRevision));
                Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.DeletedRevision));
                Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.Highlight));
                Assert.IsTrue(issues.Any(item => item.Kind == MechanicalIssueKind.Strikethrough));
            }
        }

        [TestMethod]
        public void MechanicalPipeline_IsReadOnlyAndPreservesSourceLabel()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("评估评估价值为一百万元。")))))
            {
                string before = Hash(file.FilePath);
                DocumentReadResult read = new AssetAppraisalChecker.Document.DocxDocumentReader()
                    .Read(file.FilePath);

                IReadOnlyList<MechanicalIssue> issues = new MechanicalDocumentCheckPipeline()
                    .Check(new MechanicalCheckContext(file.FilePath, read, "文档 B"));

                Assert.IsTrue(issues.Count > 0);
                Assert.IsTrue(issues.SelectMany(item => item.Occurrences)
                    .All(item => item.SourceDocument == "文档 B"));
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        private static IReadOnlyList<MechanicalIssue> CheckText(
            IMechanicalDocumentChecker checker,
            string text)
        {
            return checker.Check(Context(text));
        }

        private static MechanicalCheckContext Context(params string[] texts)
        {
            var elements = new List<DocumentElement>();
            for (int index = 0; index < texts.Length; index++)
            {
                elements.Add(new DocumentElement(
                    DocumentElementType.Paragraph,
                    texts[index],
                    new DocumentLocation(
                        DocumentPart.MainBody,
                        index,
                        null,
                        null,
                        null,
                        new string[0]),
                    index,
                    string.Empty,
                    null));
            }

            DocumentReadResult document = DocumentReadResult.Succeeded(elements, texts.Length, 0);
            return new MechanicalCheckContext("synthetic.docx", document, "文档 A");
        }

        private static SyntheticDocxFile CreateResidueDocument()
        {
            SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("批注锚点"))));

            using (WordprocessingDocument document = WordprocessingDocument.Open(file.FilePath, true))
            {
                MainDocumentPart mainPart = document.MainDocumentPart;
                Paragraph anchor = mainPart.Document.Body.Elements<Paragraph>().First();
                anchor.InsertAt(new CommentRangeStart { Id = "0" }, 0);
                anchor.Append(new CommentRangeEnd { Id = "0" });
                anchor.Append(new Run(new CommentReference { Id = "0" }));

                WordprocessingCommentsPart commentsPart =
                    mainPart.AddNewPart<WordprocessingCommentsPart>();
                commentsPart.Comments = new Comments(
                    new Comment(
                        new Paragraph(new Run(new Text("合成批注内容"))))
                    {
                        Id = "0",
                        Author = "合成测试"
                    });

                mainPart.Document.Body.Append(
                    new Paragraph(
                        new InsertedRun(new Run(new Text("插入修订内容")))
                        {
                            Id = "1",
                            Author = "合成测试"
                        },
                        new DeletedRun(new Run(new DeletedText("删除修订内容")))
                        {
                            Id = "2",
                            Author = "合成测试"
                        }));
                mainPart.Document.Body.Append(
                    new Paragraph(
                        new Run(
                            new RunProperties(new Highlight { Val = HighlightColorValues.Yellow }),
                            new Text("高亮内容")),
                        new Run(
                            new RunProperties(new Strike()),
                            new Text("删除线内容"))));
                mainPart.Document.Save();
            }

            return file;
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
