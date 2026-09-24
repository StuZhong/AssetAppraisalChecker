using System;
using AssetAppraisalChecker.Core.Consistency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class MoneyToleranceTests
    {
        [TestMethod]
        public void Presets_UseExpectedDecimalYuanAmounts()
        {
            Assert.AreEqual(0m, MoneyTolerance.Exact.AmountYuan);
            Assert.AreEqual(1m, MoneyTolerance.OneYuan.AmountYuan);
            Assert.AreEqual(10m, MoneyTolerance.TenYuan.AmountYuan);
            Assert.AreEqual(100m, MoneyTolerance.HundredYuan.AmountYuan);
            Assert.AreEqual(100m, MoneyTolerance.OneHundredthTenThousandYuan.AmountYuan);
        }

        [TestMethod]
        public void Constructor_NegativeTolerance_Throws()
        {
            try
            {
                new MoneyTolerance(-0.01m);
                Assert.Fail("负数金额容差应抛出 ArgumentOutOfRangeException。");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [TestMethod]
        public void ExactPreset_SubYuanDifference_ProducesConflict()
        {
            var engine = new ConsistencyEngine(MoneyTolerance.Exact);

            ConsistencyCheckResult result = engine.Check(new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1000000m, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1000000.01m, order: 1)
            });

            Assert.AreEqual(1, result.Issues.Count);
        }

        [TestMethod]
        public void TenYuanPreset_TenYuanDifference_IsConsistent()
        {
            var engine = new ConsistencyEngine(MoneyTolerance.TenYuan);

            ConsistencyCheckResult result = engine.Check(new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1000000m, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1000010m, order: 1)
            });

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void HundredYuanPreset_OneHundredAndOneYuanDifference_ProducesConflict()
        {
            var engine = new ConsistencyEngine(MoneyTolerance.HundredYuan);

            ConsistencyCheckResult result = engine.Check(new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1000000m, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1000101m, order: 1)
            });

            Assert.AreEqual(1, result.Issues.Count);
        }
    }
}
