using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Mechanical.Formatting;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class FormattingQualityCheckerTests
    {
        [TestMethod]
        public void Font_A_FiveConsistentBodyParagraphs_DoNotReport()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_B_WholeParagraphFontAndSizeOutlier_ReportsWithPeerEvidence()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties = new RunProperties(
                        new RunFonts { EastAsia = "宋体", Ascii = "Arial", HighAnsi = "Arial" },
                        new FontSize { Val = "21" });
                }
            }))
            {
                MechanicalIssue issue = FontIssues(file).Single();
                Assert.AreEqual(MechanicalIssueKind.FontStyleOutlier, issue.Kind);
                StringAssert.Contains(issue.Summary, "疑似格式异常");
                StringAssert.Contains(issue.Reason, "80%");
                Assert.IsTrue(issue.Occurrences.Count >= 3);
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(item => item.Contains("中文字体")));
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(item => item.Contains("字号")));
            }
        }

        [TestMethod]
        public void Font_C_HeadingAndBodyAreNotCompared()
        {
            List<OpenXmlElement> elements = BodyParagraphs(5).Cast<OpenXmlElement>().ToList();
            elements.Insert(0, StyledParagraph("Heading1", "评估结论"));
            using (SyntheticDocxFile file = Create(BaseStyles(), elements.ToArray()))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_D_CjkAndLatinFontsRemainSeparate()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, null, true))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_E_ShortBoldPhraseDoesNotChangeRepresentativeFormatting()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index != 2) return;
                paragraph.RemoveAllChildren<Run>();
                paragraph.Append(
                    new Run(new Text("这是用于局部格式比较的正文段落，")),
                    new Run(new RunProperties(new Bold { Val = true }), new Text("重要")),
                    new Run(new Text("内容。")));
            }))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_F_WholeParagraphBoldOutlier_Reports()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties =
                        new RunProperties(new Bold { Val = true });
                }
            }))
            {
                MechanicalIssue issue = FontIssues(file).Single();
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(item => item.Contains("粗体")));
            }
        }

        [TestMethod]
        public void Font_G_EqualEffectiveFontWithDifferentProvenance_DoesNotReport()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties =
                        new RunProperties(new RunFonts { EastAsia = "仿宋_GB2312" });
                }
            }))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_H_UnknownPeerFonts_DoNotCreateIssue()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties =
                        new RunProperties(new RunFonts { EastAsia = "宋体" });
                }
            }, false, BaseStyles(false, true)))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_I_TableAndBodyContextsAreNotCompared()
        {
            var elements = BodyParagraphs(5).Cast<OpenXmlElement>().ToList();
            elements.Add(new Table(
                new TableProperties(new TableStyle { Val = "TableGrid" }),
                new TableRow(
                    new TableCell(ParagraphWithRun("表格标签甲", new RunFonts { EastAsia = "黑体" })),
                    new TableCell(ParagraphWithRun("表格标签乙", new RunFonts { EastAsia = "黑体" })))));
            using (SyntheticDocxFile file = Create(BaseStyles(), elements.ToArray()))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Font_J_HyperlinkColorAndUnderlineAreExcluded()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index != 2) return;
                paragraph.RemoveAllChildren<Run>();
                paragraph.Append(new Run(new Text("这是用于局部格式比较的普通正文内容。")));
                paragraph.Append(new Hyperlink(
                    new Run(
                        new RunProperties(
                            new Color { Val = "0563C1" },
                            new Underline { Val = UnderlineValues.Single }),
                        new Text("链接"))) { Anchor = "SyntheticBookmark" });
            }))
            {
                Assert.AreEqual(0, FontIssues(file).Count);
            }
        }

        [TestMethod]
        public void Paragraph_A_FiveConsistentParagraphs_DoNotReport()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5))
            {
                Assert.AreEqual(0, ParagraphIssues(file).Count);
            }
        }

        [TestMethod]
        public void Paragraph_B_FirstLineIndentOutlier_Reports()
        {
            AssertParagraphOutlier(new Indentation { FirstLineChars = 0 }, "首行缩进");
        }

        [TestMethod]
        public void Paragraph_C_LineSpacingOutlier_Reports()
        {
            AssertParagraphOutlier(
                new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto },
                "行距规则");
        }

        [TestMethod]
        public void Paragraph_D_AlignmentOutlier_Reports()
        {
            AssertParagraphOutlier(new Justification { Val = JustificationValues.Left }, "对齐方式");
        }

        [TestMethod]
        public void Paragraph_E_HeadingAndBodyIndentAreNotCompared()
        {
            List<OpenXmlElement> elements = BodyParagraphs(5).Cast<OpenXmlElement>().ToList();
            elements.Insert(0, StyledParagraph("Heading1", "一、评估范围"));
            using (SyntheticDocxFile file = Create(BaseStyles(), elements.ToArray()))
            {
                Assert.AreEqual(0, ParagraphIssues(file).Count);
            }
        }

        [TestMethod]
        public void Paragraph_F_TableAndBodyAreNotCompared()
        {
            var elements = BodyParagraphs(5).Cast<OpenXmlElement>().ToList();
            elements.Add(new Table(
                new TableProperties(new TableStyle { Val = "TableGrid" }),
                new TableRow(new TableCell(new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(new Text("表格内容")))))));
            using (SyntheticDocxFile file = Create(BaseStyles(), elements.ToArray()))
            {
                Assert.AreEqual(0, ParagraphIssues(file).Count);
            }
        }

        [TestMethod]
        public void Paragraph_G_SpaceBeforeAndAfterOutlier_Reports()
        {
            AssertParagraphOutlier(
                new SpacingBetweenLines { Before = "200", After = "200", Line = "560", LineRule = LineSpacingRuleValues.Exact },
                "段前距");
        }

        [TestMethod]
        public void Paragraph_H_UnknownPeerSpacing_DoesNotReport()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.ParagraphProperties = new ParagraphProperties(
                        new SpacingBetweenLines { Before = "200" });
                }
            }, false, BaseStyles(true, false)))
            {
                Assert.AreEqual(0, ParagraphIssues(file).Count);
            }
        }

        [TestMethod]
        public void Paragraph_I_TwipsAndPointsEquivalent_DoNotReport()
        {
            DocumentFormattingSnapshot snapshot = ManualEquivalentUnitSnapshot();
            var document = DocumentReadResult.Succeeded(new DocumentElement[0], 0, 0);
            var context = new MechanicalCheckContext("synthetic.docx", document, "文档 A", snapshot);

            Assert.AreEqual(0, new ParagraphFormatChecker().Check(context).Count);
        }

        [TestMethod]
        public void Paragraph_J_InsufficientPeers_DoNotReport()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(4, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Left });
                }
            }))
            {
                Assert.AreEqual(0, ParagraphIssues(file).Count);
            }
        }

        [TestMethod]
        public void AntiFalsePositive_CompleteMixedDocument_HasNoFormattingIssue()
        {
            var elements = new List<OpenXmlElement>
            {
                StyledParagraph("Heading1", "合成资产评估报告")
            };
            elements.AddRange(BodyParagraphs(5));
            Paragraph phrase = elements.OfType<Paragraph>().Last();
            phrase.RemoveAllChildren<Run>();
            phrase.Append(
                new Run(new Text("评估价值中的")),
                new Run(new RunProperties(new Bold { Val = true }), new Text("重要金额")),
                new Run(new Text("仅作局部强调，English value 123.45。")));
            elements.Add(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Quote" }),
                new Run(new Text("这是合法的独立引用样式。"))));
            elements.Add(new Paragraph(
                new Run(new Text("更多信息见")),
                new Hyperlink(
                    new Run(
                        new RunProperties(
                            new Color { Val = "0563C1" },
                            new Underline { Val = UnderlineValues.Single }),
                        new Text("离线说明"))) { Anchor = "SyntheticBookmark" },
                new Run(new Text("。"))));
            elements.Add(new Table(
                new TableProperties(new TableStyle { Val = "TableGrid" }),
                new TableRow(
                    new TableCell(ParagraphWithRun("序号 1", new RunFonts { EastAsia = "黑体" })),
                    new TableCell(ParagraphWithRun("金额 123.45", new RunFonts { EastAsia = "黑体" })))));

            using (SyntheticDocxFile file = Create(BaseStyles(), elements.ToArray()))
            {
                MechanicalCheckContext context = Context(file);
                Assert.AreEqual(0, new FontStyleChecker().Check(context).Count);
                Assert.AreEqual(0, new ParagraphFormatChecker().Check(context).Count);
            }
        }

        [TestMethod]
        public void DesktopMapper_UsesReadableFormattingIssueTypes()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties =
                        new RunProperties(new FontSize { Val = "21" });
                }
            }))
            {
                MechanicalIssue issue = FontIssues(file).Single();
                ReportCheckIssue mapped = new MechanicalIssueMapper().Map(new[] { issue }, 0).Single();
                var viewModel = new IssueViewModel(mapped);
                Assert.AreEqual("字体/字号", viewModel.IssueTypeDisplay);
                Assert.IsTrue(viewModel.Occurrences.All(item => item.DocumentLabel == "文档 A"));
            }
        }

        [TestMethod]
        public async Task DesktopSingleDocument_DisplaysFormattingIssueWithoutChangingFile()
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties =
                        new RunProperties(new FontSize { Val = "21" });
                }
            }))
            {
                string before = Hash(file.FilePath);
                ReportCheckResult result = await new ReportCheckService().CheckAsync(file.FilePath);
                ReportCheckIssue issue = result.Issues.Single(item =>
                    item.IssueTypeDisplayOverride == "字体/字号");

                Assert.IsTrue(result.IsSuccess);
                StringAssert.Contains(issue.Summary, "疑似格式异常");
                Assert.IsTrue(issue.Occurrences.All(item => item.DocumentLabel == "文档 A"));
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public async Task DesktopDualDocument_FormatsEachDocumentIndependentlyAndLabelsEvidence()
        {
            using (SyntheticDocxFile documentA = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.GetFirstChild<Run>().RunProperties =
                        new RunProperties(new FontSize { Val = "21" });
                }
            }))
            using (SyntheticDocxFile documentB = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Left });
                }
            }))
            {
                string hashA = Hash(documentA.FilePath);
                string hashB = Hash(documentB.FilePath);
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null));
                IReadOnlyList<ReportCheckIssue> formattingIssues = result.Issues
                    .Where(item => item.IssueTypeDisplayOverride == "字体/字号" ||
                                   item.IssueTypeDisplayOverride == "段落格式")
                    .ToList()
                    .AsReadOnly();

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(2, formattingIssues.Count);
                Assert.IsTrue(formattingIssues.SelectMany(item => item.Occurrences)
                    .Any(item => item.DocumentLabel == "文档 A"));
                Assert.IsTrue(formattingIssues.SelectMany(item => item.Occurrences)
                    .Any(item => item.DocumentLabel == "文档 B"));
                Assert.AreEqual(hashA, Hash(documentA.FilePath));
                Assert.AreEqual(hashB, Hash(documentB.FilePath));
            }
        }

        [TestMethod]
        [DataRow("EvaluationMethod", "V2-GT-501", "Report.docx")]
        [DataRow("Integrated", "V2-GT-901", "Report.docx")]
        public void ExistingGoldenFormattingSmoke_IsReadOnlyAndEvidenceIsConservative(
            string category,
            string caseId,
            string fileName)
        {
            string path = Path.Combine(
                FindRepositoryRoot(),
                "tests", "AssetAppraisalChecker.Tests", "GoldenCases", "V2",
                category, caseId, fileName);
            string before = Hash(path);
            DocumentReadResult document = new DocxDocumentReader().Read(path);
            var context = new MechanicalCheckContext(path, document, "文档 A");

            IReadOnlyList<MechanicalIssue> fontIssues = new FontStyleChecker().Check(context);
            IReadOnlyList<MechanicalIssue> paragraphIssues = new ParagraphFormatChecker().Check(context);

            Assert.AreEqual(0, fontIssues.Count, caseId + " 不应在证据不足时产生字体格式结论。");
            Assert.AreEqual(0, paragraphIssues.Count, caseId + " 不应在证据不足时产生段落格式结论。");
            Assert.AreEqual(before, Hash(path));
        }

        private static void AssertParagraphOutlier(OpenXmlElement property, string expectedEvidence)
        {
            using (SyntheticDocxFile file = CreateBodyDocument(5, (index, paragraph) =>
            {
                if (index == 2)
                {
                    paragraph.ParagraphProperties = new ParagraphProperties(property.CloneNode(true));
                }
            }))
            {
                MechanicalIssue issue = ParagraphIssues(file).Single();
                StringAssert.Contains(issue.Summary, "疑似格式异常");
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(item => item.Contains(expectedEvidence)));
            }
        }

        private static IReadOnlyList<MechanicalIssue> FontIssues(SyntheticDocxFile file)
        {
            return new FontStyleChecker().Check(Context(file));
        }

        private static IReadOnlyList<MechanicalIssue> ParagraphIssues(SyntheticDocxFile file)
        {
            return new ParagraphFormatChecker().Check(Context(file));
        }

        private static MechanicalCheckContext Context(SyntheticDocxFile file)
        {
            DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(document.IsSuccess);
            return new MechanicalCheckContext(file.FilePath, document, "文档 A");
        }

        private static SyntheticDocxFile CreateBodyDocument(
            int count,
            Action<int, Paragraph> mutate = null,
            bool mixedScript = false,
            Styles styles = null)
        {
            List<Paragraph> paragraphs = BodyParagraphs(count, mixedScript);
            for (int index = 0; index < paragraphs.Count; index++) mutate?.Invoke(index, paragraphs[index]);
            return Create(styles ?? BaseStyles(), paragraphs.Cast<OpenXmlElement>().ToArray());
        }

        private static List<Paragraph> BodyParagraphs(int count, bool mixedScript = false)
        {
            string[] suffixes = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛" };
            return Enumerable.Range(0, count)
                .Select(index => new Paragraph(new Run(new Text(
                    mixedScript
                        ? "这是用于局部格式比较的正文 English Value " + suffixes[index] + "。"
                        : "这是用于局部格式比较的正文段落" + suffixes[index] + "。"))))
                .ToList();
        }

        private static Paragraph StyledParagraph(string styleId, string text)
        {
            return new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
                new Run(new Text(text)));
        }

        private static Paragraph ParagraphWithRun(string text, params OpenXmlElement[] runProperties)
        {
            return new Paragraph(new Run(new RunProperties(runProperties), new Text(text)));
        }

        private static Styles BaseStyles(bool includeFonts = true, bool includeParagraphFormatting = true)
        {
            var runProperties = new StyleRunProperties(
                new FontSize { Val = "24" },
                new Bold { Val = false },
                new Italic { Val = false },
                new Underline { Val = UnderlineValues.None },
                new Color { Val = "000000" });
            if (includeFonts)
            {
                runProperties.PrependChild(new RunFonts
                {
                    EastAsia = "仿宋_GB2312",
                    Ascii = "Times New Roman",
                    HighAnsi = "Times New Roman"
                });
            }

            var normal = new Style(new StyleName { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Normal",
                Default = true
            };
            normal.Append(runProperties);
            if (includeParagraphFormatting)
            {
                normal.Append(new StyleParagraphProperties(
                    new Justification { Val = JustificationValues.Both },
                    new Indentation { Left = "0", Right = "0", FirstLineChars = 200 },
                    new SpacingBetweenLines
                    {
                        Before = "0",
                        After = "0",
                        Line = "560",
                        LineRule = LineSpacingRuleValues.Exact
                    }));
            }

            var heading = new Style(new StyleName { Val = "Heading 1" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading1"
            };
            heading.Append(new BasedOn { Val = "Normal" });
            heading.Append(new StyleRunProperties(
                new RunFonts { EastAsia = "黑体", Ascii = "Arial", HighAnsi = "Arial" },
                new FontSize { Val = "32" },
                new Bold { Val = true }));
            heading.Append(new StyleParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new Indentation { FirstLineChars = 0 }));

            var quote = new Style(new StyleName { Val = "Quote" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Quote"
            };
            quote.Append(new BasedOn { Val = "Normal" });
            quote.Append(new StyleRunProperties(new Italic { Val = true }));
            return new Styles(normal, heading, quote);
        }

        private static SyntheticDocxFile Create(Styles styles, params OpenXmlElement[] elements)
        {
            SyntheticDocxFile file = SyntheticDocxFile.Create(elements);
            using (WordprocessingDocument document = WordprocessingDocument.Open(file.FilePath, true))
            {
                StyleDefinitionsPart stylePart = document.MainDocumentPart.StyleDefinitionsPart
                    ?? document.MainDocumentPart.AddNewPart<StyleDefinitionsPart>();
                stylePart.Styles = styles;
                stylePart.Styles.Save();
            }
            return file;
        }

        private static DocumentFormattingSnapshot ManualEquivalentUnitSnapshot()
        {
            var paragraphs = new List<ParagraphFormattingSnapshot>();
            for (int index = 0; index < 5; index++)
            {
                FormattingValue<decimal> before = index == 4
                    ? FormattingValue<decimal>.Resolved(12m, FormattingSource.DirectFormatting, "12", "Points")
                    : FormattingValue<decimal>.Resolved(240m, FormattingSource.ParagraphStyle, "240", "Twips");
                paragraphs.Add(ManualParagraph(index, before));
            }
            return new DocumentFormattingSnapshot(
                paragraphs,
                new TableFormattingContext[0],
                new StyleMetadataSnapshot[0],
                new DocumentDefaultsSnapshot(false, false),
                new ThemeMetadataSnapshot(false, null, null, null, null, null, null));
        }

        private static ParagraphFormattingSnapshot ManualParagraph(
            int index,
            FormattingValue<decimal> spacingBefore)
        {
            FormattingValue<decimal> number = FormattingValue<decimal>.Unknown();
            FormattingValue<string> text = FormattingValue<string>.Unknown();
            FormattingValue<bool> toggle = FormattingValue<bool>.Unknown();
            FormattingValue<int> integer = FormattingValue<int>.Unknown();
            var run = new RunFormattingSnapshot(
                0, "这是用于单位等价比较的正文段落。", string.Empty,
                FormattingResolutionStatus.Resolved,
                text, text, text, text, number, number,
                toggle, toggle, toggle, toggle, text, toggle, toggle,
                text, text, text, toggle, text, false, false);
            return new ParagraphFormattingSnapshot(
                new FormattingParagraphLocation(index, index, null, null, null, null),
                "这是用于单位等价比较的正文段落。",
                "Normal",
                FormattingResolutionStatus.Resolved,
                text, number, number, number, number,
                spacingBefore, number, number, text,
                integer, integer, toggle, toggle, toggle,
                null,
                new[] { run });
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AssetAppraisalChecker.sln")))
            {
                directory = directory.Parent;
            }
            if (directory == null) throw new DirectoryNotFoundException("无法定位仓库根目录。");
            return directory.FullName;
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
