using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests
{
    [TestClass]
    public sealed class Phase0SmokeTests
    {
        [TestMethod]
        [Description("验证 Phase 0 测试基础设施能够发现并运行测试。")]
        public void 测试基础设施应能正常运行()
        {
            Assert.IsTrue(Environment.Version.Major > 0);
        }
    }
}
