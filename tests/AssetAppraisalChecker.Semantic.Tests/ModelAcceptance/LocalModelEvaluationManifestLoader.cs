using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelEvaluationManifestLoader
    {
        internal LoadedLocalModelEvaluationManifest Load(string path)
        {
            string fullPath = RequireFile(path);
            LocalModelEvaluationManifest manifest = Deserialize<LocalModelEvaluationManifest>(
                File.ReadAllBytes(fullPath));
            new LocalModelEvaluationManifestValidator().Validate(manifest);
            return new LoadedLocalModelEvaluationManifest(
                manifest,
                LocalModelAcceptanceHash.ComputeFileSha256(fullPath));
        }

        internal LocalModelEvaluationManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("Model manifest JSON is required.");
            }

            LocalModelEvaluationManifest manifest = Deserialize<LocalModelEvaluationManifest>(
                Encoding.UTF8.GetBytes(json));
            new LocalModelEvaluationManifestValidator().Validate(manifest);
            return manifest;
        }

        private static T Deserialize<T>(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                {
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                }
            }
            catch (Exception exception) when (
                exception is SerializationException
                || exception is InvalidCastException)
            {
                throw new InvalidDataException("Invalid local model manifest JSON.", exception);
            }
        }

        private static string RequireFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Manifest path is required.", nameof(path));
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Model manifest was not found.", fullPath);
            }

            return fullPath;
        }
    }
}
