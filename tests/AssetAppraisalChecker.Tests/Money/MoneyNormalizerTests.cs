using System.Globalization;
using AssetAppraisalChecker.Core.Money;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Money
{
    [TestClass]
    public sealed class MoneyNormalizerTests
    {
        [TestMethod]
        [DataRow("人民币叁拾捌万壹仟玖佰元整", "381900.00")]
        [DataRow("三十八万一千九百元", "381900.00")]
        [DataRow("38.19万元", "381900.00")]
        [DataRow("381,900元", "381900.00")]
        [DataRow("381900元", "381900.00")]
        [DataRow("0.003819亿元", "381900.00")]
        [DataRow("人民币38.19万元", "381900.00")]
        [DataRow("RMB 38.19万元", "381900.00")]
        [DataRow("￥381900", "381900.00")]
        [DataRow("¥381900", "381900.00")]
        [DataRow("人民币壹佰柒拾万元整", "1700000.00")]
        [DataRow("壹佰柒拾万元", "1700000.00")]
        [DataRow("壹佰元伍角", "100.50")]
        [DataRow("壹佰元零伍分", "100.05")]
        [DataRow("38.1900万元", "381900.00")]
        [DataRow("壹亿元", "100000000.00")]
        [DataRow("一百七十万元", "1700000.00")]
        [DataRow("一百元五角", "100.50")]
        [DataRow("一百元零五分", "100.05")]
        public void 规范金额应解析为人民币元(string input, string expectedAmount)
        {
            MoneyParseResult result = MoneyNormalizer.Parse(input);

            Assert.AreEqual(MoneyParseStatus.Success, result.Status);
            Assert.IsTrue(result.NormalizedAmountYuan.HasValue);
            Assert.AreEqual(ParseDecimal(expectedAmount), result.NormalizedAmountYuan.Value);
            Assert.AreEqual(MoneyParseConfidence.High, result.Confidence);
        }

        [TestMethod]
        [DataRow("壹百柒拾万元")]
        [DataRow("人民币壹百柒拾万元整")]
        public void 可可靠识别的非规范财务金额应返回警告(string input)
        {
            MoneyParseResult result = MoneyNormalizer.Parse(input);

            Assert.AreEqual(MoneyParseStatus.SuccessWithWarning, result.Status);
            Assert.AreEqual(1700000.00m, result.NormalizedAmountYuan);
            Assert.AreEqual(MoneyNumberFormat.ChineseFinancial, result.NumberFormat);
            Assert.AreEqual(MoneyParseConfidence.Medium, result.Confidence);
            Assert.IsTrue(result.Warnings.Count > 0);
            StringAssert.Contains(result.Warnings[0], "使用‘百’而非‘佰’");
        }

        [TestMethod]
        public void 等值金额表达应得到相同标准金额()
        {
            string[] expressions =
            {
                "人民币叁拾捌万壹仟玖佰元整",
                "三十八万一千九百元",
                "38.19万元",
                "381,900元",
                "381900元",
                "0.003819亿元",
                "RMB 38.19万元",
                "￥381900"
            };

            foreach (string expression in expressions)
            {
                MoneyParseResult result = MoneyNormalizer.Parse(expression);

                Assert.IsTrue(result.IsSuccess, expression);
                Assert.AreEqual(381900.00m, result.NormalizedAmountYuan, expression);
            }
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("这不是金额")]
        [DataRow("壹佰猫元")]
        [DataRow("一二三元")]
        [DataRow("38,19元")]
        public void 无法构成金额的文本应安全失败(string input)
        {
            AssertFailedWithoutAmount(MoneyNormalizer.Parse(input));
        }

        [TestMethod]
        public void 空引用应安全失败()
        {
            AssertFailedWithoutAmount(MoneyNormalizer.Parse(null));
        }

        [TestMethod]
        public void 纯普通文字失败时不应伪造数字格式()
        {
            MoneyParseResult result = MoneyNormalizer.Parse("这不是金额");

            Assert.AreEqual(MoneyParseStatus.Failed, result.Status);
            Assert.AreEqual(MoneyNumberFormat.Unknown, result.NumberFormat);
        }

        [TestMethod]
        public void 零元是合法金额而不是失败标记()
        {
            MoneyParseResult result = MoneyNormalizer.Parse("0元");

            Assert.AreEqual(MoneyParseStatus.Success, result.Status);
            Assert.AreEqual(0.00m, result.NormalizedAmountYuan);
            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        [DataRow("两百元", "200.00")]
        [DataRow("一百〇五元", "105.00")]
        [DataRow("十二元", "12.00")]
        [DataRow("一亿零三万零五元", "100030005.00")]
        [DataRow("壹佰圆正", "100.00")]
        [DataRow("壹萬圆整", "10000.00")]
        [DataRow("壹億圆整", "100000000.00")]
        public void 中文金额扩展字符和层级单位应正确解析(string input, string expectedAmount)
        {
            MoneyParseResult result = MoneyNormalizer.Parse(input);

            Assert.IsTrue(result.IsSuccess, result.FailureReason);
            Assert.AreEqual(ParseDecimal(expectedAmount), result.NormalizedAmountYuan.Value);
        }

        [TestMethod]
        [DataRow("381900元", MoneyUnit.Yuan)]
        [DataRow("38.19万元", MoneyUnit.TenThousandYuan)]
        [DataRow("0.003819亿元", MoneyUnit.HundredMillionYuan)]
        public void 阿拉伯金额应记录原始单位(string input, MoneyUnit expectedUnit)
        {
            MoneyParseResult result = MoneyNormalizer.Parse(input);

            Assert.AreEqual(expectedUnit, result.DetectedUnit);
        }

        [TestMethod]
        [DataRow("381900元", MoneyNumberFormat.Arabic)]
        [DataRow("一百七十万元", MoneyNumberFormat.ChineseCommon)]
        [DataRow("壹佰柒拾万元", MoneyNumberFormat.ChineseFinancial)]
        public void 解析结果应记录数字格式(string input, MoneyNumberFormat expectedFormat)
        {
            MoneyParseResult result = MoneyNormalizer.Parse(input);

            Assert.AreEqual(expectedFormat, result.NumberFormat);
        }

        [TestMethod]
        public void 解析结果应保留未经修改的原文()
        {
            const string rawText = "  RMB 38.19万元  ";

            MoneyParseResult result = MoneyNormalizer.Parse(rawText);

            Assert.AreEqual(rawText, result.RawText);
            Assert.AreEqual(381900.00m, result.NormalizedAmountYuan);
        }

        [TestMethod]
        public void TryParse应区分成功与失败()
        {
            MoneyParseResult successResult;
            MoneyParseResult failedResult;

            bool success = MoneyNormalizer.TryParse("100元", out successResult);
            bool failed = MoneyNormalizer.TryParse("普通文字", out failedResult);

            Assert.IsTrue(success);
            Assert.AreEqual(100.00m, successResult.NormalizedAmountYuan);
            Assert.IsFalse(failed);
            Assert.IsFalse(failedResult.NormalizedAmountYuan.HasValue);
        }

        private static decimal ParseDecimal(string text)
        {
            return decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        private static void AssertFailedWithoutAmount(MoneyParseResult result)
        {
            Assert.AreEqual(MoneyParseStatus.Failed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.NormalizedAmountYuan.HasValue);
            Assert.AreEqual(MoneyUnit.Unknown, result.DetectedUnit);
            Assert.AreEqual(MoneyParseConfidence.None, result.Confidence);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.FailureReason));
        }
    }
}
