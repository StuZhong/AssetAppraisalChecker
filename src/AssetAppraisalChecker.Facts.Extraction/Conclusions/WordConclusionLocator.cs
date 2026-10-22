using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Facts.Extraction.Conclusions
{
    internal static class WordConclusionLocator
    {
        public static IReadOnlyList<ConclusionSection> Locate(DocumentReadResult document, WordConclusionDocumentKind kind)
        {
            WordConclusionTarget[] targets = kind == WordConclusionDocumentKind.Report
                ? new[] { WordConclusionTarget.ReportSummary, WordConclusionTarget.ReportChapterTen }
                : new[] { WordConclusionTarget.ExplanationChapterFour };
            var sections = new List<ConclusionSection>();
            foreach (WordConclusionTarget target in targets)
            {
                // A TOC entry or an inline mention is never a section heading, even if its text matches.
                DocumentElement[] headings = document.Elements.Where(element =>
                    element.ElementType == DocumentElementType.Heading && Matches(element, target)).ToArray();
                if (headings.Length == 0)
                    throw new ConclusionExtractionException(WordConclusionExtractionStatus.TargetNotFound,
                        target + "：未找到具有明确标题结构的目标结论。");
                if (headings.Length != 1)
                    throw new ConclusionExtractionException(WordConclusionExtractionStatus.AmbiguousTarget,
                        target + "：存在多个目标标题，无法唯一确定结论范围。");

                DocumentElement heading = headings[0];
                int depth = heading.HeadingPath.Count;
                if (depth == 0)
                    throw new ConclusionExtractionException(WordConclusionExtractionStatus.AmbiguousTarget,
                        target + "：缺少可靠的标题层级。");
                DocumentElement end = document.Elements.FirstOrDefault(element =>
                    element.DocumentOrderIndex > heading.DocumentOrderIndex
                    && element.ElementType == DocumentElementType.Heading && element.HeadingPath.Count <= depth);
                var elements = document.Elements.Where(element => element.DocumentOrderIndex > heading.DocumentOrderIndex
                    && (end == null || element.DocumentOrderIndex < end.DocumentOrderIndex)).ToArray();
                sections.Add(new ConclusionSection(target, heading, elements));
            }
            return sections;
        }

        private static bool Matches(DocumentElement heading, WordConclusionTarget target)
        {
            string text = Normalize(heading.Text);
            if (target == WordConclusionTarget.ReportSummary) return text == "资产评估报告摘要";
            if (target == WordConclusionTarget.ExplanationChapterFour)
                return text == "第四章评估结论及分析";
            if (text == "第十章评估结论" || text == "十、评估结论" || text == "十.评估结论") return true;
            // Word automatic numbering is not part of the extracted text. The unique
            // conclusion heading under the report body identifies the confirmed chapter.
            return text == "评估结论" && heading.HeadingPath.Any(part => Normalize(part) == "资产评估报告正文")
                && heading.HeadingPath.Count == 2;
        }

        internal static string Normalize(string text) => Regex.Replace(text ?? string.Empty, @"\s+", string.Empty,
            RegexOptions.CultureInvariant, System.TimeSpan.FromSeconds(1));
    }

    internal sealed class ConclusionSection
    {
        public ConclusionSection(WordConclusionTarget target, DocumentElement heading, IReadOnlyList<DocumentElement> elements)
        { Target = target; Heading = heading; Elements = elements; }
        public WordConclusionTarget Target { get; }
        public DocumentElement Heading { get; }
        public IReadOnlyList<DocumentElement> Elements { get; }
    }
}
