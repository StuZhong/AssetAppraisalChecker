using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AssetAppraisalChecker.Document.Excel
{
    internal static class ExcelSummaryExtractor
    {
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex UnitDeclaration = new Regex(
            @"(?:金额单位|单位)[:：]?(?:人民币)?(?<unit>亿元|万元|元)|[（(](?<unit>亿元|万元|元)[）)]",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex NumericValue = new Regex(
            @"^[+-]?(?<whole>[0-9]+)(?:\.(?<fraction>[0-9]*))?(?:[eE](?<exponent>[+-]?[0-9]+))?$",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public static ExcelSummaryReadResult Extract(WorkbookPart workbook, WorksheetPart worksheet)
        {
            var cells = new ExcelWorksheetCells(workbook, worksheet);
            uint headerRow;
            Dictionary<ExcelSummaryField, int> fields = FindFields(cells, out headerRow);
            RequireTenThousandYuan(cells, headerRow);
            uint assetRow;
            uint totalRow;
            FindAmountRows(cells, fields.Values.ToArray(), headerRow, out assetRow, out totalRow);

            var amounts = new List<ExcelSummaryAmount>();
            foreach (ExcelSummaryRowKind kind in new[] { ExcelSummaryRowKind.Asset, ExcelSummaryRowKind.Total })
            {
                uint row = kind == ExcelSummaryRowKind.Asset ? assetRow : totalRow;
                foreach (ExcelSummaryField field in Enum.GetValues(typeof(ExcelSummaryField)))
                {
                    int column = fields[field];
                    string address = ExcelWorksheetCells.Address(column, row);
                    if (cells.IsMerged(column, row))
                        throw Failure(ExcelSummaryReadStatus.AmbiguousLayout, address + "：金额单元格存在合并，无法可靠对应四类金额。");
                    decimal raw = ReadAmount(workbook, worksheet, cells.Get(column, row), address);
                    amounts.Add(new ExcelSummaryAmount(field, raw,
                        ExcelSummaryReader.SummaryWorksheetName, address, kind));
                }
            }
            return ExcelSummaryReadResult.Succeeded(amounts);
        }

        private static Dictionary<ExcelSummaryField, int> FindFields(ExcelWorksheetCells cells, out uint headerRow)
        {
            Dictionary<ExcelSummaryField, int> result = null;
            headerRow = 0;
            foreach (Row row in cells.Rows)
            {
                var candidate = new Dictionary<ExcelSummaryField, int>();
                foreach (Cell cell in row.Elements<Cell>())
                {
                    int column = ExcelWorksheetCells.Column(cell);
                    ExcelSummaryField field;
                    if (!TryField(cells.Text(cell), cells.HeaderAbove(column, row.RowIndex.Value), out field)) continue;
                    if (candidate.ContainsKey(field))
                        throw Failure(ExcelSummaryReadStatus.AmbiguousLayout, "同一行存在重复金额字段表头，无法可靠确定列。");
                    candidate.Add(field, column);
                }
                if (candidate.Count != 4) continue;
                if (result != null)
                    throw Failure(ExcelSummaryReadStatus.AmbiguousLayout, "存在多组完整金额表头，当前版本不猜测使用哪一组。");
                result = candidate;
                headerRow = row.RowIndex.Value;
            }
            if (result == null)
                throw Failure(ExcelSummaryReadStatus.MissingRequiredField,
                    "缺少可明确对应的四项金额表头：评估原值/净值（不含税）、评估原值/净值（含税）。");
            return result;
        }

        private static bool TryField(string text, string above, out ExcelSummaryField field)
        {
            string label = Normalize(text);
            foreach (string unit in new[] { "亿元", "万元", "元" })
                if (label.EndsWith(unit, StringComparison.Ordinal)) { label = label.Substring(0, label.Length - unit.Length); break; }
            string category = null;
            string tax = null;
            foreach (string basis in new[] { "不含税", "含税" })
            {
                foreach (string name in new[] { "评估原值", "评估净值" })
                {
                    if (label == name + basis || label == basis + name)
                    {
                        category = name;
                        tax = basis;
                    }
                }
            }
            if (category == null && (label == "评估原值" || label == "评估净值" || label == "原值" || label == "净值"))
            {
                category = label.EndsWith("原值", StringComparison.Ordinal) ? "评估原值" : "评估净值";
                string group = Normalize(above);
                foreach (string unit in new[] { "亿元", "万元", "元" })
                    if (group.EndsWith(unit, StringComparison.Ordinal)) { group = group.Substring(0, group.Length - unit.Length); break; }
                foreach (string basis in new[] { "不含税", "含税" })
                    if (group == basis || group == "评估值" + basis || group == "评估价值" + basis
                        || group == basis + "评估值" || group == basis + "评估价值") tax = basis;
            }

            field = ExcelSummaryField.AppraisedOriginalValueTaxExcluded;
            if (category == null || tax == null) return false;
            if (category == "评估原值")
                field = tax == "不含税" ? ExcelSummaryField.AppraisedOriginalValueTaxExcluded : ExcelSummaryField.AppraisedOriginalValueTaxIncluded;
            else
                field = tax == "不含税" ? ExcelSummaryField.AppraisedNetValueTaxExcluded : ExcelSummaryField.AppraisedNetValueTaxIncluded;
            return true;
        }

        private static void RequireTenThousandYuan(ExcelWorksheetCells cells, uint headerRow)
        {
            var units = new HashSet<string>(StringComparer.Ordinal);
            foreach (Cell cell in cells.Rows.Where(row => row.RowIndex.Value <= headerRow).SelectMany(row => row.Elements<Cell>()))
                foreach (Match match in UnitDeclaration.Matches(Whitespace.Replace(cells.Text(cell), string.Empty)))
                    units.Add(match.Groups["unit"].Value);
            if (units.Count != 1 || !units.Contains("万元"))
                throw Failure(ExcelSummaryReadStatus.UnitNotSupported,
                    "当前版本要求汇总表表头或表头上方明确声明金额单位为万元；单位缺失、冲突或为其他单位时不推测换算。");
        }

        private static void FindAmountRows(ExcelWorksheetCells cells, int[] columns, uint headerRow,
            out uint assetRow, out uint totalRow)
        {
            var assets = new List<uint>();
            var totals = new List<uint>();
            int firstAmountColumn = columns.Min();
            foreach (Row row in cells.Rows.Where(item => item.RowIndex.Value > headerRow))
            {
                string[] labels = row.Elements<Cell>().Where(cell => ExcelWorksheetCells.Column(cell) < firstAmountColumn)
                    .Select(cell => Normalize(cells.Text(cell))).Where(text => text.Length > 0).ToArray();
                // The asset caption can itself be a string formula. Its content is outside
                // the amount check; an explicit, numeric sequence column still identifies the row.
                bool hasOrdinal = row.Elements<Cell>().Any(cell => ExcelWorksheetCells.Column(cell) < firstAmountColumn
                    && Normalize(cells.Text(cells.Get(ExcelWorksheetCells.Column(cell), headerRow))) == "序号"
                    && IsOrdinal(cell));
                bool total = labels.Any(label => label == "合计" || label == "总计");
                bool hasAmounts = columns.Any(column => HasContent(cells.Get(column, row.RowIndex.Value)));
                // The verified template places an institution caption in E8 after the total.
                // That caption is metadata, even though it occupies a former amount column.
                // Ignore only an explicit text-only caption; extra amounts/formulas still fail closed.
                if (totals.Count > 0 && !total && IsInstitutionFooter(cells, row)) continue;
                if (total) totals.Add(row.RowIndex.Value);
                else if (hasAmounts || (totals.Count == 0 && (labels.Length > 0 || hasOrdinal)))
                {
                    if ((labels.Length == 0 && !hasOrdinal) || totals.Count > 0)
                        throw Failure(ExcelSummaryReadStatus.UnsupportedAssetStructure,
                            "金额行缺少明确的行标识，或合计之后仍存在金额行；当前版本不猜测对应关系。");
                    assets.Add(row.RowIndex.Value);
                }
            }
            if (assets.Count != 1 || totals.Count != 1 || assets[0] >= totals[0])
                throw Failure(ExcelSummaryReadStatus.UnsupportedAssetStructure,
                    "当前版本仅支持一条资产记录和其后的一条明确合计行；多资产、缺失合计或重复合计暂不支持。");
            assetRow = assets[0];
            totalRow = totals[0];
        }

        private static bool IsInstitutionFooter(ExcelWorksheetCells cells, Row row)
        {
            Cell[] populated = row.Elements<Cell>().Where(HasContent).ToArray();
            return populated.Length > 0 && populated.All(cell => cell.CellFormula == null
                && (Normalize(cells.Text(cell)).StartsWith("评估机构：", StringComparison.Ordinal)
                    || Normalize(cells.Text(cell)).StartsWith("评估机构:", StringComparison.Ordinal)));
        }

        private static bool IsOrdinal(Cell cell)
        {
            uint ordinal;
            return cell.CellFormula == null && (cell.DataType == null || cell.DataType.Value == CellValues.Number)
                && uint.TryParse(cell.CellValue?.Text, NumberStyles.None, CultureInfo.InvariantCulture, out ordinal)
                && ordinal > 0;
        }

        private static bool HasContent(Cell cell) => cell != null && (cell.CellFormula != null
            || !string.IsNullOrWhiteSpace(cell.CellValue?.Text) || !string.IsNullOrWhiteSpace(cell.InlineString?.InnerText));

        private static decimal ReadAmount(WorkbookPart workbook, WorksheetPart worksheet, Cell cell, string address)
        {
            bool formula = cell?.CellFormula != null;
            ExcelSummaryReadStatus status = formula ? ExcelSummaryReadStatus.FormulaResultUnavailable : ExcelSummaryReadStatus.InvalidAmount;
            string source = ExcelSummaryReader.SummaryWorksheetName + "!" + address;
            if (cell == null || string.IsNullOrWhiteSpace(cell.CellValue?.Text))
                throw Failure(status, source + (formula ? "：公式缺少缓存计算结果，请在 WPS/Excel 中完成计算并保存后重试。" : "：缺少金额，不能按零处理。"));
            if (cell.DataType != null && cell.DataType.Value != CellValues.Number)
                throw Failure(status, source + "：金额或公式缓存不是数值类型，不能作为可靠金额读取。");
            if (formula && RequiresCalculation(workbook, worksheet, cell))
                throw Failure(ExcelSummaryReadStatus.FormulaResultUnavailable,
                    source + "：存在待重算或手动计算标志，无法确认公式缓存可靠；请完成计算并保存后重试。");
            RequireNumericFormat(workbook, cell, source, status);
            decimal value;
            if (!TryExactDecimal(cell.CellValue.Text, out value))
                throw Failure(status, source + "：金额无法以 decimal 精确读取，未生成替代数值。");
            return value;
        }

        private static bool RequiresCalculation(WorkbookPart workbook, WorksheetPart worksheet, Cell cell)
        {
            CalculationProperties calculation = workbook.Workbook.CalculationProperties;
            return calculation?.FullCalculationOnLoad?.Value == true
                || calculation?.ForceFullCalculation?.Value == true
                || calculation?.CalculationCompleted?.Value == false
                || calculation?.CalculationMode?.Value == CalculateModeValues.Manual
                || worksheet.Worksheet.GetFirstChild<SheetCalculationProperties>()?.FullCalculationOnLoad?.Value == true
                || cell.CellFormula.CalculateCell?.Value == true;
        }

        private static void RequireNumericFormat(WorkbookPart workbook, Cell cell, string source, ExcelSummaryReadStatus status)
        {
            if (cell.StyleIndex == null) return;
            Stylesheet styles = workbook.WorkbookStylesPart?.Stylesheet;
            CellFormat format = styles?.CellFormats?.Elements<CellFormat>().ElementAtOrDefault((int)cell.StyleIndex.Value);
            if (format == null) throw Failure(status, source + "：单元格格式引用无效，无法确认金额类型。");
            uint id = format.NumberFormatId?.Value ?? 0;
            if (id == 9 || id == 10 || id == 49 || (id >= 14 && id <= 36)
                || (id >= 45 && id <= 47) || (id >= 50 && id <= 58))
                throw Failure(status, source + "：单元格采用百分比或日期时间格式，不能作为金额读取。");
            if (id < 164) return;
            string code = styles.NumberingFormats?.Elements<NumberingFormat>()
                .FirstOrDefault(item => item.NumberFormatId?.Value == id)?.FormatCode?.Value;
            if (code == null) throw Failure(status, source + "：缺少自定义数值格式，无法确认金额类型。");
            // Remove quoted labels, escapes and color/condition directives before checking format tokens.
            bool elapsedTime = Regex.IsMatch(code, @"\[[hms]+\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            string tokens = Regex.Replace(code, @"""[^""]*""|\\.|\[[^\]]*\]|_.|\*.", string.Empty,
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            // A conventional fourth section formats text, not numeric amounts.
            tokens = string.Join(";", tokens.Split(';').Take(3));
            if (elapsedTime || Regex.IsMatch(tokens, @"[%@ymdhs]|,(?![0#?])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1)))
                throw Failure(status, source + "：自定义格式为百分比、日期、文本或缩放数值，当前版本不推测金额。");
        }

        private static bool TryExactDecimal(string raw, out decimal value)
        {
            value = 0m;
            Match match = NumericValue.Match(raw.Trim());
            if (!match.Success) return false;
            int exponent = 0;
            if (match.Groups["exponent"].Success && !int.TryParse(match.Groups["exponent"].Value,
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return false;
            string digits = match.Groups["whole"].Value + match.Groups["fraction"].Value;
            int trailingZeros = digits.Length - digits.TrimEnd('0').Length;
            string significant = digits.TrimStart('0').TrimEnd('0');
            long scale = (long)match.Groups["fraction"].Length - exponent - trailingZeros;
            // Decimal.TryParse may silently round excess precision or underflow to zero.
            // Reject that input rather than claiming the rounded value is the original cache.
            if (significant.Length > 28 || (significant.Length > 0 && scale > 28)) return false;
            return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Normalize(string text) => Whitespace.Replace(text ?? string.Empty, string.Empty)
            .Replace("（", string.Empty).Replace("）", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty);

        private static ExcelSummaryReadException Failure(ExcelSummaryReadStatus status, string message) =>
            new ExcelSummaryReadException(status, message);
    }
}
