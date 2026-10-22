using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Facts.Extraction.Conclusions
{
    /// <summary>Extracts the three confirmed V3 conclusion positions; does not compare their values.</summary>
    public sealed class WordConclusionExtractor
    {
        public WordConclusionExtractionResult Extract(DocumentReadResult document, string sourceDocument,
            WordConclusionDocumentKind documentKind)
        {
            if (document == null || string.IsNullOrWhiteSpace(sourceDocument)
                || !Enum.IsDefined(typeof(WordConclusionDocumentKind), documentKind))
                return WordConclusionExtractionResult.Failed(WordConclusionExtractionStatus.InvalidInput,
                    "需要有效的 Word 读取结果、文件来源和报告/说明类型。");
            if (!document.IsSuccess)
                return WordConclusionExtractionResult.Failed(WordConclusionExtractionStatus.DocumentReadFailed, document.Message);
            try
            {
                var conclusions = new List<WordConclusion>();
                foreach (ConclusionSection section in WordConclusionLocator.Locate(document, documentKind))
                {
                    try
                    {
                        ConclusionTableAmounts table = WordConclusionTableExtractor.Extract(section, sourceDocument);
                        IReadOnlyList<WordConclusionAmount> body = ExtractBody(section, sourceDocument, table.FirstOrder);
                        conclusions.Add(new WordConclusion(sourceDocument, section.Target, section.Heading.Location,
                            table.TableIndex, body.Concat(table.Amounts)));
                    }
                    catch (ConclusionExtractionException exception)
                    {
                        throw new ConclusionExtractionException(exception.Status, section.Target + "：" + exception.Message);
                    }
                }
                return WordConclusionExtractionResult.Succeeded(conclusions);
            }
            catch (ConclusionExtractionException exception)
            {
                // Incomplete results never masquerade as a complete set for a later comparison.
                return WordConclusionExtractionResult.Failed(exception.Status, exception.Message);
            }
        }

        private static IReadOnlyList<WordConclusionAmount> ExtractBody(ConclusionSection section, string source, int tableFirstOrder)
        {
            DocumentElement[] paragraphs = section.Elements.Where(element => !element.Location.TableIndex.HasValue
                && element.DocumentOrderIndex < tableFirstOrder
                // “人民币万元” in a unit declaration is metadata, not a Chinese amount.
                && !Regex.IsMatch(WordConclusionLocator.Normalize(element.Text), @"^金额单位[:：]?(?:人民币)?(?:亿元|万元|元)$",
                    RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))).ToArray();
            int candidates = paragraphs.Sum(element => MoneyCandidateExtractor.Extract(element.Text).Count);
            MoneyOccurrence[] occurrences = new MoneyOccurrenceExtractor().Extract(paragraphs).Where(occurrence =>
                (occurrence.FieldIdentity.ValueCategory == ValueCategory.AppraisedNetValue
                    || occurrence.FieldIdentity.ValueCategory == ValueCategory.AppraisedValue)
                && occurrence.ValueCategoryConfidence == ClassificationConfidence.High
                && occurrence.TaxBasisConfidence == ClassificationConfidence.High
                && occurrence.FieldIdentity.TaxBasis != TaxBasis.Unspecified).ToArray();
            if (candidates != 4 || occurrences.Length != 4)
                throw new ConclusionExtractionException(candidates > 4
                    ? WordConclusionExtractionStatus.AmbiguousAmounts : WordConclusionExtractionStatus.IncompleteAmounts,
                    "结论正文未能唯一提取两项阿拉伯金额及两项中文财务大写金额；请核对金额、税务口径及表达数量。");

            var amounts = new List<WordConclusionAmount>();
            foreach (TaxBasis tax in new[] { TaxBasis.TaxExcluded, TaxBasis.TaxIncluded })
            {
                foreach (MoneyNumberFormat format in new[] { MoneyNumberFormat.Arabic, MoneyNumberFormat.ChineseFinancial })
                {
                    MoneyOccurrence[] matches = occurrences.Where(item => item.FieldIdentity.TaxBasis == tax
                        && item.MoneyNumberFormat == format).ToArray();
                    if (matches.Length != 1)
                        throw new ConclusionExtractionException(WordConclusionExtractionStatus.AmbiguousAmounts,
                            "正文金额的税务口径或数字表达不能唯一对应，未按出现顺序强行配对。");
                    MoneyOccurrence occurrence = matches[0];
                    // Task 03 explicitly confirms that the final narrative's “评估值”
                    // is the net value in this template. This scoped adapter does not
                    // change the V1/V2 classifier or infer the role from equal amounts.
                    string evidence = occurrence.FieldIdentity.ValueCategory == ValueCategory.AppraisedValue
                        ? "已确认的三处结论模板：正文“评估值”对应评估净值；税务口径来自当前金额子句，不依据金额相同推断。"
                        : "正文明确标注评估净值；税务口径来自当前金额子句。";
                    amounts.Add(new WordConclusionAmount(source, section.Target,
                        format == MoneyNumberFormat.Arabic ? WordConclusionSourceKind.BodyArabic : WordConclusionSourceKind.BodyChineseFinancial,
                        ValueCategory.AppraisedNetValue, occurrence, occurrence.Location, evidence));
                }
            }
            return amounts;
        }
    }

    internal sealed class ConclusionExtractionException : Exception
    {
        public ConclusionExtractionException(WordConclusionExtractionStatus status, string message) : base(message) { Status = status; }
        public WordConclusionExtractionStatus Status { get; }
    }
}
