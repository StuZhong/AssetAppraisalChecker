using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Tests.QualityRegression
{
    // All sentences are synthetic, not quotations from the unavailable QC attachment.
    internal sealed class QualityRegressionCase
    {
        public QualityRegressionCase(string id, string category, string checker, string kind,
            string evidence, Func<bool, SyntheticDocxFile> create, int paragraph = 0, int occurrences = 1)
        {
            CaseId = id; ErrorCategory = category; ExpectedChecker = checker; GovernanceKind = kind;
            EvidenceText = evidence; Create = create; ParagraphIndex = paragraph; MinimumOccurrences = occurrences;
        }
        public string CaseId { get; }
        public string ErrorCategory { get; }
        public int? SourcePage => null;
        public string SourceType => "根据错误类型合成";
        public string ExpectedChecker { get; }
        public string ExpectedResult => "正例检出 " + GovernanceKind + "；合法反例无质量问题";
        public string GovernanceKind { get; }
        public string EvidenceText { get; }
        public int ParagraphIndex { get; }
        public int MinimumOccurrences { get; }
        public Func<bool, SyntheticDocxFile> Create { get; }
        public IssueCategory Category => GovernanceKind.StartsWith("Spell.", StringComparison.Ordinal) ||
            GovernanceKind.StartsWith("Grammar.", StringComparison.Ordinal) ? IssueCategory.Language :
            GovernanceKind == "FontStyleOutlier" || GovernanceKind == "ParagraphFormatOutlier"
                ? IssueCategory.Formatting : IssueCategory.Mechanical;
        public IssueAuthority Authority => Category == IssueCategory.Language ? IssueAuthority.ReviewSuggested :
            Category == IssueCategory.Formatting || GovernanceKind.StartsWith("Duplicate", StringComparison.Ordinal)
                ? IssueAuthority.HighConfidence : IssueAuthority.Confirmed;
    }

    internal static class QualityErrorRegressionCorpus
    {
        private const string Duplicate = "评估人员已对合成机器设备开展现场勘查并核对其使用状况。";
        private static readonly IReadOnlyList<QualityRegressionCase> Cases = Build().AsReadOnly();
        public static QualityRegressionCase Find(string id) => Cases.Single(item => item.CaseId == id);

        private static List<QualityRegressionCase> Build() => new List<QualityRegressionCase>
        {
            TextCase("QC-001", "连续重复词语", "DuplicateContentChecker", "DuplicateWord", "评估评估",
                "本次评估评估工作依据合成资料开展。", "本次评估工作依据合成资料开展。"),
            new QualityRegressionCase("QC-002", "相邻正文段落重复", "DuplicateContentChecker", "DuplicateParagraph",
                Duplicate, bad => SyntheticDocxFile.Create(P(Duplicate),
                    P(bad ? Duplicate : "评估人员另行核对合成房屋建筑物的维护记录。")), occurrences: 2),
            TextCase("QC-003", "重复标点", "PunctuationChecker", "RepeatedPunctuation", "。。",
                "合成资料核对工作已经完成。。", "合成资料核对工作已经完成。"),
            TextCase("QC-004-B", "中文括号不配对", "PunctuationChecker", "UnpairedChineseBracket", "（",
                "机器设备（合成样本已完成勘查。", "机器设备（合成样本）已完成勘查。"),
            TextCase("QC-004-Q", "中文引号不配对", "PunctuationChecker", "UnpairedChineseQuote", "“",
                "本次采用“合成资产组作为校检样本。", "本次采用“合成资产组”作为校检样本。"),
            TextCase("QC-004-T", "书名号不配对", "PunctuationChecker", "UnpairedBookTitle", "《",
                "本次参考《合成资料清单开展核对。", "本次参考《合成资料清单》开展核对。"),
            TextCase("QC-005", "正文异常空格", "WhitespaceAndCharacterChecker", "ConsecutiveSpaces", "  ",
                "评估人员  已完成合成资料核对。", "评估人员已完成合成资料核对。"),
            TextCase("QC-006-T", "异常制表符", "WhitespaceAndCharacterChecker", "Tab", "\t",
                "评估人员\t已完成合成资料核对。", "评估人员已完成合成资料核对。"),
            // DEL is legal XML text, unlike the XML-prohibited C0 characters.
            TextCase("QC-006-C", "异常控制字符 U+007F", "WhitespaceAndCharacterChecker", "ControlCharacter", "\u007F",
                "评估人员\u007F已完成合成资料核对。", "评估人员已完成合成资料核对。"),
            new QualityRegressionCase("QC-007", "序号跳号", "SequenceChecker", "SequenceJump", "3、",
                bad => SyntheticDocxFile.Create(P("1、合成资料核对。"), P(bad ? "3、现场勘查记录。" : "2、现场勘查记录。")), occurrences: 2),
            new QualityRegressionCase("QC-008", "序号重号", "SequenceChecker", "SequenceDuplicate", "2、",
                bad => SyntheticDocxFile.Create(P("1、合成资料核对。"), P("2、现场勘查记录。"),
                    P(bad ? "2、资产清单整理。" : "3、资产清单整理。")), paragraph: 1, occurrences: 2),
            Residue("QC-009-C", "Word 批注", "CommentResidue", "合成批注内容"),
            Residue("QC-009-I", "插入修订", "InsertedRevision", "插入修订内容"),
            Residue("QC-009-D", "删除修订", "DeletedRevision", "删除修订内容"),
            Residue("QC-009-H", "高亮残留", "Highlight", "高亮内容"),
            Residue("QC-009-S", "删除线残留", "Strikethrough", "删除线内容"),
            Format("QC-010-F", "局部中文字体异常", "FontStyleChecker", "FontStyleOutlier", "中文字体", new RunFonts { EastAsia = "宋体" }),
            Format("QC-010-S", "局部字号异常", "FontStyleChecker", "FontStyleOutlier", "字号", new FontSize { Val = "21" }),
            Format("QC-011-L", "局部行距异常", "ParagraphFormatChecker", "ParagraphFormatOutlier", "行距规则",
                new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto }),
            Format("QC-011-I", "局部缩进异常", "ParagraphFormatChecker", "ParagraphFormatOutlier", "首行缩进", new Indentation { FirstLineChars = 0 }),
            new QualityRegressionCase("QC-012", "数量与单价分母单位冲突", "UnitConsistencyChecker", "UnitConsistency", "kg",
                bad => SyntheticDocxFile.Create(UnitTable(bad)), occurrences: 2),
            TextCase("QC-013", "受控专业术语错字", "SpellChecker", "Spell.KnownTypo", "资产评估报吿",
                "本次资产评估报吿仅用于合成校检案例。", "本次资产评估报告仅用于合成校检案例。"),
            TextCase("QC-014", "受控语句残缺", "GrammarExpressionChecker", "Grammar.DanglingSentenceEnding.GE-DANGLE-004", "以及",
                "本次评估根据现场勘查以及。", "本次评估根据现场勘查以及合成资料形成结论。"),
            TextCase("QC-015", "明确句法结构重复", "GrammarExpressionChecker", "Grammar.RepeatedSyntacticSkeleton.GE-REPEAT-001", "进行",
                "评估人员对机器设备进行了现场勘查进行核实。", "评估人员对机器设备进行了现场勘查和核实。")
        };

        private static QualityRegressionCase TextCase(string id, string category, string checker, string kind,
            string evidence, string bad, string legal) => new QualityRegressionCase(id, category, checker, kind,
                evidence, positive => SyntheticDocxFile.Create(P(positive ? bad : legal)));

        private static QualityRegressionCase Residue(string id, string category, string kind, string evidence) =>
            new QualityRegressionCase(id, category, "DocumentResidueChecker", kind, evidence,
                positive => CreateResidue(kind, evidence, positive));

        private static SyntheticDocxFile CreateResidue(string kind, string text, bool positive)
        {
            if (!positive) return SyntheticDocxFile.Create(P("合成资产的现场勘查记录已经核对。"));
            Paragraph paragraph = P("合成资产的现场勘查记录已经核对。");
            if (kind == "InsertedRevision") paragraph.Append(new InsertedRun(new Run(new Text(text))) { Id = "1", Author = "合成审阅者" });
            if (kind == "DeletedRevision") paragraph.Append(new DeletedRun(new Run(new DeletedText(text))) { Id = "2", Author = "合成审阅者" });
            if (kind == "Highlight") paragraph.Append(new Run(new RunProperties(new Highlight { Val = HighlightColorValues.Yellow }), new Text(text)));
            if (kind == "Strikethrough") paragraph.Append(new Run(new RunProperties(new Strike()), new Text(text)));
            SyntheticDocxFile file = SyntheticDocxFile.Create(paragraph);
            if (kind != "CommentResidue") return file;
            using (WordprocessingDocument doc = WordprocessingDocument.Open(file.FilePath, true))
            {
                Paragraph anchor = doc.MainDocumentPart.Document.Body.Elements<Paragraph>().First();
                anchor.InsertAt(new CommentRangeStart { Id = "0" }, 0);
                anchor.Append(new CommentRangeEnd { Id = "0" }, new Run(new CommentReference { Id = "0" }));
                doc.MainDocumentPart.AddNewPart<WordprocessingCommentsPart>().Comments = new Comments(
                    new Comment(P(text)) { Id = "0", Author = "合成审阅者" });
                doc.MainDocumentPart.Document.Save();
            }
            return file;
        }

        private static QualityRegressionCase Format(string id, string category, string checker, string kind,
            string evidence, OpenXmlElement anomaly) => new QualityRegressionCase(id, category, checker, kind, evidence, positive =>
        {
            Paragraph[] paragraphs = Peers();
            if (positive)
            {
                if (kind == "FontStyleOutlier") paragraphs[2].GetFirstChild<Run>().RunProperties = new RunProperties(anomaly.CloneNode(true));
                else paragraphs[2].ParagraphProperties = new ParagraphProperties(anomaly.CloneNode(true));
            }
            return SyntheticDocxFile.CreateWithStyles(Styles(), paragraphs.Cast<OpenXmlElement>().ToArray());
        }, paragraph: 2, occurrences: 3);

        public static SyntheticDocxFile CreateNormalDocument()
        {
            var elements = new List<OpenXmlElement>
            {
                new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text("合成资产评估报告")))
            };
            elements.AddRange(Peers());
            elements.Add(new Paragraph(new Run(new Text("现场记录中的")), new Run(new RunProperties(new Bold()), new Text("重要")),
                new Run(new Text("内容仅作局部强调，English value is valid。"))));
            elements.AddRange(new[] { "委托人：合成甲公司", "项目名称：合成资产校检项目", "报告编号：合成评报字〔2026〕第009号",
                "评估基准日：2026年5月31日。", "评估价值为998.22万元。", "评估人员采用成本法核对合成机器设备。",
                "1、合成资料核对。", "2、现场勘查记录。", "3、资产清单整理。" }.Select(P));
            elements.Add(UnitTable(false));
            return SyntheticDocxFile.CreateWithStyles(Styles(), elements.ToArray());
        }

        private static Paragraph[] Peers() => new[]
        {
            P(Duplicate), P("本次现场勘查依据合成清单逐项核实资产存放地点。"),
            P("设备维护记录由合成单位提供并用于核对资产状况。"),
            P("现场核实工作覆盖合成资产组内可见的主要设备。"), P("资料收集工作仅采用合成记录并保留核对依据。")
        };

        private static IEnumerable<Style> Styles()
        {
            var normal = new Style(new StyleName { Val = "Normal" }) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
            normal.Append(new StyleRunProperties(new RunFonts { EastAsia = "仿宋_GB2312", Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                new FontSize { Val = "24" }, new Bold { Val = false }, new Italic { Val = false },
                new Underline { Val = UnderlineValues.None }, new Color { Val = "000000" }));
            normal.Append(new StyleParagraphProperties(new Justification { Val = JustificationValues.Both },
                new Indentation { Left = "0", Right = "0", FirstLineChars = 200 },
                new SpacingBetweenLines { Before = "0", After = "0", Line = "560", LineRule = LineSpacingRuleValues.Exact }));
            var heading = new Style(new StyleName { Val = "Heading 1" }) { Type = StyleValues.Paragraph, StyleId = "Heading1" };
            heading.Append(new BasedOn { Val = "Normal" },
                new StyleRunProperties(new RunFonts { EastAsia = "黑体", Ascii = "Arial", HighAnsi = "Arial" }, new FontSize { Val = "32" }, new Bold()),
                new StyleParagraphProperties(new Justification { Val = JustificationValues.Center }, new Indentation { FirstLineChars = 0 }));
            return new[] { normal, heading };
        }

        private static Table UnitTable(bool conflict) => new Table(
            new TableRow(new[] { "名称", "数量", "单位", "单价", "金额" }.Select(text => new TableCell(P(text)))),
            new TableRow(new[] { "合成材料", "1.00", "kg", conflict ? "30.00 元/台" : "30.00 元/kg", "30.00元" }.Select(text => new TableCell(P(text)))));

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
