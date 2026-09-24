using System.Text;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class SubjectTextNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var result = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (char character in text.Trim())
            {
                if (char.IsWhiteSpace(character) || character == '\u3000')
                {
                    pendingSpace = result.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    result.Append(' ');
                    pendingSpace = false;
                }

                result.Append(character);
            }

            return result.ToString();
        }
    }
}
