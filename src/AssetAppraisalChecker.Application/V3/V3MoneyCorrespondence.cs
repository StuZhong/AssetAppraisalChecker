using System;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Money;
using AssetAppraisalChecker.Document.Excel;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Application.V3
{
    internal static class V3MoneyCorrespondence
    {
        public static WordConclusion FindConclusion(WordConclusionExtractionResult result, WordConclusionTarget target,
            string source, out string reason)
        {
            reason = null;
            if (result == null || !result.IsSuccess)
            {
                reason = "Word 结论提取未成功：" + (result?.Message ?? "缺少提取结果。");
                return null;
            }
            bool explanation = target == WordConclusionTarget.ExplanationChapterFour;
            if (result.Conclusions.Any(item => item == null
                || (explanation ? item.Target != WordConclusionTarget.ExplanationChapterFour
                    : item.Target != WordConclusionTarget.ReportSummary && item.Target != WordConclusionTarget.ReportChapterTen)
                || string.IsNullOrWhiteSpace(item.SourceDocument) || item.SourceDocument != source))
            {
                reason = "Word 提取结果包含非预期结论或相互矛盾的文件来源，无法确定对应关系。";
                return null;
            }
            WordConclusion[] matches = result.Conclusions.Where(item => item.Target == target).ToArray();
            if (matches.Length != 1)
            {
                reason = matches.Length == 0 ? "缺少指定 Word 结论。" : "指定 Word 结论存在重复，无法唯一对应。";
                return null;
            }
            WordConclusion conclusion = matches[0];
            if (conclusion.HeadingLocation == null || conclusion.TableIndex < 0
                || conclusion.Amounts.Any(amount => !IsValidWordAmount(amount, conclusion)))
            {
                reason = "Word 金额的字段、表达类型、结论归属或位置证据不完整，无法可靠对应。";
                return null;
            }
            return conclusion;
        }

        public static string FindWordSource(WordConclusionExtractionResult result) =>
            result?.Conclusions.FirstOrDefault(item => item != null)?.SourceDocument ?? string.Empty;

        public static WordConclusionAmount FindWordAmount(WordConclusion conclusion, WordConclusionSourceKind kind,
            ValueCategory category, TaxBasis tax, out string reason)
        {
            reason = null;
            if (conclusion == null) return null;
            WordConclusionAmount[] matches = conclusion.Amounts.Where(amount => amount.SourceKind == kind
                && amount.ValueCategory == category && amount.TaxBasis == tax).ToArray();
            if (matches.Length == 1) return matches[0];
            reason = matches.Length == 0 ? "缺少该字段及来源类型的 Word 金额。" : "该字段及来源类型的 Word 金额存在重复，无法唯一对应。";
            return null;
        }

        public static string ExcelProblem(ExcelSummaryReadResult result, string source)
        {
            if (result == null || !result.IsSuccess)
                return "Excel 汇总金额读取未成功：" + (result?.Message ?? "缺少读取结果。");
            if (string.IsNullOrWhiteSpace(source)) return "缺少 Excel 文件来源，无法确定参考工作簿。";
            if (result.Amounts.Any(amount => amount == null
                || !Enum.IsDefined(typeof(ExcelSummaryField), amount.Field)
                || !Enum.IsDefined(typeof(ExcelSummaryRowKind), amount.RowKind)
                || amount.WorksheetName != ExcelSummaryReader.SummaryWorksheetName
                || string.IsNullOrWhiteSpace(amount.CellAddress))
                || result.Amounts.GroupBy(amount => amount.CellAddress, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "Excel 字段、行类型或工作表/单元格来源存在歧义。";
            return null;
        }

        public static ExcelSummaryAmount FindExcelAmount(ExcelSummaryReadResult result, WordConclusionSourceKind kind,
            ValueCategory category, TaxBasis tax, out string reason)
        {
            ExcelSummaryRowKind row = kind == WordConclusionSourceKind.TableAssetRow ? ExcelSummaryRowKind.Asset : ExcelSummaryRowKind.Total;
            ExcelSummaryField field = category == ValueCategory.AppraisedOriginalValue
                ? (tax == TaxBasis.TaxExcluded ? ExcelSummaryField.AppraisedOriginalValueTaxExcluded : ExcelSummaryField.AppraisedOriginalValueTaxIncluded)
                : (tax == TaxBasis.TaxExcluded ? ExcelSummaryField.AppraisedNetValueTaxExcluded : ExcelSummaryField.AppraisedNetValueTaxIncluded);
            ExcelSummaryAmount[] matches = result.Amounts.Where(amount => amount.RowKind == row && amount.Field == field).ToArray();
            reason = matches.Length == 1 ? null : matches.Length == 0
                ? "缺少对应字段及行类型的 Excel 金额。" : "对应字段及行类型的 Excel 金额不唯一。";
            return matches.Length == 1 ? matches[0] : null;
        }

        private static bool IsValidWordAmount(WordConclusionAmount amount, WordConclusion conclusion)
        {
            if (amount == null || amount.Target != conclusion.Target || amount.SourceDocument != conclusion.SourceDocument
                || string.IsNullOrWhiteSpace(amount.RawText)
                || (amount.ValueCategory != ValueCategory.AppraisedOriginalValue && amount.ValueCategory != ValueCategory.AppraisedNetValue)
                || (amount.TaxBasis != TaxBasis.TaxExcluded && amount.TaxBasis != TaxBasis.TaxIncluded)
                || !Enum.IsDefined(typeof(WordConclusionSourceKind), amount.SourceKind) || amount.Location == null)
                return false;
            bool body = amount.SourceKind == WordConclusionSourceKind.BodyArabic || amount.SourceKind == WordConclusionSourceKind.BodyChineseFinancial;
            if (body)
                return amount.ValueCategory == ValueCategory.AppraisedNetValue && amount.Location.ParagraphIndex.HasValue
                    && !amount.Location.TableIndex.HasValue
                    && amount.NumberFormat == (amount.SourceKind == WordConclusionSourceKind.BodyArabic
                        ? MoneyNumberFormat.Arabic : MoneyNumberFormat.ChineseFinancial);
            return amount.Location.TableIndex == conclusion.TableIndex && amount.Location.RowIndex.HasValue
                && amount.Location.CellIndex.HasValue;
        }
    }
}
