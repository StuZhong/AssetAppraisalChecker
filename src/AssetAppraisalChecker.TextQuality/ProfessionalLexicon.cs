using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class ProfessionalLexicon
    {
        private static readonly Lazy<ProfessionalLexicon> DefaultValue =
            new Lazy<ProfessionalLexicon>(CreateDefault, true);

        private readonly IReadOnlyDictionary<int, IReadOnlyList<string>> _lengthIndex;
        private readonly IReadOnlyDictionary<string, ProfessionalTerm> _byValue;

        public ProfessionalLexicon(string version, IEnumerable<string> terms)
            : this(
                version,
                (terms ?? throw new ArgumentNullException(nameof(terms)))
                    .Select(item => new ProfessionalTerm(
                        item,
                        ProfessionalTermCategory.OtherProfessional)))
        {
        }

        public ProfessionalLexicon(string version, IEnumerable<ProfessionalTerm> entries)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("词典版本不能为空。", nameof(version));
            }

            Version = version;
            Entries = new ReadOnlyCollection<ProfessionalTerm>(
                (entries ?? throw new ArgumentNullException(nameof(entries)))
                    .GroupBy(item => item.Value, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(item => item.Value, StringComparer.Ordinal)
                    .ToList());
            Terms = new ReadOnlyCollection<string>(Entries.Select(item => item.Value).ToList());
            _byValue = Entries.ToDictionary(item => item.Value, StringComparer.Ordinal);
            _lengthIndex = Entries
                .Where(item => item.Value.Length >= 4)
                .GroupBy(item => item.Value.Length)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)new ReadOnlyCollection<string>(
                        group.Select(item => item.Value)
                            .OrderBy(item => item, StringComparer.Ordinal)
                            .ToList()));
        }

        public static ProfessionalLexicon Default => DefaultValue.Value;

        public string Version { get; }

        public IReadOnlyList<ProfessionalTerm> Entries { get; }

        public IReadOnlyList<string> Terms { get; }

        public bool Contains(string value)
        {
            return value != null && _byValue.ContainsKey(value);
        }

        public ProfessionalTermCategory GetCategory(string value)
        {
            return value != null && _byValue.TryGetValue(value, out ProfessionalTerm term)
                ? term.Category
                : ProfessionalTermCategory.OtherProfessional;
        }

        public IReadOnlyList<string> GetNearMatchTerms(char firstCharacter, int candidateLength)
        {
            return new ReadOnlyCollection<string>(GetNearMatchTerms(candidateLength)
                .Where(item => item.Length > 0 && item[0] == firstCharacter)
                .ToList());
        }

        public IReadOnlyList<string> GetNearMatchTerms(int candidateLength)
        {
            var values = new List<string>();
            for (int length = candidateLength - 1; length <= candidateLength + 1; length++)
            {
                if (length >= 4 && _lengthIndex.TryGetValue(length, out IReadOnlyList<string> terms))
                {
                    values.AddRange(terms);
                }
            }

            return new ReadOnlyCollection<string>(values);
        }

        private static ProfessionalLexicon CreateDefault()
        {
            return new ProfessionalLexicon(
                "v2-text-quality-professional/2.0",
                new[]
                {
                    T("项目名称", ProfessionalTermCategory.ReportField), T("报告编号", ProfessionalTermCategory.ReportField),
                    T("评估基准日", ProfessionalTermCategory.ReportField), T("评估目的", ProfessionalTermCategory.ReportField),
                    T("评估对象", ProfessionalTermCategory.ReportField), T("评估范围", ProfessionalTermCategory.ReportField),
                    T("评估方法", ProfessionalTermCategory.ReportField), T("评估结论", ProfessionalTermCategory.ReportField),
                    T("评估价值", ProfessionalTermCategory.ReportField), T("评估原值", ProfessionalTermCategory.ReportField),
                    T("评估净值", ProfessionalTermCategory.ReportField), T("账面原值", ProfessionalTermCategory.ReportField),
                    T("账面净值", ProfessionalTermCategory.ReportField), T("委托人", ProfessionalTermCategory.ReportField),
                    T("产权持有人", ProfessionalTermCategory.ReportField), T("评估机构", ProfessionalTermCategory.ReportField),
                    T("法定代表人", ProfessionalTermCategory.ReportField), T("价值类型", ProfessionalTermCategory.ReportField),

                    T("市场法", ProfessionalTermCategory.ValuationMethod), T("收益法", ProfessionalTermCategory.ValuationMethod),
                    T("成本法", ProfessionalTermCategory.ValuationMethod), T("重置成本法", ProfessionalTermCategory.ValuationMethod),
                    T("资产基础法", ProfessionalTermCategory.ValuationMethod), T("现行重建成本", ProfessionalTermCategory.ValuationMethod),
                    T("重建成本测算", ProfessionalTermCategory.ValuationMethod), T("重置成本途径", ProfessionalTermCategory.ValuationMethod),

                    T("资产评估", ProfessionalTermCategory.ValuationConcept), T("经济行为", ProfessionalTermCategory.ValuationConcept),
                    T("评估结果", ProfessionalTermCategory.ValuationConcept), T("评估总值", ProfessionalTermCategory.ValuationConcept),
                    T("整体评估结果", ProfessionalTermCategory.ValuationConcept),
                    T("全部评估对象", ProfessionalTermCategory.ValuationConcept), T("单项资产", ProfessionalTermCategory.ValuationConcept),
                    T("评估假设", ProfessionalTermCategory.ValuationConcept), T("评估依据", ProfessionalTermCategory.ValuationConcept),
                    T("评估程序", ProfessionalTermCategory.ValuationConcept), T("评估参数", ProfessionalTermCategory.ValuationConcept),
                    T("成新率", ProfessionalTermCategory.ValuationConcept), T("可比实例", ProfessionalTermCategory.ValuationConcept),
                    T("收益期限", ProfessionalTermCategory.ValuationConcept), T("折现率", ProfessionalTermCategory.ValuationConcept),
                    T("价值时点", ProfessionalTermCategory.ValuationConcept), T("评估口径", ProfessionalTermCategory.ValuationConcept),

                    T("房屋建筑物", ProfessionalTermCategory.AssetType), T("机器设备", ProfessionalTermCategory.AssetType),
                    T("土地使用权", ProfessionalTermCategory.AssetType), T("供配电资产", ProfessionalTermCategory.AssetType),
                    T("整体资产组", ProfessionalTermCategory.AssetType), T("企业整体", ProfessionalTermCategory.AssetType),
                    T("流动资产", ProfessionalTermCategory.AssetType), T("非流动资产", ProfessionalTermCategory.AssetType),
                    T("无形资产", ProfessionalTermCategory.AssetType), T("在建工程", ProfessionalTermCategory.AssetType),
                    T("长期股权投资", ProfessionalTermCategory.AssetType), T("车辆设备", ProfessionalTermCategory.AssetType),

                    T("委托合同", ProfessionalTermCategory.LegalBusinessTerm), T("资产处置", ProfessionalTermCategory.LegalBusinessTerm),
                    T("资产转让", ProfessionalTermCategory.LegalBusinessTerm), T("资产购买", ProfessionalTermCategory.LegalBusinessTerm),
                    T("资产投资", ProfessionalTermCategory.LegalBusinessTerm), T("资产接收", ProfessionalTermCategory.LegalBusinessTerm),
                    T("所有权人", ProfessionalTermCategory.LegalBusinessTerm), T("权利人", ProfessionalTermCategory.LegalBusinessTerm),
                    T("报告使用人", ProfessionalTermCategory.LegalBusinessTerm), T("评估专业人员", ProfessionalTermCategory.LegalBusinessTerm),
                    T("签字资产评估师", ProfessionalTermCategory.LegalBusinessTerm), T("委托单位", ProfessionalTermCategory.LegalBusinessTerm),

                    T("账面价值", ProfessionalTermCategory.FinancialTerm), T("评估增值", ProfessionalTermCategory.FinancialTerm),
                    T("评估减值", ProfessionalTermCategory.FinancialTerm), T("评估增值额", ProfessionalTermCategory.FinancialTerm),
                    T("评估减值额", ProfessionalTermCategory.FinancialTerm), T("重置成本", ProfessionalTermCategory.FinancialTerm),
                    T("资金成本", ProfessionalTermCategory.FinancialTerm), T("建筑安装工程费", ProfessionalTermCategory.FinancialTerm),
                    T("增值税额", ProfessionalTermCategory.FinancialTerm), T("不含增值税", ProfessionalTermCategory.FinancialTerm),
                    T("含增值税", ProfessionalTermCategory.FinancialTerm), T("不含税评估值", ProfessionalTermCategory.FinancialTerm),
                    T("含税评估值", ProfessionalTermCategory.FinancialTerm), T("评估值原值", ProfessionalTermCategory.FinancialTerm),
                    T("项目总值", ProfessionalTermCategory.FinancialTerm), T("资产原值", ProfessionalTermCategory.FinancialTerm),
                    T("资产净值", ProfessionalTermCategory.FinancialTerm), T("折旧年限", ProfessionalTermCategory.FinancialTerm),
                    T("净现金流量", ProfessionalTermCategory.FinancialTerm),

                    T("评估报告", ProfessionalTermCategory.DocumentTerm), T("评估说明", ProfessionalTermCategory.DocumentTerm),
                    T("资产评估报告", ProfessionalTermCategory.DocumentTerm),
                    T("资产评估报告", ProfessionalTermCategory.DocumentTerm),
                    T("报告摘要", ProfessionalTermCategory.DocumentTerm), T("评估结果表", ProfessionalTermCategory.DocumentTerm),
                    T("评估结果汇总", ProfessionalTermCategory.DocumentTerm), T("评估结果合计", ProfessionalTermCategory.DocumentTerm),
                    T("计算过程", ProfessionalTermCategory.DocumentTerm), T("报告正文", ProfessionalTermCategory.DocumentTerm),
                    T("报告附件", ProfessionalTermCategory.DocumentTerm), T("资产清单", ProfessionalTermCategory.DocumentTerm),
                    T("评估明细表", ProfessionalTermCategory.DocumentTerm), T("报告声明", ProfessionalTermCategory.DocumentTerm),
                    T("特别事项说明", ProfessionalTermCategory.DocumentTerm),

                    T("金额单位", ProfessionalTermCategory.OtherProfessional), T("计量单位", ProfessionalTermCategory.OtherProfessional),
                    T("使用年限", ProfessionalTermCategory.OtherProfessional), T("剩余年限", ProfessionalTermCategory.OtherProfessional),
                    T("市场价值", ProfessionalTermCategory.OtherProfessional), T("公允价值", ProfessionalTermCategory.OtherProfessional)
                });
        }

        private static ProfessionalTerm T(string value, ProfessionalTermCategory category)
        {
            return new ProfessionalTerm(value, category);
        }
    }
}
