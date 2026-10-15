using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Desktop
{
    // These contract tests exercise the actual late-bound open helper, not real WPS.
    // Interactive WPS verification is reported separately.
    [TestClass]
    public sealed class WpsEditableNavigationTests
    {
        private static Type AdapterType => typeof(WpsNavigationAdapter).Assembly.GetType(
            "AssetAppraisalChecker.Desktop.Services.Navigation.OfficeComNavigationAdapter", true);

        [TestMethod]
        public async Task SavedEditorWriteHandle_AllowsVerifiedNavigationWithoutWriting()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("合成正文。。")))))
            {
                var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                var adapter = new SourceNavigationTests.RecordingWordAdapter();
                using (var editorHandle = new FileStream(file.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    var result = await new SourceNavigationService(adapter).NavigateAsync(source,
                        SourceNavigationTests.At(source, 0, "。。"));
                    Assert.AreEqual(SourceNavigationStatus.Exact, result.Status);
                    Assert.AreEqual(1, adapter.Calls);
                    Assert.IsTrue(source.IsUnchanged());
                }
            }
        }

        [TestMethod]
        public void ChangedSavedDocument_RejectsOldStructureSnapshot()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("原始合成正文。。")))))
            {
                var source = SourceNavigationTests.Capture(file.FilePath, "文档 A");
                var occurrence = SourceNavigationTests.At(source, 0, "。。");
                using (var saved = WordprocessingDocument.Open(file.FilePath, true))
                    saved.MainDocumentPart.Document.Body.GetFirstChild<Paragraph>().GetFirstChild<Run>()
                        .GetFirstChild<Text>().Text = "已经修改的合成正文。";
                Assert.IsFalse(source.IsUnchanged());
                Assert.ThrowsExactly<IOException>(() => new SourceLocationResolver().Resolve(source, occurrence));
            }
        }

        [TestMethod]
        public void WpsOpen_RequestsEditableOriginalWithoutRevertOrRepair()
        {
            var application = new FakeApplication { AutomationSecurity = 2 };
            var documents = new FakeDocuments(application);
            const string path = @"C:\Synthetic\DocumentB.docx";
            object result = Open(true, application, documents, path);
            Assert.AreSame(documents.Document, result);
            Assert.AreEqual(path, documents.Path);
            Assert.IsFalse(documents.ReadOnly);
            Assert.IsFalse(documents.Revert);
            Assert.IsFalse(documents.Repair);
            Assert.IsFalse(documents.AddToRecentFiles);
            Assert.AreEqual(3, documents.SecurityDuringOpen);
            Assert.AreEqual(2, application.AutomationSecurity);
            Assert.AreEqual(1, documents.OpenCalls);
        }

        [TestMethod]
        public void OptionalWordOpen_KeepsExistingReadOnlyPolicy()
        {
            var application = new FakeApplication();
            var documents = new FakeDocuments(application);
            Open(false, application, documents, @"C:\Synthetic\Word.docx");
            Assert.IsTrue(documents.ReadOnly);
            Assert.IsFalse(documents.Revert);
            Assert.IsFalse(documents.Repair);
        }

        [TestMethod]
        public void OpenFailure_RestoresMacroSecurityWithoutRetry()
        {
            var application = new FakeApplication { AutomationSecurity = 1 };
            var documents = new FakeDocuments(application) { Fail = true };
            Assert.ThrowsExactly<TargetInvocationException>(() =>
                Open(true, application, documents, @"C:\Synthetic\Denied.docx"));
            Assert.AreEqual(1, application.AutomationSecurity);
            Assert.AreEqual(1, documents.OpenCalls);
        }

        [TestMethod]
        public void EditorRestriction_IsNotChangedByEditableOpenRequest()
        {
            var application = new FakeApplication();
            var documents = new FakeDocuments(application);
            documents.Document.ReadOnly = true;
            Assert.AreSame(documents.Document, Open(true, application, documents, @"C:\Synthetic\ReadOnly.docx"));
            Assert.IsTrue(documents.Document.ReadOnly);
        }

        [TestMethod]
        [DataRow(true, -1, "仍为只读")]
        [DataRow(false, 3, "编辑保护")]
        [DataRow(false, -1, "手动标黄或修改")]
        public void NavigationNotice_ReportsRestrictionAndRequiresRecheckAfterSave(bool readOnly, int protection, string expected)
        {
            string notice = (string)AdapterType.GetMethod("EditingNotice", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { readOnly, (int?)protection });
            StringAssert.Contains(notice, expected);
            StringAssert.Contains(notice, "未修改或保存原文");
            StringAssert.Contains(notice, "重新校检");
            StringAssert.Contains(notice, "旧结果和位置可能已过期");
        }

        private static object Open(bool wps, FakeApplication application, FakeDocuments documents, string path)
        {
            object adapter = Activator.CreateInstance(AdapterType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { "Unused.Application", "Test", wps, null }, null);
            Type referencesType = AdapterType.GetNestedType("ComReferences", BindingFlags.NonPublic);
            using (var references = (IDisposable)Activator.CreateInstance(referencesType, new object[] { CancellationToken.None }))
            {
                return AdapterType.GetMethod("OpenOriginalDocument", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(adapter, new object[] { references, application, documents, path });
            }
        }

        public sealed class FakeApplication
        {
            public int AutomationSecurity { get; set; }
        }

        public sealed class FakeDocument
        {
            public bool ReadOnly { get; set; }
        }

        public sealed class FakeDocuments
        {
            private readonly FakeApplication _application;
            public FakeDocuments(FakeApplication application) { _application = application; }
            public FakeDocument Document { get; } = new FakeDocument();
            public string Path { get; private set; }
            public bool ReadOnly { get; private set; }
            public bool Revert { get; private set; }
            public bool Repair { get; private set; }
            public bool AddToRecentFiles { get; private set; }
            public int SecurityDuringOpen { get; private set; }
            public int OpenCalls { get; private set; }
            public bool Fail { get; set; }

            public object Open(object fileName, object confirmConversions = null, object readOnly = null, object addToRecentFiles = null,
                object passwordDocument = null, object passwordTemplate = null, object revert = null, object writePasswordDocument = null,
                object writePasswordTemplate = null, object format = null, object encoding = null, object visible = null, object openAndRepair = null,
                object optional14 = null, object optional15 = null, object optional16 = null)
            {
                OpenCalls++;
                Path = (string)fileName;
                ReadOnly = (bool)readOnly;
                Revert = (bool)revert;
                Repair = (bool)openAndRepair;
                AddToRecentFiles = (bool)addToRecentFiles;
                SecurityDuringOpen = _application.AutomationSecurity;
                if (Fail) throw new UnauthorizedAccessException("Synthetic restriction");
                return Document;
            }
        }
    }
}
