using System;
using System.Text;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class TextQualityNormalizer
    {
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                if (character == '\u00A0' || character == '\u3000')
                {
                    builder.Append(' ');
                }
                else if (character >= '\uFF01' && character <= '\uFF5E')
                {
                    builder.Append((char)(character - 0xFEE0));
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
