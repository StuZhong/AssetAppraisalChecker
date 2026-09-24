using System.Collections.Generic;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    internal static class BasicFieldTestFactory
    {
        public static DocumentReadResult Paragraphs(params string[] texts)
        {
            var elements = new List<DocumentElement>();
            for (int index = 0; index < texts.Length; index++)
            {
                string previous = index > 0 ? texts[index - 1] : string.Empty;
                string next = index + 1 < texts.Length ? texts[index + 1] : string.Empty;
                elements.Add(new DocumentElement(
                    DocumentElementType.Paragraph,
                    texts[index],
                    new DocumentLocation(DocumentPart.MainBody, index, null, null, null, null),
                    index,
                    string.Empty,
                    new DocumentNearbyContext(previous, texts[index], next)));
            }

            return DocumentReadResult.Succeeded(elements, texts.Length, 0);
        }

        public static BasicFieldOccurrence Occurrence(
            BasicFieldType fieldType,
            string value,
            int paragraphIndex,
            ClassificationConfidence confidence = ClassificationConfidence.High)
        {
            BasicFieldNormalizationResult normalization = BasicFieldNormalizer.Normalize(fieldType, value);
            return new BasicFieldOccurrence(
                fieldType,
                value,
                value,
                normalization,
                new DocumentLocation(DocumentPart.MainBody, paragraphIndex, null, null, null, null),
                new DocumentNearbyContext(string.Empty, value, string.Empty),
                confidence,
                new[] { "自动化测试构造的明确字段证据。" },
                0,
                value.Length);
        }
    }
}
