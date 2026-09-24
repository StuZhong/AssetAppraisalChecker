using System;
using System.IO;
using System.Security;
using System.Xml;
using AssetAppraisalChecker.Core.Documents;
using DocumentFormat.OpenXml.Packaging;

namespace AssetAppraisalChecker.Document
{
    /// <summary>
    /// Reads the main body of a DOCX package without modifying the source file.
    /// </summary>
    public sealed class DocxDocumentReader
    {
        private const int AccessDeniedHResult = unchecked((int)0x80070005);
        private const int SharingViolationHResult = unchecked((int)0x80070020);
        private const int LockViolationHResult = unchecked((int)0x80070021);

        public DocumentReadResult Read(string filePath)
        {
            try
            {
                DocumentReadResult validationFailure = ValidatePath(filePath);
                if (validationFailure != null)
                {
                    return validationFailure;
                }

                // FileShare.Read is deliberately conservative: an active writer causes a safe
                // failure instead of allowing a changing package to be parsed inconsistently.
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    if (stream.Length == 0)
                    {
                        return InvalidDocx();
                    }

                    CompoundFileKind compoundFileKind = CompoundFileFormatDetector.Detect(stream);
                    if (compoundFileKind == CompoundFileKind.EncryptedOfficePackage)
                    {
                        return DocumentReadResult.Failed(
                            DocumentReadStatus.EncryptedOrProtected,
                            "无法读取该报告。文件已加密或受密码保护；程序不会尝试破解密码。原文件未被修改。");
                    }

                    if (compoundFileKind == CompoundFileKind.LegacyWordBinary)
                    {
                        return DocumentReadResult.Failed(
                            DocumentReadStatus.UnsupportedFileType,
                            "文件内容实际为旧版 .doc，请使用 WPS 另存为标准 .docx 后再校检。");
                    }

                    if (compoundFileKind == CompoundFileKind.Unknown)
                    {
                        return InvalidDocx();
                    }

                    // The stream itself has read-only access and isEditable is false. No package
                    // mutation API is used anywhere in the production reader.
                    using (WordprocessingDocument document = WordprocessingDocument.Open(stream, false))
                    {
                        if (document.MainDocumentPart == null ||
                            document.MainDocumentPart.Document == null ||
                            document.MainDocumentPart.Document.Body == null)
                        {
                            return InvalidDocx();
                        }

                        var extractor = new OpenXmlDocumentExtractor(document);
                        return extractor.Extract();
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.AccessDenied,
                    "无法读取该报告。当前用户没有文件读取权限。原文件未被修改。");
            }
            catch (SecurityException)
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.AccessDenied,
                    "无法读取该报告。当前用户没有文件读取权限。原文件未被修改。");
            }
            catch (OpenXmlPackageException)
            {
                return InvalidDocx();
            }
            catch (FileFormatException)
            {
                return InvalidDocx();
            }
            catch (FormatException)
            {
                return InvalidDocx();
            }
            catch (InvalidDataException)
            {
                return InvalidDocx();
            }
            catch (XmlException)
            {
                return InvalidDocx();
            }
            catch (IOException exception)
            {
                if (exception.HResult == SharingViolationHResult ||
                    exception.HResult == LockViolationHResult)
                {
                    return DocumentReadResult.Failed(
                        DocumentReadStatus.FileInUse,
                        "无法安全读取该报告。文件可能正在保存或被其他程序占用，请先保存报告，必要时关闭文档后重试。原文件未被修改。");
                }

                if (exception.HResult == AccessDeniedHResult)
                {
                    return DocumentReadResult.Failed(
                        DocumentReadStatus.AccessDenied,
                        "无法读取该报告。当前用户没有文件读取权限。原文件未被修改。");
                }

                return DocumentReadResult.Failed(
                    DocumentReadStatus.IoError,
                    "无法读取该报告。读取过程中发生文件系统错误，请确认文件已保存且可访问。原文件未被修改。");
            }
            catch (Exception)
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.UnexpectedError,
                    "无法读取该报告。发生了未预期的文档读取错误。原文件未被修改。");
            }
        }

        private static DocumentReadResult ValidatePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.InvalidPath,
                    "请选择一个有效的 .docx 文件。文件路径不能为空。");
            }

            string extension;
            try
            {
                extension = Path.GetExtension(filePath);
            }
            catch (ArgumentException)
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.InvalidPath,
                    "请选择一个有效的 .docx 文件。文件路径格式无效。");
            }

            if (string.Equals(extension, ".doc", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.UnsupportedFileType,
                    "当前版本暂不直接支持 .doc 格式，请先使用 WPS 将文件另存为 .docx 后再进行校检。");
            }

            if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.UnsupportedFileType,
                    "当前版本仅支持 .docx 文件。原文件未被修改。");
            }

            if (!File.Exists(filePath))
            {
                return DocumentReadResult.Failed(
                    DocumentReadStatus.FileNotFound,
                    "找不到指定的 .docx 文件，请确认文件路径后重试。");
            }

            return null;
        }

        private static DocumentReadResult InvalidDocx()
        {
            return DocumentReadResult.Failed(
                DocumentReadStatus.InvalidDocx,
                "无法读取该报告，文件可能已损坏或不是有效的 DOCX 文件。原文件未被修改。");
        }
    }
}
