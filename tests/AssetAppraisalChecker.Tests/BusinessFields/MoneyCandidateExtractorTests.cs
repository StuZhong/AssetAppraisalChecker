using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class MoneyCandidateExtractorTests
    {
        [TestMethod]
        [DataRow("经评估，金额为人民币壹佰柒拾万元整。", "人民币壹佰柒拾万元整")]
        [DataRow("金额为壹佰柒拾万元。", "壹佰柒拾万元")]
        [DataRow("金额为三十八万一千九百元。", "三十八万一千九百元")]
        [DataRow("金额为RMB 38.19万元。", "RMB 38.19万元")]
        [DataRow("金额为￥381900。", "￥381900")]
        public void Extract_SupportedExpressions_ReturnsCompleteCandidate(string text, string expected)
        {
            var candidates = MoneyCandidateExtractor.Extract(text);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(expected, candidates[0].RawText);
            Assert.AreEqual(expected, text.Substring(candidates[0].StartIndex, candidates[0].Length));
        }

        [TestMethod]
        public void Extract_MultipleAmounts_ReturnsIndependentPositionsWithoutSwallowingText()
        {
            const string text = "评估净值为170万元，较账面价值增加20万元。";

            var candidates = MoneyCandidateExtractor.Extract(text);

            CollectionAssert.AreEqual(
                new[] { "170万元", "20万元" },
                candidates.Select(candidate => candidate.RawText).ToArray());
            foreach (MoneyCandidate candidate in candidates)
            {
                Assert.AreEqual(
                    candidate.RawText,
                    text.Substring(candidate.StartIndex, candidate.Length));
            }
        }

        [TestMethod]
        [DataRow("云测试评报字〔2026〕第001号")]
        [DataRow("评估基准日为2026年7月31日")]
        [DataRow("第15页第3条")]
        [DataRow("设备型号ABC-2026，电压220V，频率50Hz")]
        [DataRow("机器设备共10台，使用年限8年")]
        [DataRow("增值率10%，成新率85%，税率13%")]
        [DataRow("联系电话13800138000")]
        [DataRow("统一社会信用代码：TEST20260001")]
        [DataRow("错误金额格式38,19元")]
        public void Extract_NonMonetaryNumbers_ReturnsNoCandidates(string text)
        {
            Assert.AreEqual(0, MoneyCandidateExtractor.Extract(text).Count);
        }

        [TestMethod]
        public void Extract_QuantityAndMoney_ReturnsOnlyExplicitMoney()
        {
            const string text = "机器设备共10台，评估净值170万元。";

            var candidates = MoneyCandidateExtractor.Extract(text);

            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual("170万元", candidates[0].RawText);
        }
    }
}
