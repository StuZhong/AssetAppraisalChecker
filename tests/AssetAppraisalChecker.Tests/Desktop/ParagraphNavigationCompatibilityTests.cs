using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class ParagraphNavigationCompatibilityTests
    {
        [TestMethod]
        public void VerifiedSectionMarker_ConfirmsSameParagraphButNeverExactCharacterRange()
        {
            var paragraph = new Paragraph(new ParagraphProperties(new SectionProperties()),
                new Run(new Text("合成声明正文。")));
            using (var file = SyntheticDocxFile.Create(paragraph))
            {
                var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, SourceNavigationTests.At(source, 0, "声明"));
                string actual = "合成声明正文。\f";
                Assert.IsTrue(WordTextMatcher.MatchesSectionBreakParagraph(target, actual, RangeXml(paragraph)));
                Assert.AreNotEqual(target.ContainerText, actual); // Adapter must select only the verified paragraph.
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, actual));
                Assert.AreEqual("合成声明正文。\f", WordTextMatcher.ContainerText(actual + "\r", false));
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public void SectionMarker_RequiresOriginalSectionAndMatchingSingleEditorParagraph()
        {
            foreach (bool section in new[] { false, true })
            {
                var paragraph = new Paragraph(new Run(new Text("合成正文。")));
                if (section) paragraph.PrependChild(new ParagraphProperties(new SectionProperties()));
                using (var file = SyntheticDocxFile.Create(paragraph))
                {
                    var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                    var target = new SourceLocationResolver().Resolve(source, SourceNavigationTests.At(source, 0, "正文"));
                    string editorXml = RangeXml(new Paragraph(new ParagraphProperties(new SectionProperties()),
                        new Run(new Text("合成正文。"))));
                    Assert.AreEqual(section, WordTextMatcher.MatchesSectionBreakParagraph(target, "合成正文。\f", editorXml));
                    Assert.IsFalse(WordTextMatcher.MatchesSectionBreakParagraph(target, "合成正文。\f", RangeXml(new Paragraph(new Run(new Text("合成正文。"))))));
                    Assert.IsFalse(WordTextMatcher.MatchesSectionBreakParagraph(target, "合成正文。\f", editorXml.Replace("合成正文。", "其他正文。")));
                    Assert.IsFalse(WordTextMatcher.MatchesSectionBreakParagraph(target, "合成正文。 \f", editorXml));
                    Assert.IsFalse(WordTextMatcher.MatchesSectionBreakParagraph(target, "合成\f正文。", editorXml));
                    Assert.IsFalse(WordTextMatcher.MatchesSectionBreakParagraph(target, "合成正文。\f\f", editorXml));
                    Assert.IsFalse(WordTextMatcher.MatchesSectionBreakParagraph(target, "合成正文。\f",
                        editorXml.Replace("</w:body>", "<w:p/></w:body>")));
                }
            }
        }

        [TestMethod]
        public void SectionMarkerCrossCheck_DoesNotBypassCountNeighboursOrAmbiguity()
        {
            // The adapter replaces only the XML-confirmed target for this comparison, not all display markers.
            Assert.IsTrue(WordTextMatcher.MatchesParagraphAt(new[] { "", "前文", "目标", "后文" },
                new[] { "\f", "前文", "目标", "后文" }, 2));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "", "前文", "目标", "后文" },
                new[] { "\f", "变化", "目标", "后文" }, 2));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "", "前文", "目标", "后文", "目标" },
                new[] { "\f", "前文", "目标", "后文", "目标" }, 2));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "前文", "目标", "后文" },
                new[] { "前文", "目标", "后文", "新增" }, 1));
        }

        private static string RangeXml(Paragraph paragraph) =>
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
            paragraph.OuterXml + "</w:body></w:document>";

        [TestMethod]
        public void UnrelatedWpsSectionAndDrawingMarkers_DoNotBlockUniqueVerifiedParagraph()
        {
            var source = new[] { "", "前文。", "合成目录项\t1", "后文。", "末尾。" };
            foreach (string marker in new[] { "\f", "/" })
            {
                var actual = new[] { marker, "前文。", "合成目录项\t1", "后文。", "末尾。\f" };
                Assert.IsFalse(WordTextMatcher.MatchesSequence(source, actual));
                Assert.IsTrue(WordTextMatcher.MatchesParagraphAt(source, actual, 2));
            }
        }

        [TestMethod]
        public void DifferentParagraphCount_RefusesEvenUniqueText()
        {
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "前文", "目标", "后文" },
                new[] { "前文", "目标", "后文", "新增" }, 1));
        }

        [TestMethod]
        public void ChangedNeighbour_RefusesFallback()
        {
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "", "前文", "目标", "后文" },
                new[] { "\f", "前文", "目标", "不同后文" }, 2));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "", "前文", "目标", "后文" },
                new[] { "\f", "不同前文", "目标", "后文" }, 2));
        }

        [TestMethod]
        public void RepeatedParagraph_RefusesLocalFallbackButPreservesStrictSequence()
        {
            var source = new[] { "", "前文", "重复目标", "后文", "重复目标" };
            Assert.IsTrue(WordTextMatcher.MatchesParagraphAt(source, source, 2));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(source,
                new[] { "\f", "前文", "重复目标", "后文", "重复目标" }, 2));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "", "前文", "目标", "后文", "其他" },
                new[] { "\f", "前文", "目标", "后文", "目标" }, 2));
        }

        [TestMethod]
        public void UniqueTextAtAnotherIndex_IsNeverUsedAsFirstSearchMatch()
        {
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "前文", "目标", "后文", "" },
                new[] { "目标", "前文", "后文", "\f" }, 1));
        }

        [TestMethod]
        public void WhitespaceOrTabDifferenceInTarget_IsNotNormalizedAway()
        {
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "前文", "目标  \t", "后文" },
                new[] { "前文", "目标 \t", "后文" }, 1));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "前文", "目标\t", "后文" },
                new[] { "前文", "目标 ", "后文" }, 1));
        }

        [TestMethod]
        public void InvalidIndexOrEmptyFallbackTarget_IsUnavailable()
        {
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(null, new[] { "目标" }, 0));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "目标" }, null, 0));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "目标" }, new[] { "目标" }, -1));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "目标" }, new[] { "目标" }, 1));
            Assert.IsFalse(WordTextMatcher.MatchesParagraphAt(new[] { "前文", "", "后文", "" },
                new[] { "前文", "", "后文", "\f" }, 1));
        }

        [TestMethod]
        public void VisibleFieldResult_ConfirmsOnlyParagraphAndNeverFieldCharacterOffsets()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(
                new SimpleField(new Run(new Text("合成目录项"), new TabChar(), new Text("1"))) { Instruction = "PAGEREF synthetic" })))
            {
                var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source,
                    SourceNavigationTests.At(source, 0, "\t", "元素内字符偏移（从 0 开始）=5"));
                Assert.IsNotNull(target);
                Assert.IsTrue(target.DisplaySensitive);
                Assert.IsTrue(WordTextMatcher.MatchesVisibleParagraph(target, "合成目录项\t1"));
                Assert.IsFalse(WordTextMatcher.MatchesVisibleParagraph(target, "合成目录项\t\t1"));
                Assert.IsNull(target.Offset);
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, "\t"));
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public void DeletedText_NotInVisibleParagraph_StillRequiresXmlVerification()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("当前正文")),
                new DeletedRun(new Run(new DeletedText("删除片段"))) { Id = "1", Author = "合成作者" })))
            {
                var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, SourceNavigationTests.At(source, 0, "删除片段"));
                Assert.IsNotNull(target);
                Assert.IsTrue(target.DisplaySensitive);
                Assert.IsFalse(WordTextMatcher.MatchesVisibleParagraph(target, "当前正文"));
                Assert.IsNull(target.Offset);
                Assert.IsTrue(source.IsUnchanged());
            }
        }
    }
}
