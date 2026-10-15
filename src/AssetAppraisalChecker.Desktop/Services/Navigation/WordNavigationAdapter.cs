using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Xml.Linq;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    /// <summary>Optional late-bound Word adapter. No Office package or runtime requirement for checking.</summary>
    public sealed class WordNavigationAdapter : IWordNavigationAdapter
    {
        private readonly OfficeComNavigationAdapter _adapter;
        public WordNavigationAdapter(Func<Type> applicationTypeResolver = null)
        {
            _adapter = new OfficeComNavigationAdapter("Word.Application", "Word", false, applicationTypeResolver);
        }
        public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken cancellationToken) =>
            _adapter.Navigate(target, cancellationToken);
    }

    // Shared, verified structure-to-range mapping. Editor activation and signatures are explicit;
    // WPS is not identified by Application.Name (WPS can report "Microsoft Word").
    internal sealed class OfficeComNavigationAdapter
    {
        private readonly Func<Type> _applicationTypeResolver;
        private readonly string _progId;
        private readonly string _editor;
        private readonly bool _wps;

        internal OfficeComNavigationAdapter(string progId, string editor, bool wps, Func<Type> applicationTypeResolver)
        {
            _progId = progId;
            _editor = editor;
            _wps = wps;
            _applicationTypeResolver = applicationTypeResolver ?? (() => Type.GetTypeFromProgID(progId));
        }

        public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken cancellationToken)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                throw new InvalidOperationException("编辑器定位必须在 STA 线程运行。");
            Type wordType = _applicationTypeResolver();
            if (wordType == null) return SourceNavigationResult.Unavailable(
                "无法使用 " + _editor + " 精确定位功能。当前未提供可用的本地自动化接口；可打开原文并复制片段手动查找。");
            using (var com = new ComReferences(cancellationToken))
            {
                // Word may have several application instances. A running file moniker lets us reuse
                // the already-open exact full-path document, rather than opening a same-name copy.
                object document = FindRunningDocument(target.Source.FullPath, com);
                object application;
                if (document != null) application = com.Get(document, "Application");
                else
                {
                    try { application = com.Track(Marshal.GetActiveObject(_progId)); }
                    catch (COMException)
                    {
                        application = com.Track(Activator.CreateInstance(wordType));
                        com.Set(application, "Visible", true);
                    }
                }
                if (!MatchesEditor(com, application)) return SourceNavigationResult.Unavailable(
                    "已连接的编辑器身份与 " + _editor + " 不符，未定位。");
                object documents = com.Get(application, "Documents");
                int count = Convert.ToInt32(com.Get(documents, "Count"));
                for (int i = 1; document == null && i <= count; i++)
                {
                    object candidate = com.Get(documents, "Item", i);
                    string path = Convert.ToString(com.Get(candidate, "FullName"));
                    if (WordTextMatcher.SameFullPath(path, target.Source.FullPath)) { document = candidate; break; }
                }
                if (document == null)
                {
                    document = OpenOriginalDocument(com, application, documents, target.Source.FullPath);
                    // Preserve the optional Word adapter's existing policy. WPS navigation
                    // requests normal editing, but never overrides editor/file restrictions.
                    if (!_wps && !Convert.ToBoolean(com.Get(document, "ReadOnly"))) return SourceNavigationResult.Unavailable(
                        _editor + " 未确认以只读方式打开，未定位。请手动以只读方式打开后再试。");
                }
                if (!Convert.ToBoolean(com.Get(document, "Saved"))) return Unsaved();
                if (!WordTextMatcher.SameFullPath(Convert.ToString(com.Get(document, "FullName")), target.Source.FullPath))
                    return SourceNavigationResult.Unavailable(_editor + " 中的文档身份无法确认，未定位。");

                TableNavigationSelection tableSelection = target.Location.TableIndex.HasValue
                    ? ResolveTable(com, document, target)
                    : null;
                object container = target.Location.TableIndex.HasValue
                    ? tableSelection?.Range
                    : ResolveParagraph(com, document, target);
                if (container == null) return SourceNavigationResult.Unavailable(
                    _editor + " 文字与校检结构无法交叉确认，未选择不确定位置。请打开原文手动查找。");

                object selection = container;
                bool exact = false;
                if (!target.DisplaySensitive && target.Offset.HasValue &&
                    (tableSelection == null || tableSelection.Status == SourceNavigationStatus.TableCell))
                {
                    string text = WordTextMatcher.ContainerText(Convert.ToString(com.Get(container, "Text")), target.Location.TableIndex.HasValue);
                    if (text == target.ContainerText)
                    {
                        int start = Convert.ToInt32(com.Get(container, "Start"));
                        object fragment = com.Get(container, "Duplicate");
                        com.Call(fragment, "SetRange", start + target.Offset.Value, start + target.Offset.Value + target.Length);
                        object prefix = com.Get(container, "Duplicate");
                        com.Call(prefix, "SetRange", start, start + target.Offset.Value);
                        // XML offsets are only a candidate. Actual Word range text must independently agree.
                        // Checking the prefix also prevents a UTF-16/Word character-count difference from
                        // selecting another identical fragment later in the same paragraph.
                        exact = WordTextMatcher.MatchesPrefix(target, Convert.ToString(com.Get(prefix, "Text"))) &&
                            WordTextMatcher.MatchesSpan(target, Convert.ToString(com.Get(fragment, "Text")));
                        if (exact) selection = fragment;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (!Convert.ToBoolean(com.Get(document, "Saved"))) return Unsaved();
                if (!target.Source.IsUnchanged()) return SourceNavigationResult.Unavailable("原文已经变化，请重新校检后定位。");
                com.Set(application, "Visible", true);
                com.Call(document, "Activate");
                com.Call(selection, "Select");
                object window = com.Get(document, "ActiveWindow");
                com.Call(window, "ScrollIntoView", selection, true);
                SourceNavigationStatus status = exact ? SourceNavigationStatus.Exact :
                    tableSelection?.Status ?? SourceNavigationStatus.ParagraphOnly;
                if (!SelectionMatches(com, application, selection, target.Source.FullPath,
                    target.Location.TableIndex.HasValue && !exact,
                    tableSelection?.Status == SourceNavigationStatus.TableRow))
                {
                    // WPS may expand a paragraph selection inside a cell. Only downgrade if
                    // the independently verified parent cell can itself be selected and read back.
                    if (tableSelection?.Status != SourceNavigationStatus.ParagraphOnly ||
                        tableSelection.FallbackCellRange == null) return SourceNavigationResult.Unavailable(
                            _editor + " 未确认实际选区，无法确认定位成功。");
                    selection = tableSelection.FallbackCellRange;
                    com.Call(selection, "Select");
                    com.Call(window, "ScrollIntoView", selection, true);
                    if (!SelectionMatches(com, application, selection, target.Source.FullPath, true))
                        return SourceNavigationResult.Unavailable(_editor + " 未确认实际选区，无法确认定位成功。");
                    status = SourceNavigationStatus.TableCell;
                }
                if (!Convert.ToBoolean(com.Get(document, "Saved"))) return Unsaved();
                string message;
                if (status == SourceNavigationStatus.Exact)
                    message = "已在 " + _editor + " 精确选中原文范围（不是页码定位）。";
                else if (status == SourceNavigationStatus.TableCell)
                    message = string.Format("已定位到表格 {0} / 第 {1} 行 / 第 {2} 列，请人工确认。",
                        target.Location.TableIndex.Value + 1, target.Location.RowIndex.Value + 1,
                        target.Location.CellIndex.Value + 1);
                else if (status == SourceNavigationStatus.TableRow)
                    message = string.Format("已定位到表格 {0} / 第 {1} 行，请人工确认具体单元格。",
                        target.Location.TableIndex.Value + 1, target.Location.RowIndex.Value + 1);
                else
                    message = target.DisplaySensitive
                        ? "已定位到相关段落，请人工确认；删除修订、隐藏文字或字段可能需要手动显示修订标记。"
                        : "已定位到相关段落，请人工确认。具体字符范围未获确认。";
                if (_wps)
                {
                    bool readOnly = Convert.ToBoolean(com.Get(document, "ReadOnly")) ||
                        (System.IO.File.GetAttributes(target.Source.FullPath) & System.IO.FileAttributes.ReadOnly) != 0;
                    message += EditingNotice(readOnly, ReadProtectionType(com, document));
                }
                return new SourceNavigationResult(status, message);
            }
        }

        private object OpenOriginalDocument(ComReferences com, object application, object documents, string path)
        {
            object originalSecurity = com.Get(application, "AutomationSecurity");
            try
            {
                com.Set(application, "AutomationSecurity", 3); // Disable macros only while opening.
                // No copy, Revert, repair, Save, Close or Quit. WPS opens the selected
                // original normally; ReadOnly=false is a request, not a permission bypass.
                return _wps
                    ? com.Call(documents, "Open", path, false, false, false,
                        Type.Missing, Type.Missing, false, Type.Missing, Type.Missing,
                        Type.Missing, Type.Missing, true, false, Type.Missing, true, Type.Missing)
                    : com.Call(documents, "Open", path, false, true, false,
                        Type.Missing, Type.Missing, false, Type.Missing, Type.Missing,
                        Type.Missing, Type.Missing, true, false, false, Type.Missing, true);
            }
            finally { com.SetUncancelled(application, "AutomationSecurity", originalSecurity); }
        }

        private static int? ReadProtectionType(ComReferences com, object document)
        {
            try { return Convert.ToInt32(com.Get(document, "ProtectionType")); }
            catch (Exception exception) when (IsUnsupportedProperty(exception)) { return null; }
        }

        private static bool IsUnsupportedProperty(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;
            return exception is MissingMemberException || exception is COMException &&
                (exception.HResult == unchecked((int)0x80020003) || exception.HResult == unchecked((int)0x80020006));
        }

        private static string EditingNotice(bool readOnly, int? protectionType)
        {
            string restriction = readOnly
                ? " 当前文档仍为只读，请检查文件属性、权限、占用或现有 WPS 打开状态；本工具不会解除限制或关闭重开文档。"
                : protectionType.HasValue && protectionType.Value != -1
                    ? " 当前文档存在编辑保护，编辑范围以 WPS 权限为准；本工具不会解除保护。"
                    : " 请在 WPS 中手动标黄或修改，编辑权限以 WPS 当前状态为准。";
            return restriction + " 定位操作未修改或保存原文；手动修改并保存后，请重新校检，旧结果和位置可能已过期。";
        }

        private static bool SelectionMatches(ComReferences com, object application, object selectedRange,
            string expectedPath, bool inTable, bool verifiedRow = false)
        {
            object actualSelection = com.Get(application, "Selection");
            object actualDocument = com.Get(actualSelection, "Document");
            return WordTextMatcher.SameFullPath(Convert.ToString(com.Get(actualDocument, "FullName")), expectedPath) &&
                Convert.ToInt32(com.Get(actualSelection, "Start")) == Convert.ToInt32(com.Get(selectedRange, "Start")) &&
                Convert.ToInt32(com.Get(actualSelection, "End")) == Convert.ToInt32(com.Get(selectedRange, "End")) &&
                Convert.ToInt32(com.Get(actualSelection, "StoryType")) == Convert.ToInt32(com.Get(selectedRange, "StoryType")) &&
                (WordTextMatcher.MatchesSelectedRangeText(
                    Convert.ToString(com.Get(selectedRange, "Text")),
                    Convert.ToString(com.Get(actualSelection, "Text")), inTable) ||
                 verifiedRow && WordTextMatcher.MatchesSelectedRowText(
                    Convert.ToString(com.Get(selectedRange, "Text")),
                    Convert.ToString(com.Get(actualSelection, "Text"))));
        }

        private SourceNavigationResult Unsaved() => SourceNavigationResult.Unavailable(
            _editor + " 中该文档存在未保存编辑，无法确认与旧校检一致。请保存并重新校检后定位。");

        private bool MatchesEditor(ComReferences com, object application)
        {
            string path = Convert.ToString(com.Get(application, "Path"));
            // Check the installation directory, not the user-visible application name or file name.
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(System.IO.Path.Combine(path, _wps ? "wps.exe" : "WINWORD.EXE"));
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx context);

        private object FindRunningDocument(string path, ComReferences com)
        {
            IRunningObjectTable table;
            IBindCtx context;
            if (GetRunningObjectTable(0, out table) != 0) return null;
            com.Track(table);
            if (CreateBindCtx(0, out context) != 0) return null;
            com.Track(context);
            IEnumMoniker enumerator;
            table.EnumRunning(out enumerator);
            com.Track(enumerator);
            var monikers = new IMoniker[1];
            while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                com.Track(monikers[0]);
                try
                {
                    string name;
                    monikers[0].GetDisplayName(context, null, out name);
                    // Do not obtain unrelated running objects (Excel, project tools, etc.).
                    if (!WordTextMatcher.SameFullPath(name, path)) continue;
                    object document;
                    table.GetObject(monikers[0], out document);
                    com.Track(document);
                    if (WordTextMatcher.SameFullPath(Convert.ToString(com.Get(document, "FullName")), path) &&
                        MatchesEditor(com, com.Get(document, "Application"))) return document;
                }
                catch (COMException) { /* ROT entries can disappear during enumeration; do not select them. */ }
                catch (ArgumentException) { /* Non-file moniker, not a candidate document. */ }
                catch (NotSupportedException) { /* Non-file moniker, not a candidate document. */ }
            }
            return null;
        }

        private static object ResolveParagraph(ComReferences com, object document, SourceNavigationTarget target)
        {
            object content = com.Get(document, "Content");
            object paragraphs = com.Get(content, "Paragraphs");
            var ranges = new List<object>();
            var texts = new List<string>();
            int count = Convert.ToInt32(com.Get(paragraphs, "Count"));
            for (int i = 1; i <= count; i++)
            {
                object range = com.Get(com.Get(paragraphs, "Item", i), "Range");
                if (Convert.ToBoolean(com.Get(range, "Information", 12))) continue; // wdWithInTable
                ranges.Add(range);
                texts.Add(WordTextMatcher.ContainerText(Convert.ToString(com.Get(range, "Text")), false));
            }
            var expected = target.Source.Document.Elements.Where(e => e.Location.ParagraphIndex.HasValue)
                .OrderBy(e => e.Location.ParagraphIndex.Value).Select(e => e.Text).ToList();
            int index = target.Location.ParagraphIndex.Value;
            if (index >= ranges.Count || expected.Count != texts.Count) return null;
            if (target.DisplaySensitive && !WordTextMatcher.MatchesVisibleParagraph(target, texts[index]))
            {
                // Cross-check the XML of this actual Word paragraph without changing revision/field view.
                string xml = Convert.ToString(com.Get(ranges[index], "WordOpenXML"));
                if (!SensitiveParagraphMatches(xml, target)) return null;
                texts[index] = expected[index];
            }
            else if (!target.DisplaySensitive && !string.Equals(expected[index], texts[index], StringComparison.Ordinal) &&
                WordTextMatcher.MatchesSectionBreakParagraph(target, texts[index],
                    Convert.ToString(com.Get(ranges[index], "WordOpenXML"))))
            {
                // Only paragraph-level mapping is confirmed; the actual range text retains
                // its section marker, so the exact-span path will not claim character precision.
                texts[index] = expected[index];
            }
            // Keep the strict whole-sequence path. If unrelated WPS display markers differ,
            // allow ONLY the same structural index with unique full text and exact neighbours.
            // No first-match Find, trimming, control-character deletion or offset conversion.
            return WordTextMatcher.MatchesParagraphAt(expected, texts, index) ? ranges[index] : null;
        }

        private static bool SensitiveParagraphMatches(string xml, SourceNavigationTarget target)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XElement body = XDocument.Parse(xml).Descendants(w + "body").SingleOrDefault();
            var paragraphs = body?.Elements(w + "p").ToList();
            if (paragraphs == null || paragraphs.Count != 1) return false;
            XElement paragraph = paragraphs[0];
            string visible = string.Concat(paragraph.Descendants().Select(e =>
                e.Name == w + "t" ? e.Value : e.Name == w + "tab" ? "\t" :
                e.Name == w + "br" || e.Name == w + "cr" ? "\n" : string.Empty));
            return visible == target.ContainerText &&
                (visible.Contains(target.Occurrence.RawText) || paragraph.Descendants(w + "del")
                    .Any(e => string.Concat(e.Descendants(w + "delText").Select(t => t.Value)).Contains(target.Occurrence.RawText)));
        }

        private static TableNavigationSelection ResolveTable(ComReferences com, object document, SourceNavigationTarget target)
        {
            object tables = com.Get(document, "Tables");
            var topTables = new List<object>();
            int count = Convert.ToInt32(com.Get(tables, "Count"));
            for (int i = 1; i <= count; i++)
            {
                object table = com.Get(tables, "Item", i);
                if (Convert.ToInt32(com.Get(table, "NestingLevel")) == 1) topTables.Add(table);
            }
            int index = target.Location.TableIndex.Value;
            if (topTables.Count != target.Source.Document.TableCount || index < 0 || index >= topTables.Count)
                return null;
            object selectedTable = topTables[index];
            object rows = com.Get(topTables[index], "Rows");
            if (Convert.ToInt32(com.Get(rows, "Count")) != target.RowCellCounts.Count) return null;
            int rowIndex = target.Location.RowIndex.Value;
            int cellIndex = target.Location.CellIndex.Value;
            var expected = target.Source.Document.Elements
                .Where(e => e.Location.TableIndex == index && e.Location.RowIndex == rowIndex)
                .OrderBy(e => e.Location.CellIndex).ToList();
            if (rowIndex < 0 || rowIndex >= target.RowCellCounts.Count ||
                cellIndex < 0 || cellIndex >= target.RowCellCounts[rowIndex] ||
                expected.Count != target.RowCellCounts[rowIndex]) return null;

            if (!target.TargetRowHasMerge)
            {
                try
                {
                    var ranges = new List<object>();
                    var actual = new List<string>();
                    bool sameCoordinates = true;
                    for (int cell = 0; cell < expected.Count; cell++)
                    {
                        object candidate = com.Call(selectedTable, "Cell", rowIndex + 1, cell + 1);
                        if (Convert.ToInt32(com.Get(candidate, "RowIndex")) != rowIndex + 1 ||
                            Convert.ToInt32(com.Get(candidate, "ColumnIndex")) != cell + 1)
                        {
                            sameCoordinates = false;
                            break;
                        }
                        object range = com.Get(candidate, "Range");
                        ranges.Add(range);
                        actual.Add(WordTextMatcher.ContainerText(Convert.ToString(com.Get(range, "Text")), true));
                    }
                    if (sameCoordinates && WordTextMatcher.MatchesSequence(expected.Select(e => e.Text).ToList(), actual))
                    {
                        object cellRange = ranges[cellIndex];
                        object paragraphRange = ResolveCellParagraph(com, cellRange, target);
                        return paragraphRange == null
                            ? new TableNavigationSelection(cellRange, SourceNavigationStatus.TableCell)
                            : new TableNavigationSelection(paragraphRange, SourceNavigationStatus.ParagraphOnly, cellRange);
                    }
                }
                catch (COMException) { /* WPS can reject Cell(row,column) for a merged row. */ }
                catch (TargetInvocationException exception) when (exception.InnerException is COMException)
                { /* Only a verified row may be used as the fallback. */ }
            }
            if (target.TargetCellOneToOne)
            {
                TableNavigationSelection verified = ResolveVerifiedCell(com, selectedTable, target, expected);
                if (verified != null) return verified;
            }
            try
            {
                object rowRange = com.Get(com.Get(rows, "Item", rowIndex + 1), "Range");
                return WordTextMatcher.MatchesRowRange(expected.Select(e => e.Text).ToList(),
                    Convert.ToString(com.Get(rowRange, "Text")))
                    ? new TableNavigationSelection(rowRange, SourceNavigationStatus.TableRow)
                    : null;
            }
            catch (COMException) { return null; }
            catch (TargetInvocationException exception) when (exception.InnerException is COMException)
            { return null; }
        }

        private static TableNavigationSelection ResolveVerifiedCell(ComReferences com, object table,
            SourceNavigationTarget target, IReadOnlyList<AssetAppraisalChecker.Core.Documents.DocumentElement> expected)
        {
            int row = target.Location.RowIndex.Value;
            int index = target.Location.CellIndex.Value;
            try
            {
                object cell = com.Call(table, "Cell", row + 1, index + 1);
                if (Convert.ToInt32(com.Get(cell, "RowIndex")) != row + 1 ||
                    Convert.ToInt32(com.Get(cell, "ColumnIndex")) != index + 1) return null;
                object range = com.Get(cell, "Range");
                if (!string.Equals(WordTextMatcher.ContainerText(Convert.ToString(com.Get(range, "Text")), true),
                    expected[index].Text, StringComparison.Ordinal)) return null;

                // Blank/repeated text is not an anchor. Confirm at least one other
                // non-empty cell at the same structural row and logical column.
                bool neighborConfirmed = false;
                for (int i = 0; i < expected.Count; i++)
                {
                    if (i == index || expected[i].Text.Length == 0) continue;
                    try
                    {
                        object neighbor = com.Call(table, "Cell", row + 1, i + 1);
                        if (Convert.ToInt32(com.Get(neighbor, "RowIndex")) != row + 1 ||
                            Convert.ToInt32(com.Get(neighbor, "ColumnIndex")) != i + 1) continue;
                        object neighborRange = com.Get(neighbor, "Range");
                        if (string.Equals(WordTextMatcher.ContainerText(
                            Convert.ToString(com.Get(neighborRange, "Text")), true),
                            expected[i].Text, StringComparison.Ordinal))
                        { neighborConfirmed = true; break; }
                    }
                    catch (COMException) { /* A merged neighbor is not corroborating evidence. */ }
                    catch (TargetInvocationException exception) when (exception.InnerException is COMException)
                    { /* A merged neighbor is not corroborating evidence. */ }
                }
                if (!neighborConfirmed && !(expected.Count == 1 && expected[index].Text.Length > 0)) return null;
                object paragraph = ResolveCellParagraph(com, range, target);
                return paragraph == null
                    ? new TableNavigationSelection(range, SourceNavigationStatus.TableCell)
                    : new TableNavigationSelection(paragraph, SourceNavigationStatus.ParagraphOnly, range);
            }
            catch (COMException) { return null; }
            catch (TargetInvocationException exception) when (exception.InnerException is COMException)
            { return null; }
        }

        private static object ResolveCellParagraph(ComReferences com, object cellRange, SourceNavigationTarget target)
        {
            if (!target.Location.ParagraphIndex.HasValue || target.ContainerText.Length == 0 ||
                target.CellParagraphTexts.Count == 0) return null;
            try
            {
                object paragraphs = com.Get(cellRange, "Paragraphs");
                if (Convert.ToInt32(com.Get(paragraphs, "Count")) != target.CellParagraphTexts.Count)
                    return null;
                var ranges = new List<object>();
                var actual = new List<string>();
                for (int i = 0; i < target.CellParagraphTexts.Count; i++)
                {
                    object range = com.Get(com.Get(paragraphs, "Item", i + 1), "Range");
                    ranges.Add(range);
                    actual.Add(WordTextMatcher.ParagraphText(Convert.ToString(com.Get(range, "Text"))));
                }
                return WordTextMatcher.MatchesSequence(target.CellParagraphTexts, actual)
                    ? ranges[target.Location.ParagraphIndex.Value]
                    : null;
            }
            catch (COMException) { return null; }
            catch (TargetInvocationException exception) when (exception.InnerException is COMException)
            { return null; }
        }

        private sealed class TableNavigationSelection
        {
            public TableNavigationSelection(object range, SourceNavigationStatus status, object fallbackCellRange = null)
            {
                Range = range;
                Status = status;
                FallbackCellRange = fallbackCellRange;
            }
            public object Range { get; }
            public SourceNavigationStatus Status { get; }
            public object FallbackCellRange { get; }
        }

        private sealed class ComReferences : IDisposable
        {
            private readonly List<object> _references = new List<object>();
            private readonly CancellationToken _cancellation;
            public ComReferences(CancellationToken cancellation) { _cancellation = cancellation; }
            public object Track(object value)
            {
                if (value != null && Marshal.IsComObject(value) && !_references.Any(r => ReferenceEquals(r, value)))
                    _references.Add(value);
                return value;
            }
            public object Get(object value, string member, params object[] args) => Invoke(value, member, BindingFlags.GetProperty, args);
            public object Call(object value, string member, params object[] args) => Invoke(value, member, BindingFlags.InvokeMethod, args);
            public void Set(object value, string member, object arg) => Invoke(value, member, BindingFlags.SetProperty, new[] { arg });
            public void SetUncancelled(object value, string member, object arg) =>
                value.GetType().InvokeMember(member, BindingFlags.SetProperty, null, value, new[] { arg });
            private object Invoke(object value, string member, BindingFlags flags, object[] args)
            {
                _cancellation.ThrowIfCancellationRequested();
                return Track(value.GetType().InvokeMember(member, flags | BindingFlags.OptionalParamBinding, null, value, args));
            }
            public void Dispose()
            {
                // Release only our references. Never Close/Quit a document or user's application.
                for (int i = _references.Count - 1; i >= 0; i--) Marshal.ReleaseComObject(_references[i]);
            }
        }
    }
}
