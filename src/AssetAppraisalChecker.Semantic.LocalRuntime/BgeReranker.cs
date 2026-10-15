using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class BgeReranker : IReranker
    {
        private readonly object _sync = new object();
        private readonly InferenceSession _session;
        private readonly NativePairTokenizer _tokenizer;
        private bool _disposed;

        public BgeReranker(
            VerifiedLocalModelAssets assets,
            string modelHash)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            if (!SemanticHash.IsSha256(modelHash))
            {
                throw new ArgumentException(
                    "Reranker model hash 必须为 SHA-256。",
                    nameof(modelHash));
            }

            NativePairTokenizer tokenizer = null;
            InferenceSession session = null;
            try
            {
                tokenizer = new NativePairTokenizer(
                    assets.PairTokenizerLibraryPath,
                    assets.RerankerTokenizerPath);
                session = CreateSession(assets.RerankerModelPath);
                VerifyModelContract(session);
                _tokenizer = tokenizer;
                _session = session;
                ModelHash = modelHash.ToLowerInvariant();
            }
            catch
            {
                session?.Dispose();
                tokenizer?.Dispose();
                throw;
            }
        }

        public string ModelId => LocalSemanticRuntimeOptions.RerankerModelId;

        public string ModelRevision => LocalSemanticRuntimeOptions.RerankerRevision;

        public string ModelHash { get; }

        public IReadOnlyList<double> Score(
            string query,
            IReadOnlyList<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Reranker query 不能为空。", nameof(query));
            }

            if (candidates == null || candidates.Count == 0
                || candidates.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Reranker candidates 必须为非空文本集合。",
                    nameof(candidates));
            }

            lock (_sync)
            {
                ThrowIfDisposed();
                PairEncoding[] encodings = _tokenizer.Encode(query, candidates);
                int length = encodings[0].input_ids.Length;
                if (length == 0 || encodings.Any(item =>
                    item.input_ids.Length != length
                    || item.attention_mask.Length != length))
                {
                    throw new InvalidDataException(
                        "Pair tokenizer 未返回矩形 batch tensor。");
                }

                long[] ids = encodings.SelectMany(item => item.input_ids)
                    .Select(value => (long)value)
                    .ToArray();
                long[] masks = encodings.SelectMany(item => item.attention_mask)
                    .Select(value => (long)value)
                    .ToArray();
                int[] shape = { encodings.Length, length };
                var inputs = new[]
                {
                    NamedOnnxValue.CreateFromTensor(
                        "input_ids",
                        new DenseTensor<long>(ids, shape)),
                    NamedOnnxValue.CreateFromTensor(
                        "attention_mask",
                        new DenseTensor<long>(masks, shape))
                };
                using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                    _session.Run(inputs))
                {
                    double[] logits = results
                        .Single(item => item.Name == "logits")
                        .AsTensor<float>()
                        .ToArray()
                        .Select(value => (double)value)
                        .ToArray();
                    if (logits.Length != candidates.Count
                        || logits.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
                    {
                        throw new InvalidDataException("Reranker logits 不符合输出契约。");
                    }

                    return logits;
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _session.Dispose();
                _tokenizer.Dispose();
                _disposed = true;
            }
        }

        private static InferenceSession CreateSession(string modelPath)
        {
            using (var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                IntraOpNumThreads = 1,
                InterOpNumThreads = 1
            })
            {
                return new InferenceSession(modelPath, options);
            }
        }

        private static void VerifyModelContract(InferenceSession session)
        {
            string[] inputs = session.InputMetadata.Keys
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (!inputs.SequenceEqual(
                    new[] { "attention_mask", "input_ids" },
                    StringComparer.Ordinal)
                || !session.OutputMetadata.ContainsKey("logits"))
            {
                throw new InvalidDataException("Reranker ONNX I/O contract 不匹配。");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BgeReranker));
            }
        }

        private sealed class NativePairTokenizer : IDisposable
        {
            private static readonly JavaScriptSerializer Json =
                new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

            private IntPtr _library;
            private IntPtr _handle;

            public NativePairTokenizer(string libraryPath, string tokenizerPath)
            {
                _library = NativeMethods.LoadLibrary(libraryPath);
                if (_library == IntPtr.Zero)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "无法加载受控 pair-tokenizer native wrapper。");
                }

                byte[] path = Encoding.UTF8.GetBytes(tokenizerPath);
                int status = NativeMethods.Create(
                    path,
                    new UIntPtr((uint)path.Length),
                    out _handle,
                    out IntPtr error,
                    out UIntPtr errorLength);
                string message = ReadAndFree(error, errorLength);
                if (status != 0 || _handle == IntPtr.Zero)
                {
                    Dispose();
                    throw new InvalidOperationException(
                        "Pair-tokenizer 初始化失败："
                        + (string.IsNullOrWhiteSpace(message) ? "Unknown" : message));
                }
            }

            public PairEncoding[] Encode(
                string query,
                IReadOnlyList<string> candidates)
            {
                if (_handle == IntPtr.Zero)
                {
                    throw new ObjectDisposedException(nameof(NativePairTokenizer));
                }

                var request = new PairRequest
                {
                    cases = candidates.Select((value, index) => new PairCase
                    {
                        caseId = "candidate-" + index,
                        query = query,
                        passage = value
                    }).ToArray()
                };
                byte[] input = Encoding.UTF8.GetBytes(Json.Serialize(request));
                int status = NativeMethods.EncodeBatch(
                    _handle,
                    input,
                    new UIntPtr((uint)input.Length),
                    out IntPtr output,
                    out UIntPtr outputLength);
                string response = ReadAndFree(output, outputLength);
                if (status != 0)
                {
                    throw new InvalidOperationException(
                        "Pair-tokenizer 编码失败：" + response);
                }

                PairResponse result = Json.Deserialize<PairResponse>(response);
                if (result?.cases == null || result.cases.Length != candidates.Count
                    || result.cases.Any(item => item == null))
                {
                    throw new InvalidDataException(
                        "Pair-tokenizer response contract 不匹配。");
                }

                return result.cases;
            }

            public void Dispose()
            {
                if (_handle != IntPtr.Zero)
                {
                    NativeMethods.Destroy(_handle);
                    _handle = IntPtr.Zero;
                }

                if (_library != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(_library);
                    _library = IntPtr.Zero;
                }
            }

            private static string ReadAndFree(IntPtr pointer, UIntPtr length)
            {
                ulong size = length.ToUInt64();
                if (pointer == IntPtr.Zero || size == 0)
                {
                    return string.Empty;
                }

                if (size > int.MaxValue)
                {
                    throw new InvalidDataException("Native buffer 过大。");
                }

                var data = new byte[(int)size];
                Marshal.Copy(pointer, data, 0, data.Length);
                NativeMethods.FreeBuffer(pointer, length);
                return Encoding.UTF8.GetString(data);
            }
        }

        private sealed class PairRequest
        {
            public PairCase[] cases { get; set; }
        }

        private sealed class PairCase
        {
            public string caseId { get; set; }

            public string query { get; set; }

            public string passage { get; set; }
        }

        private sealed class PairResponse
        {
            public PairEncoding[] cases { get; set; }
        }

        private sealed class PairEncoding
        {
            public uint[] input_ids { get; set; }

            public uint[] attention_mask { get; set; }
        }

        private static class NativeMethods
        {
            private const string Wrapper = "asset_appraisal_checker_pair_tokenizer";

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr LoadLibrary(string fileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool FreeLibrary(IntPtr module);

            [DllImport(
                Wrapper,
                CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "pair_tokenizer_create")]
            internal static extern int Create(
                byte[] tokenizerPath,
                UIntPtr tokenizerPathLength,
                out IntPtr handle,
                out IntPtr error,
                out UIntPtr errorLength);

            [DllImport(
                Wrapper,
                CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "pair_tokenizer_encode_batch")]
            internal static extern int EncodeBatch(
                IntPtr handle,
                byte[] requestJson,
                UIntPtr requestLength,
                out IntPtr outputJson,
                out UIntPtr outputJsonLength);

            [DllImport(
                Wrapper,
                CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "pair_tokenizer_destroy")]
            internal static extern void Destroy(IntPtr handle);

            [DllImport(
                Wrapper,
                CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "pair_tokenizer_free_buffer")]
            internal static extern void FreeBuffer(IntPtr data, UIntPtr length);
        }
    }
}
