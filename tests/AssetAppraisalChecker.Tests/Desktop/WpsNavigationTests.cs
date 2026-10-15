using System;
using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class WpsNavigationTests
    {
        [TestMethod]
        public void RegisteredWps_IsPreferredToWord()
        {
            var wps = new RecordingAdapter(SourceNavigationStatus.Exact);
            var word = new RecordingAdapter(SourceNavigationStatus.Exact);
            var result = new WpsPreferredNavigationAdapter(() => true, wps, word).Navigate(null, CancellationToken.None);
            Assert.AreEqual(SourceNavigationStatus.Exact, result.Status);
            Assert.AreEqual(1, wps.Calls);
            Assert.AreEqual(0, word.Calls);
        }

        [TestMethod]
        public void UnregisteredWps_KeepsOptionalWordSupport()
        {
            var wps = new RecordingAdapter(SourceNavigationStatus.Exact);
            var word = new RecordingAdapter(SourceNavigationStatus.ParagraphOnly);
            var result = new WpsPreferredNavigationAdapter(() => false, wps, word).Navigate(null, CancellationToken.None);
            Assert.AreEqual(SourceNavigationStatus.ParagraphOnly, result.Status);
            Assert.AreEqual(0, wps.Calls);
            Assert.AreEqual(1, word.Calls);
        }

        [TestMethod]
        public void RegisteredButFailingWps_DoesNotSilentlySwitchEditors()
        {
            var word = new RecordingAdapter(SourceNavigationStatus.Exact);
            var result = new WpsPreferredNavigationAdapter(() => true,
                new RecordingAdapter(SourceNavigationStatus.Unavailable), word).Navigate(null, CancellationToken.None);
            Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
            Assert.AreEqual(0, word.Calls);
        }

        [TestMethod]
        public void WpsException_DoesNotFallBackToWord()
        {
            var word = new RecordingAdapter(SourceNavigationStatus.Exact);
            Assert.ThrowsExactly<InvalidOperationException>(() => new WpsPreferredNavigationAdapter(
                () => true, new ThrowingAdapter(), word).Navigate(null, CancellationToken.None));
            Assert.AreEqual(0, word.Calls);
        }

        [TestMethod]
        public async Task WpsWithoutLocalInterface_IsNotReportedAsLocated()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("合成定位正文。")))))
            {
                var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                var result = await new SourceNavigationService(new WpsNavigationAdapter(() => null))
                    .NavigateAsync(source, SourceNavigationTests.At(source, 0, "定位正文"));
                Assert.AreEqual(SourceNavigationStatus.Unavailable, result.Status);
                StringAssert.Contains(result.Message, "WPS");
                StringAssert.Contains(result.Message, "无法使用 WPS 精确定位功能");
                Assert.IsTrue(source.IsUnchanged());
            }
        }

        [TestMethod]
        public void WpsAdapter_RequiresSta()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { new WpsNavigationAdapter(() => null).Navigate(null, CancellationToken.None); }
                catch (Exception e) { failure = e; }
            });
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            thread.Join();
            Assert.IsInstanceOfType<InvalidOperationException>(failure);
        }

        [TestMethod]
        public void InvisibleCharacters_AreNeverTrimmedInWpsRangeMatching()
        {
            Assert.AreEqual("合成  正文\t", WordTextMatcher.ContainerText("合成  正文\t\r", false));
            Assert.AreEqual("合成  正文\t", WordTextMatcher.ContainerText("合成  正文\t\r\a", true));
        }

        private sealed class RecordingAdapter : IWordNavigationAdapter
        {
            private readonly SourceNavigationStatus _status;
            public RecordingAdapter(SourceNavigationStatus status) { _status = status; }
            public int Calls { get; private set; }
            public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken token)
            {
                Calls++;
                return new SourceNavigationResult(_status, "测试适配器，不代表真实 WPS 验证。");
            }
        }
        private sealed class ThrowingAdapter : IWordNavigationAdapter
        {
            public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken token) =>
                throw new InvalidOperationException("测试接口不可用");
        }
    }
}
