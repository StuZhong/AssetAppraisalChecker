using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticSourceLocation : IEquatable<SemanticSourceLocation>
    {
        public SemanticSourceLocation(
            string sourceDocument,
            string sourceLocationKey,
            string locationSummary)
        {
            RequireText(sourceDocument, nameof(sourceDocument));
            RequireKey(sourceLocationKey, nameof(sourceLocationKey));
            RequireText(locationSummary, nameof(locationSummary));

            SourceDocument = sourceDocument;
            SourceLocationKey = sourceLocationKey;
            LocationSummary = locationSummary;
        }

        public string SourceDocument { get; }

        public string SourceLocationKey { get; }

        public string LocationSummary { get; }

        public bool Equals(SemanticSourceLocation other)
        {
            return other != null
                && string.Equals(SourceDocument, other.SourceDocument, StringComparison.Ordinal)
                && string.Equals(SourceLocationKey, other.SourceLocationKey, StringComparison.Ordinal)
                && string.Equals(LocationSummary, other.LocationSummary, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticSourceLocation);
        }

        public override int GetHashCode()
        {
            return StableOrdinalHash(string.Concat(
                SourceDocument,
                "|",
                SourceLocationKey,
                "|",
                LocationSummary));
        }

        public override string ToString()
        {
            return string.Concat(SourceDocument, " | ", LocationSummary);
        }

        internal static int StableOrdinalHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char item in value)
                {
                    hash ^= item;
                    hash *= 16777619;
                }

                return (int)hash;
            }
        }

        internal static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("值不能为空。", parameterName);
            }
        }

        internal static void RequireKey(string value, string parameterName)
        {
            RequireText(value, parameterName);
            if (value.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
            {
                throw new ArgumentException("稳定键不能包含控制字符。", parameterName);
            }
        }
    }
}
