using AssetAppraisalChecker.Core.Money;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Money
{
    [TestClass]
    public sealed class ParserBoundaryTests
    {
        [TestMethod]
        [DataRow("12,34元")]
        [DataRow("1,00,000元")]
        [DataRow("100")]
        public void 阿拉伯解析器应拒绝无效千分位或无金额标识文本(string input)
        {
            MoneyParseResult result = ArabicMoneyParser.Parse(input);

            Assert.AreEqual(MoneyParseStatus.Failed, result.Status);
            Assert.IsFalse(result.NormalizedAmountYuan.HasValue);
        }

        [TestMethod]
        public void 阿拉伯金额换算溢出时应安全失败()
        {
            MoneyParseResult result = ArabicMoneyParser.Parse("79228162514264337593543950335亿元");

            Assert.AreEqual(MoneyParseStatus.Failed, result.Status);
            Assert.IsFalse(result.NormalizedAmountYuan.HasValue);
            StringAssert.Contains(result.FailureReason, "decimal");
        }

        [TestMethod]
        [DataRow("一百百元")]
        [DataRow("十百元")]
        [DataRow("百元")]
        [DataRow("元角")]
        public void 中文解析器应拒绝无法可靠解释的结构(string input)
        {
            MoneyParseResult result = ChineseMoneyParser.Parse(input);

            Assert.AreEqual(MoneyParseStatus.Failed, result.Status);
            Assert.IsFalse(result.NormalizedAmountYuan.HasValue);
        }
    }
}
