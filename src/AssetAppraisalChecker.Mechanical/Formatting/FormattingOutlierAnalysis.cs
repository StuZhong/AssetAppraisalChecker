using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    internal sealed class FormattingDifference
    {
        public FormattingDifference(
            string fieldName,
            string currentValue,
            string dominantValue,
            IEnumerable<FormattingPeerParagraph> supportingPeers)
        {
            FieldName = fieldName;
            CurrentValue = currentValue;
            DominantValue = dominantValue;
            SupportingPeers = new ReadOnlyCollection<FormattingPeerParagraph>(
                (supportingPeers ?? Enumerable.Empty<FormattingPeerParagraph>()).ToList());
        }

        public string FieldName { get; }
        public string CurrentValue { get; }
        public string DominantValue { get; }
        public IReadOnlyList<FormattingPeerParagraph> SupportingPeers { get; }
    }

    internal static class FormattingOutlierAnalysis
    {
        internal const decimal PointTolerance = 0.01m;
        internal const decimal CharacterTolerance = 0.01m;
        internal const decimal LineTolerance = 0.01m;
        internal const decimal LineMultipleTolerance = 0.001m;

        public static FormattingDifference AnalyzeString(
            string fieldName,
            FormattingValue<string> current,
            IReadOnlyList<FormattingPeerParagraph> peers,
            Func<FormattingPeerParagraph, FormattingValue<string>> selector)
        {
            if (current == null || !current.IsResolved) return null;
            List<ValuePeer<string>> values = GetResolvedPeers(peers, selector);
            if (values.Count != peers.Count || values.Count < FormattingPeerGroupBuilder.MinimumComparablePeers)
            {
                return null;
            }

            IGrouping<string, ValuePeer<string>> dominant = values
                .GroupBy(item => item.Value.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .First();
            if (!IsDominant(dominant.Count(), peers.Count) ||
                string.Equals(current.Value, dominant.Key, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Difference(fieldName, Display(current), Display(dominant.First().Value), dominant);
        }

        public static FormattingDifference AnalyzeBoolean(
            string fieldName,
            FormattingValue<bool> current,
            IReadOnlyList<FormattingPeerParagraph> peers,
            Func<FormattingPeerParagraph, FormattingValue<bool>> selector)
        {
            if (current == null || !current.IsResolved) return null;
            List<ValuePeer<bool>> values = GetResolvedPeers(peers, selector);
            if (values.Count != peers.Count || values.Count < FormattingPeerGroupBuilder.MinimumComparablePeers)
            {
                return null;
            }

            IGrouping<bool, ValuePeer<bool>> dominant = values
                .GroupBy(item => item.Value.Value)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First();
            if (!IsDominant(dominant.Count(), peers.Count) || current.Value == dominant.Key)
            {
                return null;
            }

            return Difference(fieldName, Display(current), Display(dominant.First().Value), dominant);
        }

        public static FormattingDifference AnalyzeNumber(
            string fieldName,
            FormattingValue<decimal> current,
            IReadOnlyList<FormattingPeerParagraph> peers,
            Func<FormattingPeerParagraph, FormattingValue<decimal>> selector)
        {
            ComparableNumber currentNumber;
            if (!TryNormalize(current, out currentNumber)) return null;

            var values = new List<NumberPeer>();
            foreach (FormattingPeerParagraph peer in peers)
            {
                ComparableNumber number;
                if (!TryNormalize(selector(peer), out number) ||
                    !string.Equals(number.Dimension, currentNumber.Dimension, StringComparison.Ordinal))
                {
                    return null;
                }
                values.Add(new NumberPeer(peer, selector(peer), number));
            }
            if (values.Count < FormattingPeerGroupBuilder.MinimumComparablePeers) return null;

            IGrouping<decimal, NumberPeer> dominant = values
                .GroupBy(item => Bucket(item.Number.Value, item.Number.Tolerance))
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First();
            NumberPeer exemplar = dominant.First();
            if (!IsDominant(dominant.Count(), peers.Count) ||
                Math.Abs(currentNumber.Value - exemplar.Number.Value) <= currentNumber.Tolerance)
            {
                return null;
            }

            return new FormattingDifference(
                fieldName,
                Display(current),
                Display(exemplar.Value),
                dominant.Select(item => item.Peer));
        }

        private static bool IsDominant(int count, int total)
        {
            return total >= FormattingPeerGroupBuilder.MinimumComparablePeers &&
                (decimal)count / total >= FormattingPeerGroupBuilder.DominanceRatio;
        }

        private static FormattingDifference Difference<T>(
            string fieldName,
            string current,
            string dominant,
            IEnumerable<ValuePeer<T>> peers)
        {
            return new FormattingDifference(
                fieldName,
                current,
                dominant,
                peers.Select(item => item.Peer));
        }

        private static List<ValuePeer<T>> GetResolvedPeers<T>(
            IEnumerable<FormattingPeerParagraph> peers,
            Func<FormattingPeerParagraph, FormattingValue<T>> selector)
        {
            return peers.Select(peer => new ValuePeer<T>(peer, selector(peer)))
                .Where(item => item.Value != null && item.Value.IsResolved)
                .ToList();
        }

        private static bool TryNormalize(
            FormattingValue<decimal> value,
            out ComparableNumber number)
        {
            number = default(ComparableNumber);
            if (value == null || !value.IsResolved) return false;
            switch (value.Unit ?? string.Empty)
            {
                case "Twips":
                    number = new ComparableNumber(value.Value / 20m, "Points", PointTolerance);
                    return true;
                case "Points":
                    number = new ComparableNumber(value.Value, "Points", PointTolerance);
                    return true;
                case "Characters":
                    number = new ComparableNumber(value.Value, "Characters", CharacterTolerance);
                    return true;
                case "Lines":
                    number = new ComparableNumber(value.Value, "Lines", LineTolerance);
                    return true;
                case "LineMultiple":
                    number = new ComparableNumber(value.Value, "LineMultiple", LineMultipleTolerance);
                    return true;
                default:
                    return false;
            }
        }

        private static decimal Bucket(decimal value, decimal tolerance)
        {
            return Math.Round(value / tolerance, MidpointRounding.AwayFromZero);
        }

        public static string Display<T>(FormattingValue<T> value)
        {
            if (value == null || !value.IsResolved) return value?.Status.ToString() ?? "Unknown";
            string result = Convert.ToString(value.Value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(value.Unit)) result += " " + value.Unit;
            result += "（来源：" + value.Source + "）";
            return result;
        }

        private sealed class ValuePeer<T>
        {
            public ValuePeer(FormattingPeerParagraph peer, FormattingValue<T> value)
            {
                Peer = peer;
                Value = value;
            }

            public FormattingPeerParagraph Peer { get; }
            public FormattingValue<T> Value { get; }
        }

        private sealed class NumberPeer
        {
            public NumberPeer(
                FormattingPeerParagraph peer,
                FormattingValue<decimal> value,
                ComparableNumber number)
            {
                Peer = peer;
                Value = value;
                Number = number;
            }

            public FormattingPeerParagraph Peer { get; }
            public FormattingValue<decimal> Value { get; }
            public ComparableNumber Number { get; }
        }

        private struct ComparableNumber
        {
            public ComparableNumber(decimal value, string dimension, decimal tolerance)
            {
                Value = value;
                Dimension = dimension;
                Tolerance = tolerance;
            }

            public decimal Value { get; }
            public string Dimension { get; }
            public decimal Tolerance { get; }
        }
    }
}
