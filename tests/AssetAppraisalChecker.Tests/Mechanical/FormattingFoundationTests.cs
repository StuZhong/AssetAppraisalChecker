using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Mechanical.Formatting;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class FormattingFoundationTests
    {
        [TestMethod]
        public void A_RunDirectFormatting_ResolvesFontAndSize()
        {
            using (SyntheticDocxFile file = Create(
                null,
                null,
                Paragraph(Run("直接格式", new RunFonts
                {
                    Ascii = "Arial",
                    HighAnsi = "Arial",
                    EastAsia = "仿宋"
                }, new FontSize { Val = "24" }))))
            {
                RunFormattingSnapshot run = ReadSingleRun(file);
                AssertResolved(run.EffectiveFontAscii, "Arial", FormattingSource.DirectFormatting);
                AssertResolved(run.EffectiveFontEastAsia, "仿宋", FormattingSource.DirectFormatting);
                AssertResolved(run.EffectiveFontSize, 12m, FormattingSource.DirectFormatting);
            }
        }

        [TestMethod]
        public void B_ParagraphStyle_ProvidesRunFontAndSize()
        {
            Style body = ParagraphStyle("Body", null,
                new StyleRunProperties(
                    new RunFonts { EastAsia = "宋体" },
                    new FontSize { Val = "22" }));
            using (SyntheticDocxFile file = Create(
                new Styles(body), null,
                StyledParagraph("Body", Run("段落样式"))))
            {
                RunFormattingSnapshot run = ReadSingleRun(file);
                AssertResolved(run.EffectiveFontEastAsia, "宋体", FormattingSource.ParagraphStyle);
                AssertResolved(run.EffectiveFontSize, 11m, FormattingSource.ParagraphStyle);
            }
        }

        [TestMethod]
        public void C_TwoLevelBasedOn_InheritsBaseAndChildValues()
        {
            Style baseStyle = ParagraphStyle("Base", null,
                new StyleRunProperties(new RunFonts { Ascii = "Calibri", EastAsia = "宋体" }));
            Style child = ParagraphStyle("Child", "Base",
                new StyleRunProperties(new FontSize { Val = "26" }));
            using (SyntheticDocxFile file = Create(
                new Styles(baseStyle, child), null,
                StyledParagraph("Child", Run("两级继承"))))
            {
                RunFormattingSnapshot run = ReadSingleRun(file);
                AssertResolved(run.EffectiveFontAscii, "Calibri", FormattingSource.ParagraphStyle);
                AssertResolved(run.EffectiveFontSize, 13m, FormattingSource.ParagraphStyle);
            }
        }

        [TestMethod]
        public void D_CharacterStyle_OverridesParagraphStyle()
        {
            Style paragraphStyle = ParagraphStyle("Body", null,
                new StyleRunProperties(new RunFonts { EastAsia = "宋体" }));
            Style characterStyle = CharacterStyle("Emphasis", null,
                new StyleRunProperties(new RunFonts { EastAsia = "黑体" }));
            Run run = Run("字符样式");
            run.PrependChild(new RunProperties(new RunStyle { Val = "Emphasis" }));
            using (SyntheticDocxFile file = Create(
                new Styles(paragraphStyle, characterStyle), null,
                StyledParagraph("Body", run)))
            {
                RunFormattingSnapshot snapshot = ReadSingleRun(file);
                AssertResolved(snapshot.EffectiveFontEastAsia, "黑体", FormattingSource.CharacterStyle);
            }
        }

        [TestMethod]
        public void E_RunDirectFormatting_OverridesStyles()
        {
            Style paragraphStyle = ParagraphStyle("Body", null,
                new StyleRunProperties(new RunFonts { EastAsia = "宋体" }, new FontSize { Val = "20" }));
            Style characterStyle = CharacterStyle("Emphasis", null,
                new StyleRunProperties(new RunFonts { EastAsia = "黑体" }, new FontSize { Val = "22" }));
            Run run = Run("最终覆盖", new RunStyle { Val = "Emphasis" },
                new RunFonts { EastAsia = "楷体" }, new FontSize { Val = "28" });
            using (SyntheticDocxFile file = Create(
                new Styles(paragraphStyle, characterStyle), null,
                StyledParagraph("Body", run)))
            {
                RunFormattingSnapshot snapshot = ReadSingleRun(file);
                AssertResolved(snapshot.EffectiveFontEastAsia, "楷体", FormattingSource.DirectFormatting);
                AssertResolved(snapshot.EffectiveFontSize, 14m, FormattingSource.DirectFormatting);
            }
        }

        [TestMethod]
        public void F_DocumentDefaults_ProvideRunAndParagraphValues()
        {
            Styles styles = StylesWithDefaults(
                new RunPropertiesBaseStyle(
                    new RunFonts { EastAsia = "默认中文" },
                    new FontSize { Val = "21" }),
                new ParagraphPropertiesBaseStyle(
                    new Justification { Val = JustificationValues.Both }));
            using (SyntheticDocxFile file = Create(styles, null, Paragraph(Run("默认格式"))))
            {
                DocumentFormattingSnapshot snapshot = Read(file);
                Assert.IsTrue(snapshot.DocumentDefaults.HasRunDefaults);
                Assert.IsTrue(snapshot.DocumentDefaults.HasParagraphDefaults);
                AssertResolved(snapshot.Paragraphs[0].Runs[0].EffectiveFontEastAsia,
                    "默认中文", FormattingSource.DocumentDefault);
                AssertResolved(snapshot.Paragraphs[0].EffectiveAlignment,
                    "both", FormattingSource.DocumentDefault);
            }
        }

        [TestMethod]
        public void DefaultParagraphAndCharacterStyles_AreAppliedWhenIdsAreAbsent()
        {
            Style paragraphStyle = ParagraphStyle("Normal", null,
                new StyleRunProperties(
                    new RunFonts { EastAsia = "默认段落字体" },
                    new FontSize { Val = "23" }));
            paragraphStyle.Default = true;
            Style characterStyle = CharacterStyle("DefaultParagraphFont", null,
                new StyleRunProperties(new Italic()));
            characterStyle.Default = true;

            using (SyntheticDocxFile file = Create(
                new Styles(paragraphStyle, characterStyle),
                null,
                Paragraph(Run("默认样式"))))
            {
                ParagraphFormattingSnapshot paragraph = Read(file).Paragraphs.Single();
                RunFormattingSnapshot run = paragraph.Runs.Single();
                Assert.AreEqual("Normal", paragraph.StyleId);
                Assert.AreEqual("DefaultParagraphFont", run.CharacterStyleId);
                AssertResolved(run.EffectiveFontEastAsia, "默认段落字体", FormattingSource.ParagraphStyle);
                AssertResolved(run.EffectiveFontSize, 11.5m, FormattingSource.ParagraphStyle);
                AssertResolved(run.Italic, true, FormattingSource.CharacterStyle);
            }
        }

        [TestMethod]
        public void G_EastAsiaAndAsciiFonts_RemainIndependent()
        {
            using (SyntheticDocxFile file = Create(null, null,
                Paragraph(Run("中文 ABC 123", new RunFonts
                {
                    Ascii = "Times New Roman",
                    HighAnsi = "Times New Roman",
                    EastAsia = "仿宋_GB2312",
                    ComplexScript = "Arial"
                }))))
            {
                RunFormattingSnapshot run = ReadSingleRun(file);
                Assert.AreEqual("Times New Roman", run.EffectiveFontAscii.Value);
                Assert.AreEqual("仿宋_GB2312", run.EffectiveFontEastAsia.Value);
                Assert.AreEqual("Arial", run.EffectiveFontComplexScript.Value);
            }
        }

        [TestMethod]
        public void H_ToggleValues_DistinguishInheritedTrueAndDirectFalse()
        {
            Style body = ParagraphStyle("Body", null, new StyleRunProperties(new Bold()));
            using (SyntheticDocxFile file = Create(
                new Styles(body), null,
                StyledParagraph("Body",
                    Run("继承粗体"),
                    Run("明确非粗体", new Bold { Val = false }))))
            {
                IReadOnlyList<RunFormattingSnapshot> runs = Read(file).Paragraphs[0].Runs;
                AssertResolved(runs[0].Bold, true, FormattingSource.ParagraphStyle);
                AssertResolved(runs[1].Bold, false, FormattingSource.DirectFormatting);
            }
        }

        [TestMethod]
        public void I_ParagraphAlignmentIndentAndSpacing_AreNormalized()
        {
            var properties = new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new Indentation { Left = "720", Right = "360", FirstLine = "480", Hanging = "120" },
                new SpacingBetweenLines
                {
                    Before = "240",
                    After = "120",
                    Line = "560",
                    LineRule = LineSpacingRuleValues.Exact
                });
            using (SyntheticDocxFile file = Create(null, null,
                new Paragraph(properties, Run("段落格式"))))
            {
                ParagraphFormattingSnapshot paragraph = Read(file).Paragraphs[0];
                Assert.AreEqual("center", paragraph.EffectiveAlignment.Value);
                Assert.AreEqual(720m, paragraph.EffectiveLeftIndent.Value);
                Assert.AreEqual(480m, paragraph.EffectiveFirstLineIndent.Value);
                Assert.AreEqual(240m, paragraph.EffectiveSpacingBefore.Value);
                Assert.AreEqual("Twips", paragraph.EffectiveSpacingBefore.Unit);
                Assert.AreEqual(28m, paragraph.EffectiveLineSpacing.Value);
                Assert.AreEqual("Points", paragraph.EffectiveLineSpacing.Unit);
            }
        }

        [TestMethod]
        public void ParagraphCharacterAndLineUnits_OverrideTwipAlternatives()
        {
            var properties = new ParagraphProperties(
                new Indentation
                {
                    Left = "720",
                    LeftChars = 200,
                    FirstLine = "480",
                    FirstLineChars = 150
                },
                new SpacingBetweenLines
                {
                    Before = "240",
                    BeforeLines = 125
                });
            using (SyntheticDocxFile file = Create(null, null,
                new Paragraph(properties, Run("字符单位段落"))))
            {
                ParagraphFormattingSnapshot paragraph = Read(file).Paragraphs[0];
                Assert.AreEqual(2m, paragraph.EffectiveLeftIndent.Value);
                Assert.AreEqual("Characters", paragraph.EffectiveLeftIndent.Unit);
                Assert.AreEqual(1.5m, paragraph.EffectiveFirstLineIndent.Value);
                Assert.AreEqual("Characters", paragraph.EffectiveFirstLineIndent.Unit);
                Assert.AreEqual(1.25m, paragraph.EffectiveSpacingBefore.Value);
                Assert.AreEqual("Lines", paragraph.EffectiveSpacingBefore.Unit);
            }
        }

        [TestMethod]
        public void J_NumberingIdAndLevel_AreResolved()
        {
            var numbering = new NumberingProperties(
                new NumberingLevelReference { Val = 2 },
                new NumberingId { Val = 9 });
            using (SyntheticDocxFile file = Create(null, null,
                new Paragraph(new ParagraphProperties(numbering), Run("编号段落"))))
            {
                ParagraphFormattingSnapshot paragraph = Read(file).Paragraphs[0];
                AssertResolved(paragraph.NumberingId, 9, FormattingSource.DirectFormatting);
                AssertResolved(paragraph.NumberingLevel, 2, FormattingSource.DirectFormatting);
            }
        }

        [TestMethod]
        public void K_TableCellParagraph_RecordsStableContext()
        {
            Table table = new Table(
                new TableProperties(new TableStyle { Val = "TableGrid" }),
                new TableRow(
                    new TableCell(Paragraph(Run("单元格 A"))),
                    new TableCell(Paragraph(Run("单元格 B")))));
            using (SyntheticDocxFile file = Create(null, null, table))
            {
                DocumentFormattingSnapshot snapshot = Read(file);
                Assert.AreEqual(2, snapshot.Paragraphs.Count);
                Assert.IsTrue(snapshot.Paragraphs.All(item => item.IsInTable));
                Assert.AreEqual(0, snapshot.Paragraphs[1].Location.TableIndex);
                Assert.AreEqual(0, snapshot.Paragraphs[1].Location.RowIndex);
                Assert.AreEqual(1, snapshot.Paragraphs[1].Location.CellIndex);
                Assert.AreEqual("TableGrid", snapshot.Paragraphs[1].TableContext.TableStyleId);
                Assert.AreEqual(
                    FormattingResolutionStatus.NotImplemented,
                    snapshot.Paragraphs[1].TableContext.ConditionalTableStyleResolution);
            }
        }

        [TestMethod]
        public void L_MissingStyleReference_IsUnresolvedWithoutLosingDirectFormatting()
        {
            using (SyntheticDocxFile file = Create(null, null,
                StyledParagraph("MissingStyle", Run("缺失样式", new FontSize { Val = "24" }))))
            {
                ParagraphFormattingSnapshot paragraph = Read(file).Paragraphs[0];
                Assert.AreEqual(FormattingResolutionStatus.Unresolved, paragraph.StyleResolution);
                AssertResolved(paragraph.Runs[0].EffectiveFontSize, 12m, FormattingSource.DirectFormatting);
                Assert.AreEqual(
                    FormattingResolutionStatus.Unresolved,
                    paragraph.Runs[0].EffectiveFontEastAsia.Status);
            }
        }

        [TestMethod]
        public void M_BasedOnCycle_IsUnresolvedAndDoesNotLoop()
        {
            Style first = ParagraphStyle("First", "Second", new StyleRunProperties(new Bold()));
            Style second = ParagraphStyle("Second", "First", new StyleRunProperties(new Italic()));
            using (SyntheticDocxFile file = Create(
                new Styles(first, second), null,
                StyledParagraph("First", Run("循环样式"))))
            {
                ParagraphFormattingSnapshot paragraph = Read(file).Paragraphs[0];
                Assert.AreEqual(FormattingResolutionStatus.Unresolved, paragraph.StyleResolution);
                Assert.AreEqual(FormattingResolutionStatus.Unresolved, paragraph.Runs[0].Bold.Status);
            }
        }

        [TestMethod]
        public void N_ThemeFont_IsResolvedWhenThemeTypefaceIsExplicit()
        {
            using (SyntheticDocxFile file = Create(
                null,
                CreateTheme("Synthetic Latin", "合成中文", "Synthetic CS"),
                Paragraph(Run("主题字体", new RunFonts
                {
                    AsciiTheme = ThemeFontValues.MajorAscii,
                    EastAsiaTheme = ThemeFontValues.MajorEastAsia,
                    ComplexScriptTheme = ThemeFontValues.MajorBidi
                }))))
            {
                RunFormattingSnapshot run = ReadSingleRun(file);
                AssertResolved(run.EffectiveFontAscii, "Synthetic Latin", FormattingSource.Theme);
                AssertResolved(run.EffectiveFontEastAsia, "合成中文", FormattingSource.Theme);
                Assert.AreEqual("majorEastAsia", run.EffectiveFontEastAsia.Reference);
            }
        }

        [TestMethod]
        public void O_ThemeFont_RemainsUnresolvedWhenTypefaceIsEmpty()
        {
            using (SyntheticDocxFile file = Create(
                null,
                CreateTheme("Synthetic Latin", string.Empty, string.Empty),
                Paragraph(Run("未解析主题中文", new RunFonts
                {
                    EastAsiaTheme = ThemeFontValues.MinorEastAsia
                }))))
            {
                RunFormattingSnapshot run = ReadSingleRun(file);
                Assert.AreEqual(FormattingResolutionStatus.Unresolved, run.EffectiveFontEastAsia.Status);
                Assert.AreEqual("minorEastAsia", run.EffectiveFontEastAsia.Reference);
            }
        }

        [TestMethod]
        public void DiagnosticFormatter_EmitsEffectiveValuesAndProvenance()
        {
            using (SyntheticDocxFile file = Create(null, null,
                Paragraph(Run("诊断输出", new RunFonts { EastAsia = "仿宋" }, new FontSize { Val = "24" }))))
            {
                string output = new FormattingSnapshotDiagnosticFormatter().Format(Read(file));
                StringAssert.Contains(output, "Paragraph 0");
                StringAssert.Contains(output, "EastAsiaFont: 仿宋 <- DirectFormatting");
                StringAssert.Contains(output, "FontSize: 12 Points <- DirectFormatting");
            }
        }

        [TestMethod]
        public void ExistingDesensitizedGoldenDocx_CreatesReadOnlyRichSnapshot()
        {
            string root = FindRepositoryRoot();
            string path = Path.Combine(
                root,
                "tests", "AssetAppraisalChecker.Tests", "GoldenCases", "V2",
                "EvaluationMethod", "V2-GT-501", "Report.docx");
            string before = Hash(path);

            DocumentFormattingSnapshot snapshot = new DocumentFormattingSnapshotReader().Read(path);

            Assert.IsTrue(snapshot.Paragraphs.Count(item => !item.IsInTable) >= 3);
            Assert.IsTrue(snapshot.Paragraphs.Count(item => item.IsInTable) >= 2);
            ParagraphFormattingSnapshot title = snapshot.Paragraphs.Single(
                item => item.TextPreview == "资产评估方法合成测试文档");
            ParagraphFormattingSnapshot heading = snapshot.Paragraphs.Single(
                item => item.TextPreview == "评估方法");
            ParagraphFormattingSnapshot body = snapshot.Paragraphs.Single(
                item => item.TextPreview == "房屋建筑物采用成本法。");
            ParagraphFormattingSnapshot note = snapshot.Paragraphs.Single(
                item => item.TextPreview.StartsWith("说明：", StringComparison.Ordinal));
            RunFormattingSnapshot titleRun = title.Runs.Single();
            RunFormattingSnapshot headingRun = heading.Runs.Single();
            RunFormattingSnapshot bodyRun = body.Runs.Single();
            RunFormattingSnapshot noteRun = note.Runs.Single();

            AssertResolved(title.EffectiveAlignment, "center", FormattingSource.DirectFormatting);
            AssertResolved(titleRun.EffectiveFontEastAsia, "Microsoft YaHei", FormattingSource.DirectFormatting);
            AssertResolved(titleRun.EffectiveFontAscii, "Microsoft YaHei", FormattingSource.DirectFormatting);
            AssertResolved(titleRun.EffectiveFontSize, 17m, FormattingSource.DirectFormatting);
            AssertResolved(titleRun.Bold, true, FormattingSource.DirectFormatting);

            AssertResolved(headingRun.EffectiveFontSize, 13m, FormattingSource.DirectFormatting);
            AssertResolved(headingRun.Bold, true, FormattingSource.DirectFormatting);
            AssertResolved(bodyRun.EffectiveFontSize, 11m, FormattingSource.ParagraphStyle);
            AssertResolved(body.EffectiveSpacingAfter, 200m, FormattingSource.DirectFormatting);
            AssertResolved(body.EffectiveLineSpacing, 1.15m, FormattingSource.DocumentDefault);
            Assert.AreEqual("LineMultiple", body.EffectiveLineSpacing.Unit);
            AssertResolved(noteRun.EffectiveFontSize, 9m, FormattingSource.DirectFormatting);
            AssertResolved(noteRun.Italic, true, FormattingSource.DirectFormatting);

            ParagraphFormattingSnapshot tableCell = snapshot.Paragraphs.Single(
                item => item.TextPreview == "V2-GT-501");
            Assert.IsTrue(tableCell.IsInTable);
            Assert.AreEqual(0, tableCell.Location.TableIndex);
            Assert.AreEqual(0, tableCell.Location.RowIndex);
            Assert.AreEqual(1, tableCell.Location.CellIndex);
            AssertResolved(tableCell.Runs.Single().EffectiveFontEastAsia,
                "Microsoft YaHei", FormattingSource.DirectFormatting);
            AssertResolved(tableCell.Runs.Single().EffectiveFontSize,
                11m, FormattingSource.ParagraphStyle);
            Assert.AreEqual(before, Hash(path));
        }

        [TestMethod]
        public void ExistingIntegratedGoldenDocx_ConfirmsBodyParagraphFormattingReadOnly()
        {
            string root = FindRepositoryRoot();
            string path = Path.Combine(
                root,
                "tests", "AssetAppraisalChecker.Tests", "GoldenCases", "V2",
                "Integrated", "V2-GT-901", "Report.docx");
            string before = Hash(path);

            DocumentFormattingSnapshot snapshot = new DocumentFormattingSnapshotReader().Read(path);

            Assert.AreEqual(5, snapshot.Paragraphs.Count);
            ParagraphFormattingSnapshot title = snapshot.Paragraphs.Single(
                item => item.TextPreview == "合成资产评估报告");
            AssertResolved(title.EffectiveAlignment, "center", FormattingSource.DirectFormatting);
            AssertResolved(title.Runs.Single().EffectiveFontEastAsia, "黑体", FormattingSource.DirectFormatting);
            AssertResolved(title.Runs.Single().EffectiveFontAscii, "SimHei", FormattingSource.DirectFormatting);
            AssertResolved(title.Runs.Single().EffectiveFontSize, 18m, FormattingSource.DirectFormatting);
            AssertResolved(title.Runs.Single().Bold, true, FormattingSource.DirectFormatting);

            IReadOnlyList<ParagraphFormattingSnapshot> bodyParagraphs = snapshot.Paragraphs
                .Where(item => item != title)
                .ToList();
            Assert.AreEqual(4, bodyParagraphs.Count);
            Assert.IsTrue(bodyParagraphs.Any(item => item.TextPreview.Contains("2026")));
            Assert.IsTrue(bodyParagraphs.Any(item => item.TextPreview.Contains("998.22")));
            foreach (ParagraphFormattingSnapshot paragraph in bodyParagraphs)
            {
                AssertResolved(paragraph.EffectiveSpacingAfter, 180m, FormattingSource.DirectFormatting);
                AssertResolved(paragraph.EffectiveLineSpacing, 1.35m, FormattingSource.DirectFormatting);
                Assert.AreEqual("LineMultiple", paragraph.EffectiveLineSpacing.Unit);
                AssertResolved(paragraph.Runs.Single().EffectiveFontSize,
                    12m, FormattingSource.ParagraphStyle);
            }

            Assert.AreEqual(before, Hash(path));
        }

        private static SyntheticDocxFile Create(
            Styles styles,
            A.Theme theme,
            params DocumentFormat.OpenXml.OpenXmlElement[] bodyElements)
        {
            SyntheticDocxFile file = SyntheticDocxFile.Create(bodyElements);
            if (styles == null && theme == null)
            {
                return file;
            }

            using (WordprocessingDocument document = WordprocessingDocument.Open(file.FilePath, true))
            {
                MainDocumentPart mainPart = document.MainDocumentPart;
                if (styles != null)
                {
                    StyleDefinitionsPart stylePart = mainPart.StyleDefinitionsPart
                        ?? mainPart.AddNewPart<StyleDefinitionsPart>();
                    stylePart.Styles = styles;
                    stylePart.Styles.Save();
                }
                if (theme != null)
                {
                    ThemePart themePart = mainPart.ThemePart ?? mainPart.AddNewPart<ThemePart>();
                    themePart.Theme = theme;
                    themePart.Theme.Save();
                }
            }

            return file;
        }

        private static Styles StylesWithDefaults(
            RunPropertiesBaseStyle runDefaults,
            ParagraphPropertiesBaseStyle paragraphDefaults)
        {
            return new Styles(
                new DocDefaults(
                    new RunPropertiesDefault(runDefaults),
                    new ParagraphPropertiesDefault(paragraphDefaults)));
        }

        private static Style ParagraphStyle(string id, string basedOn, StyleRunProperties runProperties)
        {
            var style = new Style(new StyleName { Val = id })
            {
                Type = StyleValues.Paragraph,
                StyleId = id
            };
            if (!string.IsNullOrWhiteSpace(basedOn)) style.Append(new BasedOn { Val = basedOn });
            if (runProperties != null) style.Append(runProperties);
            return style;
        }

        private static Style CharacterStyle(string id, string basedOn, StyleRunProperties runProperties)
        {
            var style = new Style(new StyleName { Val = id })
            {
                Type = StyleValues.Character,
                StyleId = id
            };
            if (!string.IsNullOrWhiteSpace(basedOn)) style.Append(new BasedOn { Val = basedOn });
            if (runProperties != null) style.Append(runProperties);
            return style;
        }

        private static Paragraph StyledParagraph(string styleId, params Run[] runs)
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
            paragraph.Append(runs);
            return paragraph;
        }

        private static Paragraph Paragraph(params Run[] runs)
        {
            return new Paragraph(runs);
        }

        private static Run Run(string text, params DocumentFormat.OpenXml.OpenXmlElement[] properties)
        {
            var run = new Run();
            if (properties != null && properties.Length > 0)
            {
                run.Append(new RunProperties(properties));
            }
            run.Append(new Text(text));
            return run;
        }

        private static A.Theme CreateTheme(string latin, string eastAsia, string complexScript)
        {
            return new A.Theme(
                new A.ThemeElements(
                    new A.ColorScheme { Name = "Synthetic Colors" },
                    new A.FontScheme(
                        new A.MajorFont(
                            new A.LatinFont { Typeface = latin },
                            new A.EastAsianFont { Typeface = eastAsia },
                            new A.ComplexScriptFont { Typeface = complexScript }),
                        new A.MinorFont(
                            new A.LatinFont { Typeface = latin },
                            new A.EastAsianFont { Typeface = eastAsia },
                            new A.ComplexScriptFont { Typeface = complexScript }))
                    { Name = "Synthetic Fonts" },
                    new A.FormatScheme { Name = "Synthetic Format" }))
            { Name = "Synthetic Theme" };
        }

        private static DocumentFormattingSnapshot Read(SyntheticDocxFile file)
        {
            return new DocumentFormattingSnapshotReader().Read(file.FilePath);
        }

        private static RunFormattingSnapshot ReadSingleRun(SyntheticDocxFile file)
        {
            return Read(file).Paragraphs.Single().Runs.Single();
        }

        private static void AssertResolved<T>(
            FormattingValue<T> actual,
            T expected,
            FormattingSource source)
        {
            Assert.AreEqual(FormattingResolutionStatus.Resolved, actual.Status);
            Assert.AreEqual(expected, actual.Value);
            Assert.AreEqual(source, actual.Source);
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
