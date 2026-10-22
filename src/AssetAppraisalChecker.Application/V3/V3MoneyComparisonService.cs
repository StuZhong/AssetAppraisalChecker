using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Document.Excel;
using AssetAppraisalChecker.Facts.Extraction.Conclusions;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Application.V3
{
    public sealed class V3MoneyComparisonService
    {
        public V3MoneyComparisonResult Check(string reportPath, string explanationPath, string excelPath)
        {
            var reader = new DocxDocumentReader();
            var extractor = new WordConclusionExtractor();
            WordConclusionExtractionResult report = extractor.Extract(reader.Read(reportPath), reportPath, WordConclusionDocumentKind.Report);
            WordConclusionExtractionResult explanation = extractor.Extract(reader.Read(explanationPath), explanationPath, WordConclusionDocumentKind.Explanation);
            ExcelSummaryReadResult excel = new ExcelSummaryReader().Read(excelPath);
            return CompareCore(report, explanation, excel, excelPath, reportPath, explanationPath);
        }

        public V3MoneyComparisonResult Compare(WordConclusionExtractionResult report, WordConclusionExtractionResult explanation,
            ExcelSummaryReadResult excel, string excelSourceDocument) => CompareCore(report, explanation, excel, excelSourceDocument,
                V3MoneyCorrespondence.FindWordSource(report), V3MoneyCorrespondence.FindWordSource(explanation));

        private static V3MoneyComparisonResult CompareCore(WordConclusionExtractionResult report, WordConclusionExtractionResult explanation,
            ExcelSummaryReadResult excel, string excelSource, string reportSource, string explanationSource)
        {
            var items = new List<V3MoneyComparisonItem>();
            string excelProblem = V3MoneyCorrespondence.ExcelProblem(excel, excelSource);
            foreach (WordConclusionTarget target in new[] { WordConclusionTarget.ReportSummary, WordConclusionTarget.ReportChapterTen,
                WordConclusionTarget.ExplanationChapterFour })
            {
                bool isExplanation = target == WordConclusionTarget.ExplanationChapterFour;
                string source = isExplanation ? explanationSource : reportSource;
                string conclusionProblem;
                WordConclusion conclusion = V3MoneyCorrespondence.FindConclusion(isExplanation ? explanation : report,
                    target, source, out conclusionProblem);
                foreach (WordConclusionSourceKind kind in Enum.GetValues(typeof(WordConclusionSourceKind)))
                {
                    bool body = kind == WordConclusionSourceKind.BodyArabic || kind == WordConclusionSourceKind.BodyChineseFinancial;
                    foreach (ValueCategory category in body ? new[] { ValueCategory.AppraisedNetValue }
                        : new[] { ValueCategory.AppraisedOriginalValue, ValueCategory.AppraisedNetValue })
                        foreach (TaxBasis tax in new[] { TaxBasis.TaxExcluded, TaxBasis.TaxIncluded })
                            items.Add(CompareOne(target, kind, category, tax, source, excelSource,
                                conclusion, excel, conclusionProblem, excelProblem));
                }
            }
            return new V3MoneyComparisonResult(items);
        }

        private static V3MoneyComparisonItem CompareOne(WordConclusionTarget target, WordConclusionSourceKind kind,
            ValueCategory category, TaxBasis tax, string wordSource, string excelSource, WordConclusion conclusion,
            ExcelSummaryReadResult excel, string conclusionProblem, string excelProblem)
        {
            string wordProblem;
            WordConclusionAmount word = V3MoneyCorrespondence.FindWordAmount(conclusion, kind, category, tax, out wordProblem);
            string referenceProblem = excelProblem;
            ExcelSummaryAmount reference = null;
            if (referenceProblem == null)
                reference = V3MoneyCorrespondence.FindExcelAmount(excel, kind, category, tax, out referenceProblem);
            string reason = string.Join("；", new[] { conclusionProblem, wordProblem, referenceProblem }.Where(text => !string.IsNullOrEmpty(text)));
            decimal? comparisonYuan = null;
            decimal? difference = null;
            V3MoneyComparisonStatus status = V3MoneyComparisonStatus.Indeterminate;
            try
            {
                // Task 02 owns the confirmed report precision. Convert that amount, never
                // the higher-precision raw cache, to the yuan unit already used by Task 03.
                if (reference != null) comparisonYuan = reference.RoundedAmountTenThousandYuan * 10000m;
                if (word != null && comparisonYuan.HasValue && reason.Length == 0)
                {
                    difference = Math.Abs(word.NormalizedAmountYuan - comparisonYuan.Value);
                    // No V1 tolerance, re-rounding of Word values, or majority voting applies here.
                    status = difference.Value == 0m ? V3MoneyComparisonStatus.Consistent : V3MoneyComparisonStatus.Inconsistent;
                    reason = status == V3MoneyComparisonStatus.Consistent
                        ? "字段、税务口径及来源行明确对应；Word 标准元金额与 Excel 两位小数万元比较金额一致。"
                        : string.Format(CultureInfo.InvariantCulture,
                            "{0} / {1}：{2}{3}，Word {4:G29} 元与 Excel {5:G29} 元不一致，差额 {6:G29} 元。Excel 来源：{7}!{8}，按两位小数万元精确比较。",
                            TargetName(target), SourceName(kind), tax == TaxBasis.TaxExcluded ? "不含税" : "含税",
                            category == ValueCategory.AppraisedOriginalValue ? "评估原值" : "评估净值",
                            word.NormalizedAmountYuan, comparisonYuan.Value, difference.Value, reference.WorksheetName, reference.CellAddress);
                }
            }
            catch (OverflowException)
            {
                difference = null;
                status = V3MoneyComparisonStatus.Indeterminate;
                reason = "金额换算或差额超出 decimal 可表示范围，无法可靠比较。";
            }
            if (status == V3MoneyComparisonStatus.Indeterminate)
                reason = TargetName(target) + " / " + SourceName(kind) + "：无法判定；" + reason;
            return new V3MoneyComparisonItem(target, kind, category, tax, wordSource, excelSource,
                word, reference, comparisonYuan, difference, status, reason);
        }

        private static string TargetName(WordConclusionTarget target) => target == WordConclusionTarget.ReportSummary
            ? "评估报告摘要" : target == WordConclusionTarget.ReportChapterTen ? "评估报告正文第十章评估结论" : "评估说明第四章评估结论";

        private static string SourceName(WordConclusionSourceKind kind) => kind == WordConclusionSourceKind.BodyArabic
            ? "正文阿拉伯金额" : kind == WordConclusionSourceKind.BodyChineseFinancial ? "正文中文大写金额"
            : kind == WordConclusionSourceKind.TableAssetRow ? "表格资产行" : "表格合计行";
    }
}
