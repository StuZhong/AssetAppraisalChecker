using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    /// <summary>Identity captured by the read-only check, never reconstructed at navigation time.</summary>
    public sealed class CheckedDocumentSource
    {
        public CheckedDocumentSource(string label, string path, string sha256, DocumentReadResult document)
        {
            if (label != "文档 A" && label != "文档 B") throw new ArgumentException("文档来源必须明确。", nameof(label));
            if (sha256 == null || !Regex.IsMatch(sha256, "\\A[0-9a-fA-F]{64}\\z"))
                throw new ArgumentException("需要有效的 SHA-256。", nameof(sha256));
            if (document == null || !document.IsSuccess) throw new ArgumentException("需要成功读取的文档。", nameof(document));
            Label = label;
            FullPath = Path.GetFullPath(path);
            Sha256 = sha256.ToUpperInvariant();
            Document = document;
        }

        public string Label { get; }
        public string FullPath { get; }
        public string Sha256 { get; }
        public DocumentReadResult Document { get; }

        public static string ComputeSha256(string path)
        {
            // WPS holds a write-capable handle even when the document is saved.
            // This does not write: the full digest must still match the checked identity.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        public bool IsUnchanged() => string.Equals(Sha256, ComputeSha256(FullPath), StringComparison.Ordinal);

        internal MemoryStream ReadVerifiedSnapshot()
        {
            var snapshot = new MemoryStream();
            try
            {
                using (var stream = new FileStream(FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    stream.CopyTo(snapshot);
                snapshot.Position = 0;
                using (var hash = SHA256.Create())
                {
                    string digest = BitConverter.ToString(hash.ComputeHash(snapshot)).Replace("-", string.Empty);
                    if (!string.Equals(Sha256, digest, StringComparison.Ordinal))
                        throw new IOException("原文已变化，请重新校检后定位。");
                }
                snapshot.Position = 0;
                return snapshot;
            }
            catch { snapshot.Dispose(); throw; }
        }
    }
}
