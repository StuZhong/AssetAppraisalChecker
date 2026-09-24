using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Tests.Document
{
    internal sealed class SyntheticDocxFile : IDisposable
    {
        private SyntheticDocxFile(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

        public static SyntheticDocxFile Create(params OpenXmlElement[] bodyElements)
        {
            return CreateWithStyles(null, bodyElements);
        }

        public static SyntheticDocxFile CreateWithStyles(
            IEnumerable<Style> styles,
            params OpenXmlElement[] bodyElements)
        {
            string filePath = NewTemporaryPath(".docx");

            using (WordprocessingDocument document = WordprocessingDocument.Create(
                filePath,
                WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = document.AddMainDocumentPart();
                var body = new Body();
                foreach (OpenXmlElement element in bodyElements)
                {
                    body.Append(element);
                }

                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);

                if (styles != null)
                {
                    StyleDefinitionsPart stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                    stylesPart.Styles = new Styles(styles);
                }
            }

            return new SyntheticDocxFile(filePath);
        }

        public static SyntheticDocxFile CreateRaw(byte[] content, string extension = ".docx")
        {
            string filePath = NewTemporaryPath(extension);
            File.WriteAllBytes(filePath, content ?? new byte[0]);
            return new SyntheticDocxFile(filePath);
        }

        public void Dispose()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }

        private static string NewTemporaryPath(string extension)
        {
            return Path.Combine(
                Path.GetTempPath(),
                "AssetAppraisalChecker-Synthetic-" + Guid.NewGuid().ToString("N") + extension);
        }
    }
}
