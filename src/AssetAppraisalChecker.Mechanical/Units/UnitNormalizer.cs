using System;
using System.Collections.Generic;

namespace AssetAppraisalChecker.Mechanical.Units
{
    public sealed class UnitNormalizer
    {
        private static readonly IReadOnlyDictionary<string, Definition> Definitions =
            CreateDefinitions();

        public bool TryNormalize(string text, out UnitToken token)
        {
            token = null;
            string value = NormalizeSurface(text);
            if (value.Length == 0 || !Definitions.TryGetValue(value, out Definition definition))
            {
                return false;
            }

            token = new UnitToken(text.Trim(), definition.CanonicalUnit, definition.Family, definition.Scale);
            return true;
        }

        internal static IEnumerable<string> KnownAliases => Definitions.Keys;

        internal static string NormalizeSurface(string text)
        {
            return (text ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("\u3000", string.Empty)
                .Replace("％", "%")
                .Replace("／", "/")
                .Replace("㎏", "kg")
                .Replace("M²", "m²")
                .Replace("M³", "m³");
        }

        private static IReadOnlyDictionary<string, Definition> CreateDefinitions()
        {
            var values = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);

            Add(values, UnitFamily.Count, "Count(Unit)", 1m,
                "台", "个", "件", "套", "辆", "只", "部", "宗", "项", "座", "组", "批");
            Add(values, UnitFamily.Mass, "Kilogram", 1m, "kg", "公斤", "千克");
            Add(values, UnitFamily.Mass, "Kilogram", 0.001m, "g", "克");
            Add(values, UnitFamily.Mass, "Kilogram", 1000m, "t", "吨");
            Add(values, UnitFamily.Length, "Meter", 0.001m, "mm", "毫米");
            Add(values, UnitFamily.Length, "Meter", 0.01m, "cm", "厘米");
            Add(values, UnitFamily.Length, "Meter", 1m, "m", "米");
            Add(values, UnitFamily.Length, "Meter", 1000m, "km", "千米", "公里");
            Add(values, UnitFamily.Area, "SquareMeter", 0.000001m, "mm²", "mm2");
            Add(values, UnitFamily.Area, "SquareMeter", 0.0001m, "cm²", "cm2");
            Add(values, UnitFamily.Area, "SquareMeter", 1m, "m²", "m2", "㎡", "平方米", "平米");
            Add(values, UnitFamily.Area, "SquareMeter", 1000000m, "km²", "km2", "平方千米");
            Add(values, UnitFamily.Area, "SquareMeter", 666.66666666666666666666666667m, "亩");
            Add(values, UnitFamily.Volume, "CubicMeter", 1m, "m³", "m3", "立方米");
            Add(values, UnitFamily.Volume, "CubicMeter", 0.001m, "L", "升");
            Add(values, UnitFamily.Volume, "CubicMeter", 0.000001m, "mL", "毫升");
            Add(values, UnitFamily.Time, "Year", 1m, "年");
            Add(values, UnitFamily.Time, "Month", 1m, "月");
            Add(values, UnitFamily.Time, "Day", 1m, "日", "天");
            Add(values, UnitFamily.Time, "Hour", 1m, "小时");
            Add(values, UnitFamily.Percentage, "Ratio", 0.01m, "%");
            Add(values, UnitFamily.Currency, "CNY_Yuan", 1m, "元", "人民币元");
            Add(values, UnitFamily.Currency, "CNY_Yuan", 10000m, "万元", "人民币万元");
            return values;
        }

        private static void Add(
            IDictionary<string, Definition> values,
            UnitFamily family,
            string canonical,
            decimal scale,
            params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                values[NormalizeSurface(alias)] = new Definition(family, canonical, scale);
            }
        }

        private sealed class Definition
        {
            public Definition(UnitFamily family, string canonicalUnit, decimal scale)
            {
                Family = family;
                CanonicalUnit = canonicalUnit;
                Scale = scale;
            }

            public UnitFamily Family { get; }
            public string CanonicalUnit { get; }
            public decimal Scale { get; }
        }
    }
}
