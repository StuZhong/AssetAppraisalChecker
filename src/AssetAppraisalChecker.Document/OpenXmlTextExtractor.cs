using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Document
{
    internal static class OpenXmlTextExtractor
    {
        public static string ExtractParagraphText(Paragraph paragraph)
        {
            var builder = new StringBuilder();

            foreach (OpenXmlElement element in paragraph.Descendants())
            {
                var text = element as Text;
                if (text != null)
                {
                    builder.Append(text.Text);
                    continue;
                }

                if (element is TabChar)
                {
                    builder.Append('\t');
                }
                else if (element is Break || element is CarriageReturn)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        public static string ExtractCellText(TableCell cell)
        {
            // Descendant paragraphs inside nested tables belong to their own cells and must not
            // be duplicated in the outer cell. Paragraphs wrapped in content controls are kept.
            var paragraphTexts = cell
                .Descendants<Paragraph>()
                .Where(paragraph => paragraph.Ancestors<TableCell>().FirstOrDefault() == cell)
                .Select(ExtractParagraphText);

            return string.Join("\n", paragraphTexts);
        }
    }
}
