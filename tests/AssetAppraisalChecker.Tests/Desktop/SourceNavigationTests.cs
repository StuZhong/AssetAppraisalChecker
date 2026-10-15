using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class SourceNavigationTests
    {
        [TestMethod]
        public async Task DocumentA_UsesCheckedIdentityAndStaAdapter()
        {
            using (var file = SyntheticDocxFile.Create(P("合成评估对象。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var adapter = new RecordingWordAdapter();
                var result = await new SourceNavigationService(adapter).NavigateAsync(source, At(source, 0, "评估对象"));
                Assert.AreEqual(SourceNavigationStatus.Exact, result.Status);
                Assert.AreEqual(source.FullPath, adapter.Last.Source.FullPath);
                Assert.AreEqual(ApartmentState.STA, adapter.Apartment);
                Assert.AreEqual(1, adapter.Calls);
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public async Task DocumentB_UsesOccurrenceSourceNotArrayOrder()
        {
            using (var a = SyntheticDocxFile.Create(P("合成 A。")))
            using (var b = SyntheticDocxFile.Create(P("合成 B。")))
            {
                var sources = new[] { Capture(b.FilePath, "文档 B"), Capture(a.FilePath, "文档 A") };
                var occurrence = At(sources[0], 0, "合成 B");
                var adapter = new RecordingWordAdapter();
                var source = SourceNavigationService.FindSource(sources, occurrence);
                await new SourceNavigationService(adapter).NavigateAsync(source, occurrence);
                Assert.AreEqual(Path.GetFullPath(b.FilePath), adapter.Last.Source.FullPath);
            }
        }

        [TestMethod]
        public void ExchangingAB_KeepsActualSourceCorrect()
        {
            using (var a = SyntheticDocxFile.Create(P("对象甲。")))
            using (var b = SyntheticDocxFile.Create(P("对象乙。")))
            {
                var original = new[] { Capture(a.FilePath, "文档 A"), Capture(b.FilePath, "文档 B") };
                var swapped = new[] { Capture(b.FilePath, "文档 A"), Capture(a.FilePath, "文档 B") };
                Assert.AreEqual(a.FilePath, SourceNavigationService.FindSource(original, At(original[0], 0, "对象甲")).FullPath);
                Assert.AreEqual(a.FilePath, SourceNavigationService.FindSource(swapped, At(swapped[1], 0, "对象甲")).FullPath);
            }
        }

        [TestMethod]
        public void SameBasenameDifferentDirectories_IsNotSameWordDocument()
        {
            Assert.IsFalse(WordTextMatcher.SameFullPath(@"C:\合成A\报告.docx", @"C:\合成B\报告.docx"));
            Assert.IsTrue(WordTextMatcher.SameFullPath(@"C:\合成A\报告.docx", @"c:\合成A\报告.docx"));
        }

        [TestMethod]
        public async Task IncorrectSourceLabel_RefusesBeforeAdapter()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var a = Capture(file.FilePath, "文档 A");
                var b = Capture(file.FilePath, "文档 B");
                var adapter = new RecordingWordAdapter();
                Assert.AreEqual(SourceNavigationStatus.Unavailable,
                    (await new SourceNavigationService(adapter).NavigateAsync(a, At(b, 0, "合成正文"))).Status);
                Assert.AreEqual(0, adapter.Calls);
            }
        }

        [TestMethod]
        public void UnlabelledDualOccurrence_DoesNotGuessA()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var a = Capture(file.FilePath, "文档 A");
                var b = Capture(file.FilePath, "文档 B");
                var occurrence = At(a, 0, "合成正文", label: string.Empty);
                Assert.IsNull(SourceNavigationService.FindSource(new[] { a, b }, occurrence));
                Assert.AreSame(a, SourceNavigationService.FindSource(new[] { a }, occurrence));
            }
        }

        [TestMethod]
        public void RepeatedSentence_UsesSecondStructuralParagraphNotFirstFind()
        {
            using (var file = SyntheticDocxFile.Create(P("采用市场法。"), P("采用市场法。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 1, "市场法"));
                Assert.AreEqual(1, target.Location.ParagraphIndex);
                Assert.AreEqual("市场法", target.SpanText);
                Assert.IsTrue(WordTextMatcher.MatchesSequence(new[] { "采用市场法。", "采用市场法。" },
                    new[] { "采用市场法。", "采用市场法。" }));
                Assert.IsFalse(WordTextMatcher.MatchesSequence(new[] { "采用市场法。", "采用市场法。" }, new[] { "采用市场法。" }));
            }
        }

        [TestMethod]
        public void RepeatedFragmentInsideParagraph_NoOffsetMeansParagraphOnly()
        {
            using (var file = SyntheticDocxFile.Create(P("市场法与市场法。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 0, "市场法"));
                Assert.IsNull(target.Offset);
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, "市场法"));
            }
        }

        [TestMethod]
        public void RepeatedFragment_WithVerifiedOffsetChoosesSecondSpan()
        {
            using (var file = SyntheticDocxFile.Create(P("市场法与市场法。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 0, "市场法", "原文范围：Start=4，Length=3"));
                Assert.AreEqual(4, target.Offset);
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "市场法"));
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, "法与市"));
            }
        }

        [TestMethod]
        public void UnicodePrefix_MustAgreeBeforeTrustingRepeatedSpanOffset()
        {
            using (var file = SyntheticDocxFile.Create(P("😀金额金额。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 0, "金额", "原文范围：Start=4，Length=2"));
                Assert.IsTrue(WordTextMatcher.MatchesPrefix(target, "😀金额"));
                Assert.IsFalse(WordTextMatcher.MatchesPrefix(target, "😀金"));
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "金额"));
            }
        }

        [TestMethod]
        public void OrdinaryTable_MapsPhysicalRowCellAndMultiParagraphText()
        {
            var table = new Table(new TableRow(new TableCell(P("甲")), new TableCell(P("首段"), P("评估价值。"))));
            using (var file = SyntheticDocxFile.Create(P("前文。"), table))
            {
                var source = Capture(file.FilePath, "文档 A");
                var element = source.Document.Elements.Single(e => e.Location.CellIndex == 1);
                var target = new SourceLocationResolver().Resolve(source, Occurrence(element.Location, "评估价值", "文档 A"));
                Assert.AreEqual(0, target.Location.TableIndex);
                Assert.AreEqual(0, target.Location.RowIndex);
                Assert.AreEqual(1, target.Location.CellIndex);
                Assert.AreEqual(2, target.RowCellCounts[0]);
                Assert.AreEqual("首段\n评估价值。", WordTextMatcher.ContainerText("首段\r评估价值。\r\a", true));
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "评估价值"));
            }
        }

        [TestMethod]
        public void MergedCell_IsNotMappedAsAnOrdinaryLogicalColumn()
        {
            var table = new Table(new TableRow(new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("合并内容"))));
            using (var file = SyntheticDocxFile.Create(table))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source,
                    Occurrence(source.Document.Elements[0].Location, "合并内容", "文档 A"));
                Assert.IsNotNull(target);
                Assert.IsTrue(target.TargetRowHasMerge);
                Assert.IsFalse(target.TargetCellOneToOne);
            }
        }

        [TestMethod]
        public void FormattingLocation_MapsCellParagraphWithoutClaimingExactText()
        {
            var table = new Table(new TableRow(new TableCell(P("首段。"), P("格式需复核的第二段。")),
                new TableCell(P("相邻单元格。"))));
            using (var file = SyntheticDocxFile.Create(table))
            {
                var source = Capture(file.FilePath, "文档 B");
                var cell = source.Document.Elements.Single(e => e.Location.CellIndex == 0);
                Assert.IsNull(cell.Location.ParagraphIndex);
                var location = new DocumentLocation(DocumentPart.MainBody, 1, 0, 0, 0, new string[0]);
                var target = new SourceLocationResolver().Resolve(source,
                    Occurrence(location, "格式需复核的第二段。", "文档 B"));
                Assert.IsNotNull(target);
                Assert.AreEqual(2, target.CellParagraphTexts.Count);
                Assert.AreEqual("格式需复核的第二段。", target.CellParagraphTexts[1]);
                Assert.IsNull(target.Offset);
                Assert.IsTrue(target.TargetCellOneToOne);
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public void EmptyFormattingParagraph_CanResolveCellButNotInventCharacterRange()
        {
            var table = new Table(new TableRow(new TableCell(P("")), new TableCell(P("相邻证据。"))));
            using (var file = SyntheticDocxFile.Create(table))
            {
                var source = Capture(file.FilePath, "文档 A");
                var location = new DocumentLocation(DocumentPart.MainBody, 0, 0, 0, 0, new string[0]);
                var target = new SourceLocationResolver().Resolve(source, Occurrence(location, "", "文档 A"));
                Assert.IsNotNull(target);
                Assert.AreEqual(string.Empty, target.ContainerText);
                Assert.IsNull(target.Offset);
                Assert.AreEqual(1, target.CellParagraphTexts.Count);
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public void MergedNeighbor_DoesNotBlockVerifiedUnmergedTargetCell()
        {
            var table = new Table(new TableRow(new TableCell(P("左侧证据")), new TableCell(P("")),
                new TableCell(new TableCellProperties(new VerticalMerge { Val = MergedCellValues.Continue }), P(""))));
            using (var file = SyntheticDocxFile.Create(table))
            {
                var source = Capture(file.FilePath, "文档 A");
                var location = new DocumentLocation(DocumentPart.MainBody, 0, 0, 0, 1, new string[0]);
                var target = new SourceLocationResolver().Resolve(source, Occurrence(location, "", "文档 A"));
                Assert.IsNotNull(target);
                Assert.IsTrue(target.TargetRowHasMerge);
                Assert.IsTrue(target.TargetCellOneToOne);
                Assert.IsNull(target.Offset);
            }
        }

        [TestMethod]
        public void WrongCellParagraphOrRawText_StillFailsClosed()
        {
            var table = new Table(new TableRow(new TableCell(P("唯一合成段落。"))));
            using (var file = SyntheticDocxFile.Create(table))
            {
                var source = Capture(file.FilePath, "文档 A");
                var resolver = new SourceLocationResolver();
                Assert.IsNull(resolver.Resolve(source, Occurrence(
                    new DocumentLocation(DocumentPart.MainBody, 1, 0, 0, 0, new string[0]),
                    "唯一合成段落。", "文档 A")));
                Assert.IsNull(resolver.Resolve(source, Occurrence(
                    new DocumentLocation(DocumentPart.MainBody, 0, 0, 0, 0, new string[0]),
                    "另一个段落。", "文档 A")));
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public void WpsCellSelection_AllowsOnlyMissingStructuralCellMarker()
        {
            Assert.IsTrue(WordTextMatcher.MatchesSelectedRangeText("合成\r\a", "合成\r", true));
            Assert.IsFalse(WordTextMatcher.MatchesSelectedRangeText("合成\r\a", "其他\r", true));
            Assert.IsFalse(WordTextMatcher.MatchesSelectedRangeText("合成\r\a", "合成\r", false));
            Assert.IsTrue(WordTextMatcher.MatchesRowRange(new[] { "甲", "", "乙" }, "甲\r\a\r\a乙\r\a"));
            Assert.IsTrue(WordTextMatcher.MatchesRowRange(new[] { "甲", "", "乙" }, "甲\r\a\r\a乙\r\a\r\a"));
            Assert.IsFalse(WordTextMatcher.MatchesRowRange(new[] { "甲", "", "乙" }, "甲\r\a\r\a乙\r\aX\r\a"));
            Assert.IsFalse(WordTextMatcher.MatchesRowRange(new[] { "甲", "", "乙" }, "乙\r\a\r\a甲\r\a"));
            Assert.IsTrue(WordTextMatcher.MatchesSelectedRowText("甲\r\a乙\r\a\r\a", "甲\r乙\r\r"));
            Assert.IsFalse(WordTextMatcher.MatchesSelectedRowText("甲\r\a乙\r\a\r\a", "甲乙\r\r"));
        }

        [TestMethod]
        public void NestedTable_FailsClosed()
        {
            var table = new Table(new TableRow(new TableCell(P("外层"), new Table(new TableRow(new TableCell(P("内层")))))));
            using (var file = SyntheticDocxFile.Create(table))
            {
                var source = Capture(file.FilePath, "文档 A");
                Assert.IsNull(new SourceLocationResolver().Resolve(source, Occurrence(source.Document.Elements[0].Location, "外层", "文档 A")));
            }
        }

        [TestMethod]
        public async Task ProductionWhitespaceEvidence_SelectsSpacesAndPreservesFileHash()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文  存在间隔。")))
            {
                string before = CheckedDocumentSource.ComputeSha256(file.FilePath);
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var issue = result.Issues.First(i => i.Occurrences.Any(o => o.Evidence.Any(e => e.Contains("字符偏移="))));
                var occurrence = issue.Occurrences[0];
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[0], occurrence);
                Assert.AreEqual("  ", target.SpanText);
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "  "));
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, " "));
                Assert.AreEqual(before, CheckedDocumentSource.ComputeSha256(file.FilePath));
            }
        }

        [TestMethod]
        public async Task ProductionTabEvidence_SelectsActualTabNotXmlRunOffset()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("合成正文"), new TabChar(), new Text("间隔。")))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var occurrence = result.Issues.Single(i => i.GovernanceKind == "Tab").Occurrences[0];
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[0], occurrence);
                Assert.IsNotNull(target);
                Assert.AreEqual(4, target.Offset);
                Assert.AreEqual("\t", target.SpanText);
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "\t"));
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, " "));
                Assert.IsTrue(result.SourceDocuments[0].IsUnchanged());
            }
        }

        [TestMethod]
        public async Task ProductionPunctuationEvidence_SelectsVerifiedRepeatedMarks()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文，，存在重复标点。")))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var occurrence = result.Issues.Single(i => i.GovernanceKind == "RepeatedPunctuation").Occurrences[0];
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[0], occurrence);
                Assert.AreEqual("，，", target.SpanText);
                Assert.AreEqual(4, target.Offset);
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "，，"));
                Assert.IsTrue(result.SourceDocuments[0].IsUnchanged());
            }
        }

        [TestMethod]
        public async Task ProductionUnpairedSymbol_SeparateLengthSelectsOneVerifiedCharacterInCorrectCell()
        {
            foreach (string text in new[] { "合成《引用名称", "合成表格内容（" })
            using (var file = SyntheticDocxFile.Create(new Table(new TableRow(
                new TableCell(P("邻列证据")), new TableCell(P(text))))))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var issue = result.Issues.Single(i => i.GovernanceKind ==
                    (text.Contains("《") ? "UnpairedBookTitle" : "UnpairedChineseBracket"));
                var occurrence = issue.Occurrences[0];
                Assert.IsTrue(occurrence.Evidence.Contains("长度=1"));
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[0], occurrence);
                Assert.IsNotNull(target);
                Assert.AreEqual(0, target.Location.TableIndex);
                Assert.AreEqual(1, target.Location.CellIndex);
                Assert.AreEqual(1, target.Length);
                Assert.AreEqual(text.Contains("《") ? "《" : "（", target.SpanText);
                Assert.IsTrue(WordTextMatcher.MatchesPrefix(target, text.Substring(0, target.Offset.Value)));
                Assert.IsTrue(result.SourceDocuments[0].IsUnchanged());
            }
        }

        [TestMethod]
        public void SeparateLength_RejectsConflictingMalformedDuplicateAndOutOfBoundsEvidence()
        {
            using (var file = SyntheticDocxFile.Create(P("合成（正文")))
            {
                var source = Capture(file.FilePath, "文档 A");
                foreach (var evidence in new[] {
                    new[] { "字符偏移=2；长度=2", "长度=1" },
                    new[] { "字符偏移=2", "长度=1", "长度=1" },
                    new[] { "字符偏移=2", "长度=未知" },
                    new[] { "字符偏移=2", "长度=0" },
                    new[] { "字符偏移=2", "长度=999999999999999" },
                    new[] { "字符偏移=5", "长度=1" } })
                {
                    var occurrence = new ReportCheckOccurrence("合成（正文", "", source.Document.Elements[0].Location,
                        "", "", "", evidence, null, "", false, documentLabel: source.Label);
                    Assert.IsNull(new SourceLocationResolver().Resolve(source, occurrence));
                }
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public async Task ProductionControlCharacter_DroppedWordCharacterCannotClaimExact()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文\u007f片段。")))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var occurrence = result.Issues.Single(i => i.GovernanceKind == "ControlCharacter").Occurrences[0];
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[0], occurrence);
                Assert.IsNotNull(target);
                Assert.IsTrue(WordTextMatcher.MatchesSpan(target, "合成正文\u007f片段。"));
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, "合成正文片段。"));
                Assert.IsTrue(result.SourceDocuments[0].IsUnchanged());
            }
        }

        [TestMethod]
        public async Task ProductionDeletedRevision_VerifiesRealNodeButNeverClaimsExact()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("现有正文。")),
                new DeletedRun(new Run(new DeletedText("合成删除片段"))) { Id = "1", Author = "合成作者" })))
            {
                var result = await new ReportCheckService().CheckAsync(file.FilePath);
                var occurrence = result.Issues.Single(i => i.GovernanceKind == "DeletedRevision").Occurrences[0];
                var target = new SourceLocationResolver().Resolve(result.SourceDocuments[0], occurrence);
                Assert.IsNotNull(target);
                Assert.IsTrue(target.DisplaySensitive);
                Assert.IsNull(target.Offset);
                Assert.IsFalse(WordTextMatcher.MatchesSpan(target, occurrence.RawText));
                Assert.AreEqual(0, target.Location.ParagraphIndex);
                Assert.IsTrue(result.SourceDocuments[0].IsUnchanged());
            }
        }

        [TestMethod]
        public void HiddenText_DoesNotClaimExact()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new RunProperties(new Vanish()), new Text("隐藏合成文字")))))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 0, "隐藏合成文字"));
                Assert.IsTrue(target.DisplaySensitive);
                Assert.IsNull(target.Offset);
            }
        }

        [TestMethod]
        public void DeletedRevisionAcrossRuns_StillVerifiesContainingParagraph()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("现有正文。")),
                new DeletedRun(new Run(new DeletedText("合成")), new Run(new DeletedText("删除片段"))) { Id = "1", Author = "合成作者" })))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 0, "合成删除片段"));
                Assert.IsNotNull(target);
                Assert.IsTrue(target.DisplaySensitive);
                Assert.IsNull(target.Offset);
            }
        }

        [TestMethod]
        public void FieldResult_DoesNotUseFieldCodeAsVisibleOffset()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new SimpleField(new Run(new Text("合成字段"))) { Instruction = "DATE" })))
            {
                var source = Capture(file.FilePath, "文档 A");
                var target = new SourceLocationResolver().Resolve(source, At(source, 0, "合成字段"));
                Assert.IsTrue(target.DisplaySensitive);
                Assert.IsNull(target.Offset);
            }
        }

        [TestMethod]
        public async Task ChangedDiskFile_RequestsRecheckBeforeWord()
        {
            using (var file = SyntheticDocxFile.Create(P("合成旧内容。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                using (var package = WordprocessingDocument.Open(file.FilePath, true))
                    package.MainDocumentPart.Document.Body.Append(P("新增合成内容。"));
                var adapter = new RecordingWordAdapter();
                var result = await new SourceNavigationService(adapter).NavigateAsync(source, At(source, 0, "合成旧内容"));
                Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
                StringAssert.Contains(result.Message, "重新校检");
                Assert.AreEqual(0, adapter.Calls);
            }
        }

        [TestMethod]
        public async Task MissingDiskFile_GivesSafeMessage()
        {
            var file = SyntheticDocxFile.Create(P("合成正文。"));
            var source = Capture(file.FilePath, "文档 A");
            file.Dispose();
            var adapter = new RecordingWordAdapter();
            var result = await new SourceNavigationService(adapter).NavigateAsync(source, At(source, 0, "合成正文"));
            Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
            StringAssert.Contains(result.Message, "不存在");
            Assert.AreEqual(0, adapter.Calls);
        }

        [TestMethod]
        public async Task WordUnavailableAdapter_ProvidesFallbackWithoutChangingSource()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var adapter = new RecordingWordAdapter { Handler = t => SourceNavigationResult.Unavailable("无法使用 Word 精确定位功能。") };
                Assert.AreEqual(SourceNavigationStatus.Unavailable,
                    (await new SourceNavigationService(adapter).NavigateAsync(source, At(source, 0, "合成正文"))).Status);
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public async Task WordComRegistrationAbsent_RealAdapterReturnsHonestFallback()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                // Inject only registration discovery; execute the production adapter's unavailable branch.
                var result = await new SourceNavigationService(new WordNavigationAdapter(() => null))
                    .NavigateAsync(source, At(source, 0, "合成正文"));
                Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
                StringAssert.Contains(result.Message, "无法使用 Word 精确定位功能");
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public async Task NoVerifiedText_RefusesAdapter()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var adapter = new RecordingWordAdapter();
                var result = await new SourceNavigationService(adapter).NavigateAsync(source, At(source, 0, "并不存在的原文"));
                Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
                Assert.AreEqual(0, adapter.Calls);
            }
        }

        [TestMethod]
        public void InvalidOffset_DoesNotFallbackToFirstOccurrence()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                Assert.IsNull(new SourceLocationResolver().Resolve(source, At(source, 0, "正文", "原文范围：Start=99，Length=2")));
            }
        }

        [TestMethod]
        public void UnsupportedStoryAndUnknownStructure_DoNotPretendBody()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                Assert.IsNull(new SourceLocationResolver().Resolve(source,
                    Occurrence(new DocumentLocation(DocumentPart.MainBody, null, null, null, null, null), "合成正文", "文档 A")));
            }
        }

        [TestMethod]
        public async Task AdapterException_DiagnosticDoesNotLeakDocumentContent()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            {
                var source = Capture(file.FilePath, "文档 A");
                var adapter = new RecordingWordAdapter { Handler = t => throw new InvalidOperationException("不应输出的模拟敏感内容") };
                var result = await new SourceNavigationService(adapter).NavigateAsync(source, At(source, 0, "合成正文"));
                Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
                Assert.IsFalse(result.Message.Contains("模拟敏感内容"));
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public async Task SlowComCall_DoesNotBlockUiOrStartAnotherWorker()
        {
            using (var file = SyntheticDocxFile.Create(P("合成正文。")))
            using (var release = new ManualResetEventSlim())
            using (var entered = new ManualResetEventSlim())
            using (var finished = new ManualResetEventSlim())
            {
                var source = Capture(file.FilePath, "文档 A");
                var adapter = new RecordingWordAdapter
                {
                    Handler = t =>
                    {
                        entered.Set();
                        release.Wait(TimeSpan.FromSeconds(5));
                        return SourceNavigationResult.Unavailable("已取消");
                    },
                    Finished = finished
                };
                var service = new SourceNavigationService(adapter, TimeSpan.FromMilliseconds(500));
                try
                {
                    var result = await service.NavigateAsync(source, At(source, 0, "合成正文"));
                    Assert.IsTrue(entered.IsSet);
                    StringAssert.Contains(result.Message, "限定时间");
                    var second = await service.NavigateAsync(source, At(source, 0, "合成正文"));
                    Assert.AreEqual(SourceNavigationStatus.Unavailable, second.Status);
                    Assert.AreEqual(1, adapter.Calls);
                }
                finally
                {
                    release.Set();
                    Assert.IsTrue(finished.Wait(TimeSpan.FromSeconds(5)));
                    // The worker still performs the post-navigation SHA check after the fake returns.
                    Assert.IsTrue(SpinWait.SpinUntil(() => !service.IsNavigationPending, TimeSpan.FromSeconds(5)));
                }
            }
        }

        [TestMethod]
        public void WordStructuralTerminators_DoNotTrimRealSpacesOrTabs()
        {
            Assert.AreEqual("  合成\t正文  ", WordTextMatcher.ContainerText("  合成\t正文  \r", false));
            Assert.AreEqual("甲\n乙", WordTextMatcher.ContainerText("甲\v乙\r", false));
            Assert.IsFalse(WordTextMatcher.MatchesSequence(new[] { "合成  正文" }, new[] { "合成 正文" }));
        }

        [TestMethod]
        public async Task RealCheck_CapturesBothHashesAndNavigationLeavesBothUnchanged()
        {
            using (var a = SyntheticDocxFile.Create(P("评估基准日为2026年6月15日。")))
            using (var b = SyntheticDocxFile.Create(P("评估基准日为2026年5月31日。")))
            {
                var check = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(check.IsSuccess);
                Assert.AreEqual(2, check.SourceDocuments.Count);
                var issue = check.Issues.First(i => i.Occurrences.Any(o => o.DocumentLabel == "文档 B"));
                var occurrence = issue.Occurrences.First(o => o.DocumentLabel == "文档 B" && o.RawText.Contains("2026"));
                var source = SourceNavigationService.FindSource(check.SourceDocuments, occurrence);
                var result = await new SourceNavigationService(new RecordingWordAdapter()).NavigateAsync(source, occurrence);
                Assert.AreNotEqual(SourceNavigationStatus.Unavailable, result.Status);
                Assert.IsTrue(check.SourceDocuments.All(s => s.IsUnchanged()));
                Assert.AreEqual(b.FilePath, source.FullPath);
            }
        }

        internal static CheckedDocumentSource Capture(string path, string label) =>
            new CheckedDocumentSource(label, path, CheckedDocumentSource.ComputeSha256(path), new DocxDocumentReader().Read(path));

        internal static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        internal static ReportCheckOccurrence At(CheckedDocumentSource source, int paragraph, string raw, string evidence = null, string label = null) =>
            Occurrence(source.Document.Elements.Single(e => e.Location.ParagraphIndex == paragraph).Location,
                raw, label ?? source.Label, evidence);

        internal static ReportCheckOccurrence Occurrence(DocumentLocation location, string raw, string label, string evidence = null) =>
            new ReportCheckOccurrence(raw, raw, location, "正文结构位置（非页码）", "正文", string.Empty,
                evidence == null ? new string[0] : new[] { evidence }, new string[0], string.Empty, false, documentLabel: label);

        internal sealed class RecordingWordAdapter : IWordNavigationAdapter
        {
            public SourceNavigationTarget Last { get; private set; }
            public int Calls { get; private set; }
            public ApartmentState Apartment { get; private set; }
            public Func<SourceNavigationTarget, SourceNavigationResult> Handler { get; set; }
            public ManualResetEventSlim Finished { get; set; }
            public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken cancellationToken)
            {
                Last = target;
                Calls++;
                Apartment = Thread.CurrentThread.GetApartmentState();
                try
                {
                    return Handler?.Invoke(target) ?? new SourceNavigationResult(
                        target.Offset.HasValue ? SourceNavigationStatus.Exact : SourceNavigationStatus.ParagraphOnly, "模拟适配器结果（非真实 Word）。");
                }
                finally { Finished?.Set(); }
            }
        }
    }
}
