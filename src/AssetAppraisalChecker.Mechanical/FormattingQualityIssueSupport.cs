using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Mechanical.Formatting;

namespace AssetAppraisalChecker.Mechanical
{
    internal static class FormattingQualityIssueSupport
    {
        public static MechanicalIssue Create(
            MechanicalCheckContext context,
            MechanicalIssueKind kind,
            string fieldName,
            string summary,
            string reason,
            FormattingPeerParagraph current,
            IEnumerable<FormattingDifference> differences)
        {
            List<FormattingDifference> values = differences.Where(item => item != null).ToList();
            var occurrences = new List<MechanicalIssueOccurrence>
            {
                Occurrence(
                    context,
                    current,
                    new[]
                    {
                        "当前格式：" + string.Join("；", values.Select(item =>
                            item.FieldName + "=" + item.CurrentValue)),
                        "附近同类内容主要格式：" + string.Join("；", values.Select(item =>
                            item.FieldName + "=" + item.DominantValue))
                    })
            };

            IEnumerable<FormattingPeerParagraph> evidencePeers = values
                .SelectMany(item => item.SupportingPeers)
                .GroupBy(item => item.Paragraph.Location.ParagraphIndex)
                .Select(group => group.First())
                .OrderBy(item => Math.Abs(
                    item.Paragraph.Location.ParagraphIndex -
                    current.Paragraph.Location.ParagraphIndex))
                .Take(3);
            foreach (FormattingPeerParagraph peer in evidencePeers)
            {
                occurrences.Add(Occurrence(
                    context,
                    peer,
                    new[] { "附近同类内容证据；该位置支持主要格式模式。" }));
            }

            return new MechanicalIssue(
                kind.ToString(),
                kind,
                MechanicalIssueSeverity.Normal,
                fieldName,
                summary,
                reason,
                "请结合相邻同类段落核对该处格式；程序不会自动修改文档。",
                occurrences);
        }

        private static MechanicalIssueOccurrence Occurrence(
            MechanicalCheckContext context,
            FormattingPeerParagraph item,
            IEnumerable<string> evidence)
        {
            FormattingParagraphLocation location = item.Paragraph.Location;
            var documentLocation = new DocumentLocation(
                DocumentPart.MainBody,
                location.IsInTable ? location.CellParagraphIndex : location.BodyParagraphIndex,
                location.TableIndex,
                location.RowIndex,
                location.CellIndex,
                new string[0]);
            var details = new List<string>(evidence ?? Enumerable.Empty<string>())
            {
                string.Format(
                    CultureInfo.InvariantCulture,
                    "结构化位置：Paragraph {0}{1}",
                    location.ParagraphIndex,
                    location.IsInTable
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            "，Table {0} / Row {1} / Cell {2}",
                            location.TableIndex,
                            location.RowIndex,
                            location.CellIndex)
                        : string.Empty)
            };
            return new MechanicalIssueOccurrence(
                context.SourceDocument,
                item.Paragraph.TextPreview,
                documentLocation,
                details);
        }
    }
}
