using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class FormattingSnapshotDiagnosticFormatter
    {
        public string Format(DocumentFormattingSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var output = new StringBuilder();
            foreach (ParagraphFormattingSnapshot paragraph in snapshot.Paragraphs)
            {
                output.AppendLine("Paragraph " + paragraph.Location.ParagraphIndex);
                output.AppendLine("Text: " + paragraph.TextPreview);
                output.AppendLine("StyleId: " + EmptyAsUnknown(paragraph.StyleId));
                Append(output, "Alignment", paragraph.EffectiveAlignment);
                Append(output, "FirstLineIndent", paragraph.EffectiveFirstLineIndent);
                Append(output, "LineSpacing", paragraph.EffectiveLineSpacing);
                foreach (RunFormattingSnapshot run in paragraph.Runs)
                {
                    output.AppendLine("Run " + run.RunIndex + ": " + run.Text);
                    Append(output, "EastAsiaFont", run.EffectiveFontEastAsia);
                    Append(output, "AsciiFont", run.EffectiveFontAscii);
                    Append(output, "FontSize", run.EffectiveFontSize);
                    Append(output, "Bold", run.Bold);
                    Append(output, "Color", run.Color);
                }

                output.AppendLine();
            }

            return output.ToString();
        }

        private static void Append<T>(
            StringBuilder output,
            string name,
            FormattingValue<T> value)
        {
            string display = value.IsResolved
                ? Convert.ToString(value.Value, CultureInfo.InvariantCulture)
                : value.Status.ToString();
            if (!string.IsNullOrWhiteSpace(value.Unit))
            {
                display += " " + value.Unit;
            }
            output.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1} <- {2}",
                name,
                display,
                value.Source));
        }

        private static string EmptyAsUnknown(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }
    }
}
