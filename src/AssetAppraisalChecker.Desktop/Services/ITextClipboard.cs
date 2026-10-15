using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace AssetAppraisalChecker.Desktop.Services
{
    public interface ITextClipboard { bool TryCopy(string text); }

    public sealed class WindowsTextClipboard : ITextClipboard
    {
        public bool TryCopy(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            try { Clipboard.SetText(text); return true; }
            catch (ExternalException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
