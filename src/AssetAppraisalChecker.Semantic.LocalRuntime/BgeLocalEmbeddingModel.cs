using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class BgeLocalEmbeddingModel : ILocalEmbeddingModel
    {
        private const int MaximumLength = 512;
        private const int EmbeddingDimension = 512;

        private readonly object _sync = new object();
        private readonly InferenceSession _model;
        private readonly InferenceSession _tokenizer;
        private bool _disposed;

        public BgeLocalEmbeddingModel(
            VerifiedLocalModelAssets assets,
            string modelHash)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            if (!SemanticHash.IsSha256(modelHash))
            {
                throw new ArgumentException("BGE model hash 必须为 SHA-256。", nameof(modelHash));
            }

            InferenceSession model = null;
            InferenceSession tokenizer = null;
            try
            {
                model = CreateModelSession(assets.BgeModelPath);
                using (var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
                })
                {
                    options.RegisterCustomOpLibrary(assets.OrtExtensionsLibraryPath);
                    tokenizer = new InferenceSession(assets.BgeTokenizerPath, options);
                }

                VerifyTokenizerContract(tokenizer);
                _model = model;
                _tokenizer = tokenizer;
                ModelHash = modelHash.ToLowerInvariant();
            }
            catch
            {
                tokenizer?.Dispose();
                model?.Dispose();
                throw;
            }
        }

        public string ModelId => LocalSemanticRuntimeOptions.BgeModelId;

        public string ModelRevision => LocalSemanticRuntimeOptions.BgeRevision;

        public string ModelHash { get; }

        public int Dimension => EmbeddingDimension;

        public float[] Embed(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Embedding 输入不能为空。", nameof(text));
            }

            lock (_sync)
            {
                ThrowIfDisposed();
                TokenTensors tensors = Tokenize(text);
                int[] shape = { 1, tensors.InputIds.Length };
                var inputs = new[]
                {
                    NamedOnnxValue.CreateFromTensor(
                        "input_ids",
                        new DenseTensor<long>(tensors.InputIds, shape)),
                    NamedOnnxValue.CreateFromTensor(
                        "attention_mask",
                        new DenseTensor<long>(tensors.AttentionMask, shape)),
                    NamedOnnxValue.CreateFromTensor(
                        "token_type_ids",
                        new DenseTensor<long>(tensors.TokenTypeIds, shape))
                };
                using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                    _model.Run(inputs))
                {
                    Tensor<float> output = results
                        .Single(item => item.Name == "last_hidden_state")
                        .AsTensor<float>();
                    int[] dimensions = output.Dimensions.ToArray();
                    if (!dimensions.SequenceEqual(
                        new[] { 1, tensors.InputIds.Length, EmbeddingDimension }))
                    {
                        throw new InvalidDataException(
                            "BGE Transformer output shape 不符合契约。");
                    }

                    float[] values = output.ToArray();
                    var pooled = new float[EmbeddingDimension];
                    Array.Copy(values, pooled, EmbeddingDimension);
                    return Normalize(pooled);
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

                _tokenizer.Dispose();
                _model.Dispose();
                _disposed = true;
            }
        }

        private TokenTensors Tokenize(string text)
        {
            var input = NamedOnnxValue.CreateFromTensor(
                "text",
                new DenseTensor<string>(new[] { text }, new[] { 1 }));
            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                _tokenizer.Run(new[] { input }))
            {
                long[] ids = ReadTensor(results, "input_ids");
                long[] mask = ReadTensor(results, "attention_mask");
                long[] types = ReadTensor(results, "token_type_ids");
                if (ids.Length == 0 || ids.Length > MaximumLength
                    || mask.Length != ids.Length || types.Length != ids.Length
                    || mask.Any(value => value != 0 && value != 1))
                {
                    throw new InvalidDataException("BGE tokenizer tensor 不符合契约。");
                }

                return new TokenTensors(ids, mask, types);
            }
        }

        private static long[] ReadTensor(
            IEnumerable<DisposableNamedOnnxValue> results,
            string name)
        {
            return results.Single(item => item.Name == name).AsTensor<long>().ToArray();
        }

        private static float[] Normalize(float[] values)
        {
            double norm = Math.Sqrt(values.Sum(value => (double)value * value));
            if (norm <= 0d || double.IsNaN(norm) || double.IsInfinity(norm))
            {
                throw new InvalidDataException("BGE embedding norm 无效。");
            }

            return values.Select(value => (float)(value / norm)).ToArray();
        }

        private static InferenceSession CreateModelSession(string modelPath)
        {
            InferenceSession session;
            using (var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                IntraOpNumThreads = 1,
                InterOpNumThreads = 1
            })
            {
                session = new InferenceSession(modelPath, options);
            }

            if (!session.InputMetadata.ContainsKey("input_ids")
                || !session.InputMetadata.ContainsKey("attention_mask")
                || !session.InputMetadata.ContainsKey("token_type_ids")
                || !session.OutputMetadata.ContainsKey("last_hidden_state")
                || session.OutputMetadata["last_hidden_state"].Dimensions.Last()
                    != EmbeddingDimension)
            {
                session.Dispose();
                throw new InvalidDataException("BGE ONNX I/O contract 不匹配。");
            }

            return session;
        }

        private static void VerifyTokenizerContract(InferenceSession tokenizer)
        {
            if (!tokenizer.InputMetadata.ContainsKey("text")
                || !tokenizer.OutputMetadata.ContainsKey("input_ids")
                || !tokenizer.OutputMetadata.ContainsKey("attention_mask")
                || !tokenizer.OutputMetadata.ContainsKey("token_type_ids"))
            {
                throw new InvalidDataException("BGE tokenizer ONNX I/O contract 不匹配。");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BgeLocalEmbeddingModel));
            }
        }

        private sealed class TokenTensors
        {
            public TokenTensors(long[] inputIds, long[] attentionMask, long[] tokenTypeIds)
            {
                InputIds = inputIds;
                AttentionMask = attentionMask;
                TokenTypeIds = tokenTypeIds;
            }

            public long[] InputIds { get; }

            public long[] AttentionMask { get; }

            public long[] TokenTypeIds { get; }
        }
    }
}
