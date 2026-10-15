using System;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class TextSpan
    {
        public TextSpan(int start, int length)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Start = start;
            Length = length;
        }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public bool Overlaps(TextSpan other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Start < other.End && other.Start < End;
        }

        public bool Contains(TextSpan other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Start <= other.Start && End >= other.End;
        }
    }
}
