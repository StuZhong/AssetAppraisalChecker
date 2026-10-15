using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Mechanical
{
    internal static class MechanicalCheckerSupport
    {
        public static MechanicalIssueOccurrence Occurrence(
            MechanicalCheckContext context,
            DocumentElement element,
            string rawText,
            params string[] evidence)
        {
            return new MechanicalIssueOccurrence(
                context.SourceDocument,
                rawText ?? element?.Text ?? string.Empty,
                element?.Location ?? UnknownLocation(),
                evidence);
        }

        public static DocumentLocation UnknownLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                null,
                null,
                null,
                null,
                new string[0]);
        }

        public static DocumentElement FindBestElement(
            MechanicalCheckContext context,
            string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return null;
            }

            return context.Document.Elements.FirstOrDefault(item =>
                string.Equals(item.Text.Trim(), normalized, StringComparison.Ordinal))
                ?? context.Document.Elements.FirstOrDefault(item =>
                    item.Text.IndexOf(normalized, StringComparison.Ordinal) >= 0);
        }

        public static bool IsBodyParagraph(DocumentElement element)
        {
            return element != null && element.ElementType == DocumentElementType.Paragraph;
        }

        public static IReadOnlyList<MechanicalIssue> ReadOnly(List<MechanicalIssue> issues)
        {
            return issues.AsReadOnly();
        }
    }
}
